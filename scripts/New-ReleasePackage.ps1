#requires -Version 5.1
param()
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Use-ProjectTools.ps1')
$project = Join-Path $projectRoot 'dev\PCL-CE\Plain Craft Launcher 2'
$metadata = Get-Content -LiteralPath (Join-Path $project 'metadata.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$version = $metadata.version.base + '-' + $metadata.version.suffix
if ($version -notmatch '^\d+\.\d+\.\d+-[a-zA-Z0-9.-]+$') { throw '预览版本号无效。' }
$releaseRoot = Join-Path $localRoot "releases\v$version-win-x64\$(Get-Date -Format 'yyyyMMdd-HHmmss')"
if (Test-Path -LiteralPath $releaseRoot) { throw "输出目录已存在：$releaseRoot" }
$publishRoot = Join-Path $releaseRoot 'publish'
$packageName = "PigLauncher-$version-win-x64"
$packageRoot = Join-Path $releaseRoot $packageName
New-Item -ItemType Directory -Path $packageRoot -Force | Out-Null
$logPath = Join-Path $releaseRoot 'publish.log'

# 独立中间产物，禁止 Source Generator 将当前终端凭证编译进发布包。
$oldTestRoot = $env:PIGLAUNCHER_TEST_ROOT
$oldWriteSecret = $env:PCL_WRITE_SECRET
try {
    $env:PIGLAUNCHER_TEST_ROOT = $releaseRoot
    $env:PCL_WRITE_SECRET = $null
    & (Join-Path $env:DOTNET_ROOT 'dotnet.exe') publish (Join-Path $project 'Plain Craft Launcher 2.csproj') `
        -c Release -r win-x64 --self-contained true '-p:Platform=x64' `
        '-p:PublishSingleFile=true' '-p:IncludeNativeLibrariesForSelfExtract=true' `
        '-p:NuGetAudit=false' '-p:RestoreIgnoreFailedSources=true' `
        -o $publishRoot --verbosity minimal *> $logPath
    if ($LASTEXITCODE -ne 0) { throw "发布构建失败，见 $logPath" }
}
finally {
    $env:PIGLAUNCHER_TEST_ROOT = $oldTestRoot
    $env:PCL_WRITE_SECRET = $oldWriteSecret
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

# Client ID 是公开应用标识；只复制该字段，不复制本地 CF Key 或账号数据。
$servicesPath = Join-Path $localRoot 'service-config.json'
if (Test-Path -LiteralPath $servicesPath) {
    $services = Get-Content -LiteralPath $servicesPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $clientId = [Guid]::Parse([string]$services.MS_CLIENT_ID)
    if ($clientId -eq [Guid]::Empty) { throw '微软应用标识为空。' }
    @{ MS_CLIENT_ID = $clientId.ToString() } | ConvertTo-Json |
        Set-Content -LiteralPath (Join-Path $packageRoot 'service-config.json') -Encoding UTF8
}

@"
# 猪咪启动器 $version

Windows x64 预览版，解压后运行 PigLauncher.exe。
已包含 .NET 运行时；Minecraft 使用的 Java 需单独准备或由启动器下载。

此草稿不附带 CurseForge Key，CF 搜索及下载需要有效的本地配置。
个人测试可将自己的 service-config.json 放到程序同目录；它不包含在源码仓库中。
Modrinth、实例管理和游戏启动仍可按各自条件使用。

基于 PCL CE v2.15.1（1a7653127d578efdb2a9388999a3fdc67d9e49d5）。
许可、来源和组件声明见 licenses/。这是预览版本，完整功能验收尚未完成。
"@ | Set-Content -LiteralPath (Join-Path $packageRoot 'README.md') -Encoding UTF8

$zipPath = Join-Path $releaseRoot "$packageName.zip"
[IO.Compression.ZipFile]::CreateFromDirectory($packageRoot, $zipPath, [IO.Compression.CompressionLevel]::Optimal, $true)
$hash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  $packageName.zip" | Set-Content -LiteralPath (Join-Path $releaseRoot 'SHA256SUMS.txt') -Encoding ASCII
Write-Output "Package: $zipPath"
Write-Output "SHA256: $hash"
