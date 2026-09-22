[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string] $Script,

    [Parameter(Position = 1, ValueFromRemainingArguments = $true)]
    [string[]] $ScriptArguments
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# git prints the root as UTF-8, but `& git` decodes a native command's stdout
# with the console encoding: on a console at the OEM code page a Cyrillic root
# came back as mojibake. Run git as a Process whose stdout is read
# as UTF-8 instead; the console's own encoding is never touched, so this also
# works where there is no console. The root stays git's --show-toplevel:
# computing it from PowerShell's location breaks under links, because git
# resolves them and PowerShell does not.
$git = Get-Command git -CommandType Application -ErrorAction SilentlyContinue |
    Select-Object -First 1
if (-not $git) {
    throw 'git was not found.'
}
$startInfo = New-Object System.Diagnostics.ProcessStartInfo
$startInfo.FileName = $git.Source
$startInfo.Arguments = 'rev-parse --show-toplevel'
$startInfo.WorkingDirectory = (Get-Location -PSProvider FileSystem).ProviderPath
$startInfo.UseShellExecute = $false
$startInfo.CreateNoWindow = $true
$startInfo.RedirectStandardOutput = $true
$startInfo.RedirectStandardError = $true
$startInfo.StandardOutputEncoding = New-Object System.Text.UTF8Encoding $false
$startInfo.StandardErrorEncoding = New-Object System.Text.UTF8Encoding $false
$gitProcess = [System.Diagnostics.Process]::Start($startInfo)
# Drain stderr asynchronously so a full pipe cannot block the stdout read.
$gitErrors = $gitProcess.StandardError.ReadToEndAsync()
$repoRoot = $gitProcess.StandardOutput.ReadToEnd().Trim()
$gitProcess.WaitForExit()
$gitErrorText = $gitErrors.Result.Trim()
if ($gitProcess.ExitCode -ne 0 -or [string]::IsNullOrWhiteSpace($repoRoot)) {
    # Keep git's reason: "dubious ownership" is not "outside a worktree".
    if ($gitErrorText) {
        throw "Run this command inside a Git worktree. git: $gitErrorText"
    }
    throw 'Run this command inside a Git worktree.'
}
$repoRoot = [System.IO.Path]::GetFullPath($repoRoot)

$scriptPath = Join-Path $repoRoot $Script
if (-not (Test-Path -LiteralPath $scriptPath -PathType Leaf)) {
    throw "Shell script not found: $scriptPath"
}

$bashCandidates = @(
    (Join-Path $env:ProgramFiles 'Git\bin\bash.exe'),
    (Join-Path $env:ProgramFiles 'Git\usr\bin\bash.exe')
)
if (${env:ProgramFiles(x86)}) {
    $bashCandidates += Join-Path ${env:ProgramFiles(x86)} 'Git\bin\bash.exe'
}
$bashCandidates += @(Get-Command bash.exe -All -ErrorAction SilentlyContinue |
    Where-Object { $_.Source -match '\\Git\\' } |
    Select-Object -ExpandProperty Source)

$bash = $bashCandidates | Where-Object { Test-Path -LiteralPath $_ } |
    Select-Object -First 1
if (-not $bash) {
    throw 'Git for Windows Bash was not found.'
}

Push-Location -LiteralPath $repoRoot
try {
    # Run the script through a -c command, not as a file handed to
    # `bash --login`: then the script's commands are the login shell's own,
    # its `exit` makes the login shell read Git for Windows'
    # /etc/bash.bash_logout, and that clears the screen over the output.
    #
    # Legacy native argument passing (Windows PowerShell 5.1, PowerShell
    # before 7.3) does not escape the double quotes inside 'exec "$@"': the
    # quoting of the whole command line shifted and every later argument with
    # a space was split in two. There the quotes are escaped by
    # hand; the standard mode escapes them itself and would double the
    # backslashes.
    $argumentPassing = Get-Variable PSNativeCommandArgumentPassing -ValueOnly -ErrorAction SilentlyContinue
    $bootstrap = 'exec "$@"'
    if (-not $argumentPassing -or "$argumentPassing" -eq 'Legacy') {
        $bootstrap = 'exec \"$@\"'
    }
    & $bash --login -c $bootstrap -- $Script @ScriptArguments
    $exitCode = $LASTEXITCODE
}
finally {
    Pop-Location
}

exit $exitCode
