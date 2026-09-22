[CmdletBinding()]
param(
    [Parameter(ValueFromPipeline = $true)]
    [string] $InputObject
)

# The Windows adapter carries no policy of its own: it forwards the Codex
# JSON to the same Python hook that Claude Code uses. Any change to the
# policy therefore takes effect for both vendors at once.

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

try {
    $payload = if ($PSBoundParameters.ContainsKey('InputObject')) {
        $InputObject
    }
    else {
        [Console]::In.ReadToEnd()
    }

    # Codex calls the shell tool `Bash` on every platform, but commandWindows
    # is executed by PowerShell. We normalize only the vendor field before the
    # shared policy runs, otherwise PowerShell commands would be parsed as
    # Bash.
    try {
        $hookEvent = $payload | ConvertFrom-Json
        $toolName = $hookEvent.PSObject.Properties['tool_name']
        if ($null -ne $toolName -and $toolName.Value -eq 'Bash') {
            $toolName.Value = 'PowerShell'
            $payload = $hookEvent | ConvertTo-Json -Compress -Depth 20
        }
    }
    catch {
        # Pass invalid JSON on to the shared fail-open policy without guessing.
    }

    $repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..') -ErrorAction Stop).Path
    if ([string]::IsNullOrWhiteSpace($repoRoot)) {
        exit 0
    }

    $pythonCandidates = @()
    $localPython = Join-Path $env:LOCALAPPDATA 'Python\bin\python.exe'
    if (Test-Path -LiteralPath $localPython) {
        $pythonCandidates += $localPython
    }
    $pythonCandidates += @(Get-Command -Name python.exe, python3.exe -All -ErrorAction SilentlyContinue |
        Where-Object { $_.Source -notmatch '\\WindowsApps\\' } |
        Select-Object -ExpandProperty Source)

    $python = $pythonCandidates | Select-Object -First 1
    if ($null -eq $python) {
        # Matches the fail-open semantics of the shared policy: a missing
        # runtime must not block a legitimate command with no diagnosable way
        # to do so.
        exit 0
    }

    $hook = Join-Path $repoRoot '.claude\hooks\block_wide_fs_search.py'
    # ProcessStartInfo preserves the exact exit code. A regular PowerShell
    # pipeline turns native stderr into an ErrorRecord and, on Windows
    # PowerShell 5, can replace the normal blocking code 2 with the generic
    # code 1.
    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $python
    $startInfo.Arguments = "-B `"$hook`""
    $startInfo.UseShellExecute = $false
    $startInfo.RedirectStandardInput = $true

    $process = [System.Diagnostics.Process]::Start($startInfo)
    $process.StandardInput.Write($payload)
    $process.StandardInput.Close()
    $process.WaitForExit()
    exit $process.ExitCode
}
catch {
    # The policy is deliberately fail-open; the rationale lives in the shared
    # Python hook.
    exit 0
}
