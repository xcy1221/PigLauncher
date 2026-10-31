$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
. (Join-Path $PSScriptRoot 'Use-ProjectTools.ps1')

$sdkVersion = '10.0.402'
$archiveName = "dotnet-sdk-$sdkVersion-win-x64.zip"
$downloadUrl = "https://builds.dotnet.microsoft.com/dotnet/Sdk/$sdkVersion/$archiveName"
$expectedHash = 'ae4fd528cb7e9f6af6aaf7de36715a69f761f183744f1bb3025d19f5a92447090ec561410e46266c1d10a63781ed6f3de771760bc76c876d87daabc367b898cd'
$dotnetExe = Join-Path $env:DOTNET_ROOT 'dotnet.exe'

if (Test-Path -LiteralPath $dotnetExe) {
    $installedVersion = (& $dotnetExe --version | Out-String).Trim()
    if ($LASTEXITCODE -eq 0 -and $installedVersion -eq $sdkVersion) {
        Write-Output "Project SDK already installed: $installedVersion"
        return
    }
    throw 'Existing project SDK does not match; inspect .local/dotnet before continuing.'
}

$downloadFolder = Join-Path $localRoot 'downloads'
New-Item -ItemType Directory -Path $downloadFolder -Force | Out-Null
$archivePath = Join-Path $downloadFolder $archiveName
if (-not (Test-Path -LiteralPath $archivePath)) {
    Write-Output "Downloading .NET SDK $sdkVersion (win-x64)..."
    Invoke-WebRequest -Uri $downloadUrl -OutFile $archivePath -UseBasicParsing -TimeoutSec 900
}
Write-Output 'Verifying SHA-512...'
$actualHash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA512).Hash
if ($actualHash -ne $expectedHash) {
    throw 'SDK checksum mismatch; extraction cancelled. Inspect the archive before retrying.'
}

if (Test-Path -LiteralPath $env:DOTNET_ROOT) {
    throw 'Project SDK directory already exists without a usable SDK; inspect it before retrying.'
}
Write-Output 'Extracting project SDK...'
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::ExtractToDirectory($archivePath, $env:DOTNET_ROOT)
& $dotnetExe --info
if ($LASTEXITCODE -ne 0) { throw 'Project SDK validation failed.' }

$installation = [ordered]@{
    sdkVersion = $sdkVersion
    architecture = 'win-x64'
    url = $downloadUrl
    sha512 = $actualHash.ToLowerInvariant()
    installedAtUtc = [DateTime]::UtcNow.ToString('o')
}
$installation | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $localRoot 'toolchain.json') -Encoding UTF8
Remove-Item -LiteralPath $archivePath
Write-Output 'Project SDK installed; verified download archive removed.'
