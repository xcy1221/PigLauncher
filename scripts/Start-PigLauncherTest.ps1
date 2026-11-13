#requires -Version 5.1
param([string]$RunId = (Get-Date -Format 'yyyyMMdd-HHmmss'))
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$testsRoot = Join-Path $projectRoot '.local\functional-tests'
if ($RunId -notmatch '^[a-zA-Z0-9_-]+$') { throw '运行编号只能包含字母、数字、下划线和连字符。' }
$runRoot = [IO.Path]::GetFullPath((Join-Path $testsRoot $RunId))
$distribution = Join-Path $projectRoot 'dev\PigLauncher'
if (!(Test-Path -LiteralPath (Join-Path $distribution 'PigLauncher.exe'))) { throw '缺少 dev\PigLauncher 构建。' }
if (Test-Path -LiteralPath $runRoot) { throw '运行目录已存在，请使用新的编号。' }
New-Item -ItemType Directory -Path $runRoot | Out-Null
$evidence = New-Item -ItemType Directory -Path (Join-Path $runRoot 'evidence')

function Get-ProtectedState {
    $gameParent = [IO.Path]::GetFullPath((Join-Path $projectRoot '..'))
    $protectedPaths = @(
        (Join-Path $gameParent '.minecraft'), (Join-Path $gameParent 'PCL'),
        "$env:APPDATA\PCL", "$env:APPDATA\PCLCE", "$env:APPDATA\PCLCE_Debug",
        "$env:APPDATA\.PCLCE", "$env:APPDATA\.PCLCEDebug",
        "$env:APPDATA\PigLauncher", "$env:LOCALAPPDATA\PigLauncher"
    )
    $localPathsFile = Join-Path $projectRoot '.local/protected-test-paths.json'
    if (Test-Path -LiteralPath $localPathsFile) {
        $protectedPaths += @(Get-Content -LiteralPath $localPathsFile -Raw | ConvertFrom-Json)
    }
    $files = foreach ($path in $protectedPaths) {
        [pscustomobject]@{
            Path = $path; Exists = (Test-Path -LiteralPath $path)
            Files = @(if (Test-Path -LiteralPath $path) {
                Get-ChildItem -LiteralPath $path -File -Recurse -Force |
                    Sort-Object FullName | Select-Object FullName,Length,@{n='Ticks';e={$_.LastWriteTimeUtc.Ticks}}
            })
        }
    }
    $proxy = Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Internet Settings'
    [ordered]@{
        Files = @($files); Proxy = @($proxy.ProxyEnable, $proxy.ProxyServer)
        PATH = $env:PATH; JAVA_HOME = $env:JAVA_HOME
        AumidExists = (Test-Path 'HKCU:\Software\Classes\AppUserModelId\xcy1221.PigLauncher')
    } | ConvertTo-Json -Depth 7
}

