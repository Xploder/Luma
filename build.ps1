param(
    [switch]$Publish,
    [string]$OutputDirectory = (Join-Path $PSScriptRoot 'dist\Luma-Windows-x64'),
    [string]$DotnetPath = 'dotnet'
)
$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
if (-not $env:LumaBuildRoot) { $env:LumaBuildRoot = Join-Path $PSScriptRoot '.build' }
$env:DOTNET_CLI_HOME = Join-Path $env:LumaBuildRoot 'dotnet-home'
$env:NUGET_HTTP_CACHE_PATH = Join-Path $env:LumaBuildRoot 'nuget-http'
$env:NUGET_PLUGINS_CACHE_PATH = Join-Path $env:LumaBuildRoot 'nuget-plugins'
$env:MSBuildEnableWorkloadResolver = 'false'
$env:TEMP = Join-Path $env:LumaBuildRoot 'tmp'
$env:TMP = $env:TEMP
New-Item -ItemType Directory -Force $env:TEMP | Out-Null
if ($Publish) {
    & $DotnetPath publish "$PSScriptRoot\Luma.csproj" -c Release -o $OutputDirectory --self-contained true
} else {
    & $DotnetPath build "$PSScriptRoot\Luma.csproj" -c Release
}
exit $LASTEXITCODE
