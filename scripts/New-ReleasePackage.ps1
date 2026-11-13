#requires -Version 5.1
param(
    [switch]$UseSystemDotnet,
    [switch]$EmbedServiceCredentials,
    [string]$ExpectedTag = ''
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Use-ProjectTools.ps1') -UseSystemDotnet:$UseSystemDotnet
$dotnetExe = (Get-Command dotnet -ErrorAction Stop).Source
$project = Join-Path $projectRoot 'dev\PCL-CE\Plain Craft Launcher 2'
$releaseVersion = & (Join-Path $PSScriptRoot 'Get-ReleaseVersion.ps1') -ProjectDirectory $project -ExpectedTag $ExpectedTag
$version = $releaseVersion.Version
$sourceCommit = (& git -C $projectRoot rev-parse HEAD | Out-String).Trim()
if ($LASTEXITCODE -ne 0) { throw '无法读取源码提交。' }
$embeddedClientId = ([string]$env:PCL_MS_CLIENT_ID).Trim()
$embeddedCfKey = ([string]$env:PCL_CURSEFORGE_API_KEY).Trim()
if ($EmbedServiceCredentials) {
    $parsedClientId = [Guid]::Empty
    if ([string]::IsNullOrWhiteSpace($embeddedClientId) -or
        ![Guid]::TryParse($embeddedClientId, [ref]$parsedClientId) -or $parsedClientId -eq [Guid]::Empty) {
        throw '缺少有效的 MS_CLIENT_ID Secret。'
    }
    if ([string]::IsNullOrWhiteSpace($embeddedCfKey) -or $embeddedCfKey -notmatch '^[\x21-\x7E]+$') {
        throw '缺少有效的 CURSEFORGE_API_KEY Secret。'
    }
}
$releaseRoot = Join-Path $localRoot "releases\v$version-win-x64\$(Get-Date -Format 'yyyyMMdd-HHmmss')"
if (Test-Path -LiteralPath $releaseRoot) { throw "输出目录已存在：$releaseRoot" }
$publishRoot = Join-Path $releaseRoot 'publish'
$packageRoot = Join-Path $releaseRoot 'release'
New-Item -ItemType Directory -Path $packageRoot -Force | Out-Null
$logPath = Join-Path $releaseRoot 'publish.log'

# 独立中间产物；凭证构建只允许指定的两个服务标识和源码提交。
$oldTestRoot = $env:PIGLAUNCHER_TEST_ROOT
$oldPclEnvironment = @{}
Get-ChildItem Env:PCL_* | ForEach-Object { $oldPclEnvironment[$_.Name] = $_.Value }
try {
    $env:PIGLAUNCHER_TEST_ROOT = $releaseRoot
    foreach ($name in $oldPclEnvironment.Keys) { [Environment]::SetEnvironmentVariable($name, $null, 'Process') }
    if ($EmbedServiceCredentials) {
        $env:PCL_WRITE_SECRET = '1'
        $env:PCL_MS_CLIENT_ID = $embeddedClientId
        $env:PCL_CURSEFORGE_API_KEY = $embeddedCfKey
        $env:PCL_GITHUB_SHA = $sourceCommit
    }
    & $dotnetExe publish (Join-Path $project 'Plain Craft Launcher 2.csproj') `
        -c Release -r win-x64 --self-contained true '-p:Platform=x64' `
        '-p:PublishSingleFile=true' '-p:IncludeNativeLibrariesForSelfExtract=true' `
        '-p:UseSharedCompilation=false' `
        '-p:NuGetAudit=false' '-p:RestoreIgnoreFailedSources=true' `
        -o $publishRoot --verbosity minimal *> $logPath
    if ($LASTEXITCODE -ne 0) {
        Get-Content -LiteralPath $logPath -Tail 60
        throw "发布构建失败，见 $logPath"
    }
}
finally {
    $env:PIGLAUNCHER_TEST_ROOT = $oldTestRoot
    Get-ChildItem Env:PCL_* | ForEach-Object { [Environment]::SetEnvironmentVariable($_.Name, $null, 'Process') }
    foreach ($name in $oldPclEnvironment.Keys) { [Environment]::SetEnvironmentVariable($name, $oldPclEnvironment[$name], 'Process') }
}

if ($EmbedServiceCredentials) {
    $corePath = Join-Path $project 'bin\Release\net10.0-windows\win-x64\PCL.Core.dll'
    $coreHex = [BitConverter]::ToString([IO.File]::ReadAllBytes($corePath))
    foreach ($value in @($embeddedCfKey, $embeddedClientId, $sourceCommit)) {
        if (!$coreHex.Contains([BitConverter]::ToString([Text.Encoding]::Unicode.GetBytes($value)))) {
            throw '编译结果缺少要求内嵌的服务配置或源码提交。'
        }
    }
    Write-Output 'Embedded service configuration verified.'
}

$originalExe = Join-Path $publishRoot 'Plain Craft Launcher 2.exe'
if (!(Test-Path -LiteralPath $originalExe -PathType Leaf)) { throw '发布输出缺少主程序。' }
$extraFiles = @(Get-ChildItem -LiteralPath $publishRoot -Recurse -File | Where-Object {
    $_.FullName -ne $originalExe -and $_.Extension -ne '.pdb'
})
if ($extraFiles.Count) { throw "发布输出含有未合并进 EXE 的文件：$($extraFiles.Name -join ', ')" }
$exePath = Join-Path $packageRoot 'PigLauncher.exe'
Copy-Item -LiteralPath $originalExe -Destination $exePath
$checksumPath = Join-Path $packageRoot 'SHA256SUMS.txt'
$hash = (Get-FileHash -LiteralPath $exePath -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  PigLauncher.exe" | Set-Content -LiteralPath $checksumPath -Encoding ASCII
Write-Output "Executable: $exePath"
Write-Output "SHA256: $hash"
if ($env:GITHUB_ACTIONS -eq 'true' -and $env:GITHUB_OUTPUT) {
    $outputs = "exe_path=$exePath`nchecksum_path=$checksumPath`nversion=$version`n"
    [IO.File]::AppendAllText($env:GITHUB_OUTPUT, $outputs, [Text.UTF8Encoding]::new($false))
}
