[CmdletBinding()]
param()

# Regression cases for scripts/invoke-sh.ps1, the launcher behind every
# scripts/*.ps1 wrapper.
#
# The defect: the launcher took the repository root from git's stdout, which
# PowerShell decodes with [Console]::OutputEncoding. On a console left at the
# OEM code page (866 on a Russian Windows) a root containing Cyrillic came back
# as mojibake, and every wrapper threw "Shell script not found".
#
# The cases do not depend on where this repository is checked out: they build
# a throwaway repository and a linked worktree under a Cyrillic directory in
# %TEMP%, copy the launcher into it, and run it in a child PowerShell whose
# console encoding is forced to 866, from a subdirectory of the checkout (and
# through a junction onto one) so the launcher has to find the root and move
# there itself. The Cyrillic text is
# built from code points so this file stays ASCII: Windows PowerShell 5.1 reads
# a BOM-less script as ANSI.
#
# The child shares this console, so its 866 outlives it; the console's own
# encoding is put back at the end.

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([Environment]::OSVersion.Platform -ne 'Win32NT') {
    Write-Output 'invoke-sh.ps1: NOT CHECKED, the launcher is Windows-only.'
    exit 0
}

$launcher = (Resolve-Path (Join-Path $PSScriptRoot '..\invoke-sh.ps1')).Path
$cyrillic = -join ([char[]](0x043C, 0x0438, 0x043D, 0x0442, 0x0440, 0x0430, 0x043D, 0x0441))
$utf8 = [System.Text.UTF8Encoding]::new($false)

$shells = @(
    @(Get-Command pwsh -ErrorAction SilentlyContinue) +
    @(Get-Command powershell.exe -ErrorAction SilentlyContinue) |
        Select-Object -ExpandProperty Source
)
if ($shells.Count -eq 0) {
    throw 'Neither pwsh nor powershell.exe was found.'
}

function Invoke-Git {
    # Continue: Windows PowerShell turns redirected native stderr into errors.
    $ErrorActionPreference = 'Continue'
    $output = & git @args 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "git $($args -join ' ') failed: $output"
    }
}

$sandbox = Join-Path ([System.IO.Path]::GetTempPath()) (
    "invoke-sh-$cyrillic-" + [guid]::NewGuid().ToString('N').Substring(0, 8))
$mainRoot = Join-Path $sandbox "repo $cyrillic"
$worktreeRoot = Join-Path $sandbox "worktree-$cyrillic"
$arguments = @('plain', "arg-$cyrillic", 'with space')

# The child sets the console encoding itself: a wrapper runs in the user's
# console, not in this process.
$childCommand = @'
[Console]::OutputEncoding = [System.Text.Encoding]::GetEncoding(866)
$env:HOME = $env:INVOKE_SH_CASE_HOME
Set-Location -LiteralPath $env:INVOKE_SH_CASE_START
& .\invoke-sh.ps1 scripts/probe.sh plain $env:INVOKE_SH_CASE_ARG 'with space'
exit $LASTEXITCODE
'@

$consoleEncoding = $null
try {
    $consoleEncoding = [Console]::OutputEncoding
}
catch {
    # No console attached: there is nothing to restore.
}

# Run as a git hook (.githooks/pre-push -> verify.sh) this process inherits
# GIT_DIR and friends, and they beat `git -C <sandbox>`: init, config, add and
# commit would land in the real repository (found in review). Clear every
# variable git itself lists as repository-local, for the sandbox git calls and
# the child shells alike; restore them at the end.
$savedGitEnv = @{}
foreach ($name in @(& git rev-parse --local-env-vars)) {
    $value = [Environment]::GetEnvironmentVariable($name)
    if ($null -ne $value) {
        $savedGitEnv[$name] = $value
        # Not SetEnvironmentVariable($name, $null): PowerShell 7 passes the
        # $null as "", and git rejects an empty GIT_DIR.
        Remove-Item -LiteralPath "Env:\$name"
    }
}

