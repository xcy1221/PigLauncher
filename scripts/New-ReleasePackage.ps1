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
$releaseKind = if ($releaseVersion.IsPrerelease) { '预览版' } else { '稳定版' }
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
$packageName = "PigLauncher-$version-win-x64"
$packageRoot = Join-Path $releaseRoot $packageName
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

Get-ChildItem -LiteralPath $publishRoot -Force | Where-Object Extension -ne '.pdb' |
    Copy-Item -Destination $packageRoot -Recurse
$originalExe = Join-Path $packageRoot 'Plain Craft Launcher 2.exe'
if (!(Test-Path -LiteralPath $originalExe)) { throw '发布输出缺少主程序。' }
Rename-Item -LiteralPath $originalExe -NewName 'PigLauncher.exe'

$licensesRoot = Join-Path $packageRoot 'licenses'
New-Item -ItemType Directory -Path $licensesRoot | Out-Null
Copy-Item -LiteralPath (Join-Path $projectRoot 'dev\PCL-CE\LICENSE') -Destination (Join-Path $licensesRoot 'Apache-2.0.txt')
Copy-Item -LiteralPath (Join-Path $project 'LICENCE') -Destination (Join-Path $licensesRoot 'PCL-LICENCE.txt')
Copy-Item -LiteralPath (Join-Path $projectRoot 'dev\PCL-CE\PCL.Core\NOTICE') -Destination (Join-Path $licensesRoot 'PCL.Core-NOTICE.txt')
Copy-Item -LiteralPath (Join-Path $projectRoot 'dev\PCL-CE\PCL.Core\UI\Assets\IconPacks\lucide\LICENSE.txt') -Destination (Join-Path $licensesRoot 'Lucide-LICENSE.txt')
Copy-Item -LiteralPath (Join-Path $project 'Images\Pig\SOURCE.md') -Destination (Join-Path $licensesRoot 'Minecraft-Pig-SOURCE.md')
Copy-Item -LiteralPath (Join-Path $project 'metadata.json') -Destination (Join-Path $licensesRoot 'components.json')

