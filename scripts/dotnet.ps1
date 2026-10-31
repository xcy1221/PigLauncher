. (Join-Path $PSScriptRoot 'Use-ProjectTools.ps1')
$dotnetExe = Join-Path $env:DOTNET_ROOT 'dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnetExe)) {
    throw 'Run scripts/Install-ProjectTools.ps1 first.'
}
& $dotnetExe @args
exit $LASTEXITCODE
