<#
.SYNOPSIS
  Build, optionally migrate, and zip-deploy MOOG-API, MOOG-Portal, and MOOG-Worker to Azure UAT.

.DESCRIPTION
  Uses Azure CLI identity (az login) — no publish-profile passwords.
  Resource group defaults match the existing UAT apps.

.PARAMETER ResourceGroup
  Azure resource group. Default: Online-Order-Gateway

.PARAMETER Targets
  Which apps to deploy. Default: Api, Portal, Worker

.PARAMETER SkipBuild
  Skip dotnet publish (reuse existing artifacts under .artifacts/)

.PARAMETER SkipMigrate
  Skip EF database update

.PARAMETER SqlConnectionString
  ADO.NET connection string for EF migrate. If omitted, reads env GATEWAY_SQL_CONNECTION_STRING.
  Required unless -SkipMigrate is set. Do not commit secrets.

.EXAMPLE
  az login
  $env:GATEWAY_SQL_CONNECTION_STRING = 'Server=tcp:sql-moog-uat.database.windows.net,1433;Initial Catalog=db-moog;User ID=gatewayadmin;Password=***;Encrypt=True;TrustServerCertificate=False;MultipleActiveResultSets=true;Authentication=SqlPassword'
  .\scripts\Deploy-Uat.ps1

.EXAMPLE
  .\scripts\Deploy-Uat.ps1 -Targets Portal -SkipMigrate
#>
[CmdletBinding()]
param(
    [string] $ResourceGroup = "Online-Order-Gateway",

    [ValidateSet("Api", "Portal", "Worker")]
    [string[]] $Targets = @("Api", "Portal", "Worker"),

    [switch] $SkipBuild,

    [switch] $SkipMigrate,

    [string] $SqlConnectionString = $env:GATEWAY_SQL_CONNECTION_STRING,

    [string] $Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $repoRoot

$apps = @{
    Api = @{
        Project     = "src/Gateway.Api/Gateway.Api.csproj"
        AzureName   = "MOOG-API"
        Kind        = "webapp"
        ArtifactDir = ".artifacts/api"
    }
    Portal = @{
        Project     = "src/Gateway.Portal/Gateway.Portal.csproj"
        AzureName   = "MOOG-Portal"
        Kind        = "webapp"
        ArtifactDir = ".artifacts/portal"
    }
    Worker = @{
        Project     = "src/Gateway.Worker/Gateway.Worker.csproj"
        AzureName   = "MOOG-Worker"
        Kind        = "functionapp"
        ArtifactDir = ".artifacts/worker"
    }
}

function Assert-Command([string] $Name) {
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Required command not found on PATH: $Name"
    }
}

function Write-Step([string] $Message) {
    Write-Host ""
    Write-Host "==> $Message" -ForegroundColor Cyan
}

Assert-Command "dotnet"
Assert-Command "az"

Write-Step "Checking Azure CLI login"
$account = az account show 2>$null | ConvertFrom-Json
if (-not $account) {
    throw "Not logged in to Azure CLI. Run: az login"
}
Write-Host "Subscription: $($account.name) ($($account.id))"

if (-not $SkipMigrate) {
    if ([string]::IsNullOrWhiteSpace($SqlConnectionString)) {
        throw "EF migrate needs -SqlConnectionString or env GATEWAY_SQL_CONNECTION_STRING (or pass -SkipMigrate)."
    }

    Write-Step "Applying EF migrations to Azure SQL"
    Assert-Command "dotnet"
    # Design package lives on Infrastructure; no API host required.
    & dotnet ef database update `
        --project "src/Gateway.Infrastructure/Gateway.Infrastructure.csproj" `
        --startup-project "src/Gateway.Infrastructure/Gateway.Infrastructure.csproj" `
        --connection $SqlConnectionString
    if ($LASTEXITCODE -ne 0) { throw "EF database update failed (exit $LASTEXITCODE)." }
    Write-Host "Migrations applied." -ForegroundColor Green
}
else {
    Write-Host "Skipping EF migrate (-SkipMigrate)." -ForegroundColor Yellow
}

$artifactRoot = Join-Path $repoRoot ".artifacts"
if (-not (Test-Path $artifactRoot)) {
    New-Item -ItemType Directory -Path $artifactRoot | Out-Null
}

foreach ($target in $Targets) {
    $app = $apps[$target]
    $outDir = Join-Path $repoRoot $app.ArtifactDir
    $zipPath = Join-Path $artifactRoot ("{0}.zip" -f $target.ToLowerInvariant())

    if (-not $SkipBuild) {
        Write-Step "Publishing $target ($($app.Project))"
        if (Test-Path $outDir) {
            Remove-Item -Recurse -Force $outDir
        }
        New-Item -ItemType Directory -Path $outDir | Out-Null

        & dotnet publish $app.Project `
            -c $Configuration `
            -o $outDir `
            --nologo
        if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed for $target (exit $LASTEXITCODE)." }
    }
    else {
        if (-not (Test-Path $outDir)) {
            throw "SkipBuild set but artifact folder missing: $outDir"
        }
        Write-Host "Reusing publish output: $outDir" -ForegroundColor Yellow
    }

    Write-Step "Zipping $target -> $zipPath"
    if (Test-Path $zipPath) {
        Remove-Item -Force $zipPath
    }
    # Compress contents of the publish folder (not the folder itself).
    Compress-Archive -Path (Join-Path $outDir "*") -DestinationPath $zipPath -Force

    Write-Step "Deploying $target to $($app.AzureName) ($($app.Kind))"
    if ($app.Kind -eq "functionapp") {
        & az functionapp deployment source config-zip `
            --resource-group $ResourceGroup `
            --name $app.AzureName `
            --src $zipPath `
            --timeout 600
    }
    else {
        & az webapp deploy `
            --resource-group $ResourceGroup `
            --name $app.AzureName `
            --src-path $zipPath `
            --type zip `
            --async false `
            --timeout 600
    }
    if ($LASTEXITCODE -ne 0) { throw "Azure deploy failed for $target (exit $LASTEXITCODE)." }
    Write-Host "$target deployed." -ForegroundColor Green
}

Write-Step "Done"
Write-Host "Deployed: $($Targets -join ', ')" -ForegroundColor Green
Write-Host "Portal: https://moog-portal-dgbkbfanc2bdb9fv.southafricanorth-01.azurewebsites.net"
Write-Host "API:    https://moog-api-cehvddbad6c0f8gd.southafricanorth-01.azurewebsites.net"
Write-Host ""
Write-Host "Smoke: open Command Centre, confirm health chips + recent orders; send a test order."
