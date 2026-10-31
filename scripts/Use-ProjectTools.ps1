$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$localRoot = Join-Path $projectRoot '.local'
$env:DOTNET_ROOT = Join-Path $localRoot 'dotnet'
$env:DOTNET_ROOT_X64 = $env:DOTNET_ROOT
$env:DOTNET_CLI_HOME = Join-Path $localRoot 'dotnet-home'
$env:NUGET_PACKAGES = Join-Path $localRoot 'nuget\packages'
$env:NUGET_HTTP_CACHE_PATH = Join-Path $localRoot 'nuget\http-cache'
$env:NUGET_PLUGINS_CACHE_PATH = Join-Path $localRoot 'nuget\plugins-cache'
$env:NUGET_SCRATCH = Join-Path $localRoot 'nuget\scratch'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_NOLOGO = '1'
$env:DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = 'false'
if (($env:PATH -split ';') -notcontains $env:DOTNET_ROOT) {
    $env:PATH = "$env:DOTNET_ROOT;$env:PATH"
}
