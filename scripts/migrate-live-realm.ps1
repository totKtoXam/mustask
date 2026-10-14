[CmdletBinding()]
param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]] $Arguments
)

# The live-realm migration (mod-016, BR-M1-28) in PowerShell: the same script as
# scripts/migrate-live-realm.sh (read its header: what it does, when to run it, why
# never scripts/stand-reset.ps1 for the drift check's findings). KEYCLOAK_ADMIN_PASSWORD
# and KEYCLOAK_URI come from the environment, never from an argument.
$ErrorActionPreference = 'Stop'
try {
    & (Join-Path $PSScriptRoot 'invoke-sh.ps1') 'scripts/migrate-live-realm.sh' @Arguments
    exit $LASTEXITCODE
}
catch {
    Write-Error $_
    exit 1
}