$failures = 0
$caseCount = 0
$junction = $null
try {
    New-Item -ItemType Directory -Path (Join-Path $mainRoot 'scripts') | Out-Null
    Copy-Item -LiteralPath $launcher -Destination (Join-Path $mainRoot 'scripts\invoke-sh.ps1')
    # The probe records where bash ran and exactly which arguments reached it.
    # It ends with an explicit exit, as scripts/verify.sh does: a non-interactive
    # login shell reads bash_logout only on the exit builtin.
    [System.IO.File]::WriteAllText(
        (Join-Path $mainRoot 'scripts\probe.sh'),
        "#!/bin/sh`n{ pwd -W; for a in `"`$@`"; do printf '%s\n' `"`$a`"; done; } > probe.out`nexit 0`n",
        $utf8)

    Invoke-Git -C $mainRoot init --quiet
    Invoke-Git -C $mainRoot config core.autocrlf false
    Invoke-Git -C $mainRoot add scripts
    Invoke-Git -C $mainRoot -c user.name=invoke-sh-cases -c user.email=invoke-sh-cases@localhost -c commit.gpgsign=false commit --quiet -m probe
    Invoke-Git -C $mainRoot worktree add --quiet $worktreeRoot

    # bash reads ~/.bash_logout only when a login shell exits.
    $homeDir = Join-Path $sandbox 'home'
    $logoutMarker = Join-Path $homeDir 'logout-ran'
    New-Item -ItemType Directory -Path $homeDir | Out-Null
    [System.IO.File]::WriteAllText(
        (Join-Path $homeDir '.bash_logout'), ": > `"`$HOME/logout-ran`"`n", $utf8)

    $env:INVOKE_SH_CASE_ARG = $arguments[1]
    $env:INVOKE_SH_CASE_HOME = $homeDir
    # A junction outside the repository onto its scripts directory: git
    # resolves the link, PowerShell keeps the path as typed, so a root derived
    # from PowerShell's location lands outside the checkout.
    $junction = Join-Path $sandbox "link-$cyrillic"
    New-Item -ItemType Junction -Path $junction -Value (Join-Path $mainRoot 'scripts') | Out-Null

    $roots = @(
        [pscustomobject]@{ Name = 'main checkout'; Path = $mainRoot; Start = Join-Path $mainRoot 'scripts' },
        [pscustomobject]@{ Name = 'linked worktree'; Path = $worktreeRoot; Start = Join-Path $worktreeRoot 'scripts' },
        [pscustomobject]@{ Name = 'through a junction'; Path = $mainRoot; Start = $junction }
    )
    foreach ($shell in $shells) {
        foreach ($root in $roots) {
            $caseCount++
            $probeOut = Join-Path $root.Path 'probe.out'
            Remove-Item -LiteralPath $probeOut -ErrorAction SilentlyContinue

            $env:INVOKE_SH_CASE_START = $root.Start
            $childOutput = & {
                $ErrorActionPreference = 'Continue'
                & $shell -NoProfile -ExecutionPolicy Bypass -Command $childCommand 2>&1
            }
            $exitCode = $LASTEXITCODE

            $problems = @()
            if ($exitCode -ne 0) {
                $problems += "exit code $exitCode"
            }
            # The script's exit must not run the login shell's logout files:
            # Git for Windows' /etc/bash.bash_logout clears the screen over
            # the script's output.
            if (Test-Path -LiteralPath $logoutMarker) {
                $problems += "the script's exit ran bash_logout (it ran as the login shell's own commands)"
                Remove-Item -LiteralPath $logoutMarker
            }
            if (-not (Test-Path -LiteralPath $probeOut)) {
                $problems += 'the probe script did not run'
            }
            else {
                $lines = @([System.IO.File]::ReadAllLines($probeOut, $utf8))
                $expectedDir = $root.Path.TrimEnd('\')
                $actualDir = ''
                if ($lines.Count -gt 0) {
                    $actualDir = $lines[0].Replace('/', '\').TrimEnd('\')
                }
                if ($actualDir -ne $expectedDir) {
                    $problems += "bash ran in '$actualDir', expected '$expectedDir'"
                }
                $actualArgs = @($lines | Select-Object -Skip 1)
                if (($actualArgs -join '|') -ne ($arguments -join '|')) {
                    $problems += "arguments '$($actualArgs -join '|')', expected '$($arguments -join '|')'"
                }
            }

            $label = '{0}, {1}' -f (Split-Path -Leaf $shell), $root.Name
            if ($problems.Count -eq 0) {
                Write-Output "  ok   $label"
            }
            else {
                $failures++
                Write-Output "  FAIL $label"
                foreach ($problem in $problems) {
                    [Console]::Error.WriteLine("       $problem")
                }
                if ($childOutput) {
                    [Console]::Error.WriteLine(($childOutput | Out-String).Trim())
                }
            }
        }
    }
}
finally {
    # First, so a failed cleanup below cannot leave the caller without them.
    foreach ($name in $savedGitEnv.Keys) {
        [Environment]::SetEnvironmentVariable($name, $savedGitEnv[$name])
    }
    if ($consoleEncoding) {
        [Console]::OutputEncoding = $consoleEncoding
    }
    Remove-Item Env:\INVOKE_SH_CASE_START, Env:\INVOKE_SH_CASE_ARG, Env:\INVOKE_SH_CASE_HOME -ErrorAction SilentlyContinue
    # Remove the junction itself first, so the recursive delete cannot follow it.
    if ($junction -and (Test-Path -LiteralPath $junction)) {
        [System.IO.Directory]::Delete($junction)
    }
    if (Test-Path -LiteralPath $sandbox) {
        Remove-Item -LiteralPath $sandbox -Recurse -Force
    }
}

Write-Output "invoke-sh.ps1: $caseCount cases, $failures failed."
if ($failures -gt 0) {
    exit 1
}
exit 0
