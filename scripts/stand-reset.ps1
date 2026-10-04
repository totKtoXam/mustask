[CmdletBinding()]
param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]] $Arguments
)

$ErrorActionPreference = 'Stop'
try {
    & (Join-Path $PSScriptRoot 'invoke-sh.ps1') 'scripts/stand-reset.sh' @Arguments
    exit $LASTEXITCODE
}
catch {
    Write-Error $_
    exit 1
}
