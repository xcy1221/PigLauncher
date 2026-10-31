#requires -Version 5.1
param(
    [string]$ProjectDirectory = (Join-Path $PSScriptRoot '..\dev\PCL-CE\Plain Craft Launcher 2'),
    [string]$ExpectedTag = ''
)
$ErrorActionPreference = 'Stop'
$metadata = Get-Content -LiteralPath (Join-Path $ProjectDirectory 'metadata.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$baseVersion = [string]$metadata.version.base
$suffix = [string]$metadata.version.suffix
if ($baseVersion -cnotmatch '^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$') {
    throw '基础版本必须为无前导零的 主版本.次版本.修订号。'
}
if ($suffix -ne '' -and $suffix -cnotmatch '^preview\.[1-9][0-9]*$') {
    throw '版本后缀必须为空或 preview.N（N 从 1 开始且无前导零）。'
}
$isPrerelease = $suffix -ne ''
$version = if ($isPrerelease) { "$baseVersion-$suffix" } else { $baseVersion }
if ($ExpectedTag -and $ExpectedTag -cne "v$version") { throw '标签与源码版本不一致。' }

[xml]$project = Get-Content -LiteralPath (Join-Path $ProjectDirectory 'Plain Craft Launcher 2.csproj') -Raw -Encoding UTF8
foreach ($field in @('Version', 'AssemblyVersion', 'FileVersion')) {
    $expected = if ($field -eq 'Version') { $version } else { "$baseVersion.0" }
    $declared = $project.SelectSingleNode("/Project/PropertyGroup/$field")
    if (!$declared -or $declared.InnerText -cne $expected) {
        throw "csproj 的 $field 必须与 metadata.json 一致，应为 $expected。"
    }
}
[pscustomobject]@{
    Version = $version
    BaseVersion = $baseVersion
    IsPrerelease = $isPrerelease
    Tag = "v$version"
}
