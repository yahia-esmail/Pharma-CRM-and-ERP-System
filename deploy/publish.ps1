<#
.SYNOPSIS
  Builds, tests and publishes the three deployable apps into one folder per app:
    <Output>\api        PharmaERP.Web.Api   (ASP.NET Core; IIS or Kestrel behind nginx)
    <Output>\dashboard  PharmaERP.Web.Mvc   (ASP.NET Core; IIS or Kestrel behind nginx)
    <Output>\fieldapp   PharmaERP.FieldApp  (static files: serve <Output>\fieldapp\wwwroot)
.EXAMPLE
  ./deploy/publish.ps1 -Output D:\Releases\2026-10-05 -FieldAppSettings D:\Config\fieldapp.appsettings.json
#>
param(
    [string] $Output = (Join-Path $PSScriptRoot "..\artifacts\publish"),
    # The field app's production appsettings.json (API address) - start from deploy/settings/FieldApp.appsettings.json.
    [string] $FieldAppSettings,
    [switch] $SkipTests
)
$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..")

function Invoke-Step([string] $name, [scriptblock] $command) {
    Write-Host "==> $name" -ForegroundColor Cyan
    & $command
    if ($LASTEXITCODE -ne 0) { throw "$name failed (exit code $LASTEXITCODE)." }
}

if (Test-Path $Output) { Remove-Item $Output -Recurse -Force }

Invoke-Step "Build" { dotnet build "$root\PharmaERP.slnx" -c Release }
if (-not $SkipTests) { Invoke-Step "Test" { dotnet test "$root\PharmaERP.slnx" -c Release --no-build } }

Invoke-Step "Publish API"       { dotnet publish "$root\src\Web.Api\PharmaERP.Web.Api.csproj" -c Release -o "$Output\api" }
Invoke-Step "Publish dashboard" { dotnet publish "$root\src\Web.Mvc\PharmaERP.Web.Mvc.csproj" -c Release -o "$Output\dashboard" }
# The production settings go in BEFORE publishing: the service worker checks every file against the hash recorded
# in service-worker-assets.js at publish time, so a file swapped afterwards would make the offline install fail.
$devSettings = Join-Path $root "src\FieldApp\wwwroot\appsettings.json"
$backup = Join-Path ([System.IO.Path]::GetTempPath()) "pharmaerp-fieldapp-appsettings.dev.json"   # outside wwwroot, or it would ship
if ($FieldAppSettings) { Copy-Item $devSettings $backup -Force; Copy-Item $FieldAppSettings $devSettings -Force }
try {
    Invoke-Step "Publish field app" { dotnet publish "$root\src\FieldApp\PharmaERP.FieldApp.csproj" -c Release -o "$Output\fieldapp" }
} finally {
    if ($FieldAppSettings) { Move-Item $backup $devSettings -Force }
}

$fieldRoot = Join-Path $Output "fieldapp\wwwroot"
if (-not $FieldAppSettings) {
    Write-Warning "No -FieldAppSettings: the field app still points at the development API ($((Get-Content (Join-Path $fieldRoot 'appsettings.json') -Raw | ConvertFrom-Json).Api.BaseUrl))."
}

# Development-only files never ship.
Get-ChildItem $Output -Recurse -Include "appsettings.Development.json", "appsettings.Development.json.br", "appsettings.Development.json.gz" | Remove-Item -Force

# Guard: nothing secret in the output (the key folders live outside the sites - Storage:KeysPath).
$leaks = Get-ChildItem $Output -Recurse -Force | Where-Object { $_.Name -in @(".keys", ".uploads", "vapid.json", "jwt-signing.key") -or $_.Name -like "key-*.xml" }
if ($leaks) { throw "Secret files in the publish output: $($leaks.FullName -join ', ')" }

Write-Host "Published to $Output" -ForegroundColor Green
Write-Host "Next: deploy/README.md - settings, secrets, database, first sign-in."