# 收录本次实际解析的包声明及随包许可，避免只附一份上游 NOTICE。
$assetsPath = Join-Path $releaseRoot 'build\Plain Craft Launcher 2\obj\project.assets.json'
$assets = Get-Content -LiteralPath $assetsPath -Raw -Encoding UTF8 | ConvertFrom-Json
$packages = @{}
foreach ($entry in $assets.libraries.PSObject.Properties | Where-Object { $_.Value.type -eq 'package' }) {
    $packages[$entry.Name] = $entry.Value.path
}
foreach ($framework in $assets.project.frameworks.PSObject.Properties) {
    foreach ($dependency in $framework.Value.downloadDependencies) {
        $packageVersion = ($dependency.version.Trim('[', ']') -split ',')[0].Trim()
        $packageId = $dependency.name + '/' + $packageVersion
        $packages[$packageId] = $packageId.ToLowerInvariant()
    }
}
$components = foreach ($entry in $packages.GetEnumerator() | Sort-Object Key) {
    $packagePath = Join-Path $env:NUGET_PACKAGES $entry.Value
    $target = Join-Path $licensesRoot ('nuget\' + ($entry.Key -replace '/', '-'))
    New-Item -ItemType Directory -Path $target -Force | Out-Null
    $nuspec = Get-ChildItem -LiteralPath $packagePath -Filter '*.nuspec' -File | Select-Object -First 1
    [xml]$spec = Get-Content -LiteralPath $nuspec.FullName -Raw -Encoding UTF8
    $info = $spec.package.metadata
    Copy-Item -LiteralPath $nuspec.FullName -Destination $target
    foreach ($file in Get-ChildItem -LiteralPath $packagePath -Recurse -File | Where-Object {
        $_.Name -match '^(licen[cs]e|copying|notice|copyright|third[-_. ]?party|OFL)(\b|[-_. ])'
    }) {
        $relative = $file.FullName.Substring($packagePath.Length).TrimStart('\', '/')
        $destination = Join-Path $target $relative
        New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
        Copy-Item -LiteralPath $file.FullName -Destination $destination
    }
    if ($info.license.type -eq 'file') {
        $licenseFile = Join-Path $packagePath $info.license.InnerText
        if (!(Test-Path -LiteralPath $licenseFile)) { throw "缺少 $($entry.Key) 的许可文件。" }
        Copy-Item -LiteralPath $licenseFile -Destination $target -Force
    }
    [pscustomobject]@{ Package = $entry.Key; License = $info.license.InnerText; Copyright = [string]$info.copyright; Project = [string]$info.projectUrl }
}
$components | Export-Csv -LiteralPath (Join-Path $licensesRoot 'nuget-components.csv') -NoTypeInformation -Encoding UTF8

Add-Type -AssemblyName System.IO.Compression.FileSystem
foreach ($jar in Get-ChildItem -LiteralPath (Join-Path $project 'Resources') -Filter '*.jar' -File) {
    $archive = [IO.Compression.ZipFile]::OpenRead($jar.FullName)
    try {
        foreach ($entry in $archive.Entries | Where-Object { $_.FullName -match '(^|/)(LICENSE|LICENCE|COPYING|NOTICE|COPYRIGHT)(\.[^/]*)?$' }) {
            $target = Join-Path $licensesRoot ('embedded\' + $jar.BaseName)
            New-Item -ItemType Directory -Path $target -Force | Out-Null
            $destination = Join-Path $target ($entry.FullName -replace '[^a-zA-Z0-9._-]', '_')
            [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $destination, $true)
        }
    }
    finally { $archive.Dispose() }
}

# 非凭证构建只复制公开的 Client ID；凭证构建由程序内置配置提供默认值。
$servicesPath = Join-Path $localRoot 'service-config.json'
if (!$EmbedServiceCredentials -and (Test-Path -LiteralPath $servicesPath)) {
    $services = Get-Content -LiteralPath $servicesPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $clientId = [Guid]::Parse([string]$services.MS_CLIENT_ID)
    if ($clientId -eq [Guid]::Empty) { throw '微软应用标识为空。' }
    @{ MS_CLIENT_ID = $clientId.ToString() } | ConvertTo-Json |
        Set-Content -LiteralPath (Join-Path $packageRoot 'service-config.json') -Encoding UTF8
}

$serviceDescription = if ($EmbedServiceCredentials) {
    '本包已通过 GitHub Secrets 内嵌项目的微软应用标识与 CurseForge Key，可直接使用对应服务。本地 service-config.json 仍可覆盖内置配置。'
} else {
    '本包不附带 CurseForge Key。个人测试可在程序同目录放入自己的 service-config.json；缺少 Key 时 CF 功能不可用。'
}
[ordered]@{
    version = $version
    isPrerelease = $releaseVersion.IsPrerelease
    sourceCommit = $sourceCommit
    runtime = 'win-x64'
    selfContained = $true
    serviceCredentialsEmbedded = [bool]$EmbedServiceCredentials
    workflowRun = if ($env:GITHUB_ACTIONS -eq 'true') { "$env:GITHUB_SERVER_URL/$env:GITHUB_REPOSITORY/actions/runs/$env:GITHUB_RUN_ID" } else { $null }
} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $packageRoot 'build-info.json') -Encoding UTF8

@"
# 猪咪启动器 $version

Windows x64 $releaseKind，解压后运行 PigLauncher.exe。
已包含 .NET 运行时；Minecraft 使用的 Java 需单独准备或由启动器下载。

$serviceDescription
Modrinth、实例管理和游戏启动仍可按各自条件使用。

基于 PCL CE v2.15.1（1a7653127d578efdb2a9388999a3fdc67d9e49d5）。
许可、来源和组件声明见 licenses/；具体功能验收记录见项目文档。
"@ | Set-Content -LiteralPath (Join-Path $packageRoot 'README.md') -Encoding UTF8

$zipPath = Join-Path $releaseRoot "$packageName.zip"
[IO.Compression.ZipFile]::CreateFromDirectory($packageRoot, $zipPath, [IO.Compression.CompressionLevel]::Optimal, $true)
$hash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  $packageName.zip" | Set-Content -LiteralPath (Join-Path $releaseRoot 'SHA256SUMS.txt') -Encoding ASCII
Write-Output "Package: $zipPath"
Write-Output "SHA256: $hash"
if ($env:GITHUB_ACTIONS -eq 'true' -and $env:GITHUB_OUTPUT) {
    $outputs = "zip_path=$zipPath`nchecksum_path=$(Join-Path $releaseRoot 'SHA256SUMS.txt')`nversion=$version`n"
    [IO.File]::AppendAllText($env:GITHUB_OUTPUT, $outputs, [Text.UTF8Encoding]::new($false))
}