$before = Get-ProtectedState
$before | Set-Content (Join-Path $evidence 'before.json')
try {
    foreach ($name in @('app','temp','config')) { New-Item -ItemType Directory -Path (Join-Path $runRoot $name) | Out-Null }
    Copy-Item -Path (Join-Path $distribution '*') -Destination (Join-Path $runRoot 'app') -Recurse
    $serviceConfigPath = Join-Path $projectRoot '.local\service-config.json'
    if (Test-Path -LiteralPath $serviceConfigPath) {
        $services = [IO.File]::ReadAllText($serviceConfigPath) | ConvertFrom-Json
        $clientId = [string]$services.MS_CLIENT_ID
        if ([Guid]::Parse($clientId) -eq [Guid]::Empty) { throw 'MS_CLIENT_ID 不能为空。' }
        $testServices = @{ MS_CLIENT_ID = $clientId }
        if ($null -ne $services.CURSEFORGE_API_KEY) {
            $cfKey = [string]$services.CURSEFORGE_API_KEY
            if ($cfKey.Trim() -notmatch '^[\x21-\x7E]+$') { throw 'CURSEFORGE_API_KEY 格式无效。' }
            $testServices.CURSEFORGE_API_KEY = $cfKey.Trim()
        }
        [IO.File]::WriteAllText((Join-Path $runRoot 'app\service-config.json'),
            ($testServices | ConvertTo-Json), [Text.UTF8Encoding]::new($false))
    }
    # 独立测试配置；不读取真实账号、配置或现有实例。
    $testConfig = @{ SystemEula = $true; SystemTelemetry = $false; SystemNetEnableDoH = $false } | ConvertTo-Json
    [IO.File]::WriteAllText((Join-Path $runRoot 'config\config.v1.json'), $testConfig, [Text.UTF8Encoding]::new($false))
    $start = [Diagnostics.ProcessStartInfo]::new()
    $start.FileName = Join-Path $runRoot 'app\PigLauncher.exe'
    $start.WorkingDirectory = Join-Path $runRoot 'app'
    $start.UseShellExecute = $false
    $start.EnvironmentVariables['PIGLAUNCHER_TEST_ROOT'] = $runRoot
    $start.EnvironmentVariables['DOTNET_ROOT'] = Join-Path $projectRoot '.local\dotnet'
    $start.EnvironmentVariables['TEMP'] = Join-Path $runRoot 'temp'
    $start.EnvironmentVariables['TMP'] = Join-Path $runRoot 'temp'
    $networkConfigPath = Join-Path $projectRoot '.local\network-config.json'
    if (Test-Path -LiteralPath $networkConfigPath) {
        $network = [IO.File]::ReadAllText($networkConfigPath) | ConvertFrom-Json
        $start.EnvironmentVariables['PIGLAUNCHER_TEST_PROXY'] = $network.proxy
        $start.EnvironmentVariables['PIGLAUNCHER_TEST_PROXY_BYPASS'] = $network.bypass -join ';'
    }
    $javaHome = Join-Path $projectRoot '.local\java\25'
    if (Test-Path -LiteralPath (Join-Path $javaHome 'bin\java.exe')) {
        $start.EnvironmentVariables['JAVA_HOME'] = $javaHome
        $start.EnvironmentVariables['PATH'] = (Join-Path $javaHome 'bin') + ';' + $start.EnvironmentVariables['PATH']
    }
    foreach ($key in @('PCL_PATH','PCL_PATH_SHARED','PCL_PATH_LOCAL','PCL_PATH_TEMP')) { $start.EnvironmentVariables.Remove($key) }
    Write-Host "测试目录：$runRoot；关闭启动器后自动清理本轮数据，保留 evidence。"
    $process = [Diagnostics.Process]::Start($start)
    $process.Id | Set-Content (Join-Path $evidence 'launcher.pid')
    $process.WaitForExit()
    $process.ExitCode | Set-Content (Join-Path $evidence 'exit-code.txt')
}
finally {
    if ($process -and !$process.HasExited) { $process.Kill(); $process.WaitForExit() }
    $logs = Join-Path $runRoot 'app\PCL\Log'
    if (Test-Path -LiteralPath $logs) { Copy-Item -LiteralPath $logs -Destination (Join-Path $evidence 'launcher-logs') -Recurse }
    $removed = @()
    foreach ($item in Get-ChildItem -LiteralPath $runRoot -Force | Where-Object Name -ne 'evidence') {
        $absolute = [IO.Path]::GetFullPath($item.FullName)
        if (![string]::Equals([IO.Path]::GetDirectoryName($absolute), $runRoot, [StringComparison]::OrdinalIgnoreCase)) { throw '清理路径越界。' }
        $removed += $absolute
        Remove-Item -LiteralPath $absolute -Recurse -Force
    }
    $removed | Set-Content (Join-Path $evidence 'cleaned-paths.txt')
    $after = Get-ProtectedState
    $after | Set-Content (Join-Path $evidence 'after.json')
    @{ ProtectedStateUnchanged = ($before -ceq $after); Remaining = @(Get-ChildItem -LiteralPath $runRoot | Select-Object -ExpandProperty Name) } |
        ConvertTo-Json | Set-Content (Join-Path $evidence 'cleanup.json')
    if ($before -cne $after) { Write-Warning '保护目录或设置有变化，详见 before.json / after.json；脚本不会覆盖真实用户数据。' }
}
