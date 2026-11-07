param([ValidateSet('Build', 'Test')][string]$Action = 'Test')
$ErrorActionPreference = 'Stop'
$serviceRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$artifactRoot = Join-Path $serviceRoot '.artifacts'
foreach ($name in @('dotnet', 'packages', 'http-cache', 'plugins-cache', 'tmp', 'test-results')) {
    [IO.Directory]::CreateDirectory((Join-Path $artifactRoot $name)) | Out-Null
}
# Keep build output, SDK state, package caches and temporary files inside this service.
$env:DOTNET_CLI_HOME = Join-Path $artifactRoot 'dotnet'
$env:NUGET_PACKAGES = Join-Path $artifactRoot 'packages'
$env:NUGET_HTTP_CACHE_PATH = Join-Path $artifactRoot 'http-cache'
$env:NUGET_PLUGINS_CACHE_PATH = Join-Path $artifactRoot 'plugins-cache'
$env:TEMP = Join-Path $artifactRoot 'tmp'
$env:TMP = $env:TEMP
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE = 'true'
Push-Location $serviceRoot
try {
    dotnet build SigoCalco.Authentication.sln --configuration Release --nologo
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    if ($Action -eq 'Test') {
        dotnet test SigoCalco.Authentication.sln --configuration Release --no-build --no-restore --nologo --logger 'trx;LogFileName=authentication.trx' --results-directory (Join-Path $artifactRoot 'test-results')
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }
}
finally { Pop-Location }
