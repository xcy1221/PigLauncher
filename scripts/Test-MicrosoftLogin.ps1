param(
    [switch]$Run,
    [string]$Proxy
)

$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$configPath = Join-Path $projectRoot '.local/service-config.json'
$clientId = [string](Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json).MS_CLIENT_ID
if ([Guid]::Parse($clientId) -eq [Guid]::Empty) { throw 'MS_CLIENT_ID 不能为空。' }
if (-not $Run) {
    Write-Output '配置已就绪；未发送网络请求。实际登录需显式添加 -Run。'
    return
}

Add-Type -AssemblyName System.Net.Http
$handler = [Net.Http.HttpClientHandler]::new()
if ($Proxy) {
    $proxyUri = [Uri]$Proxy
    if ($proxyUri.Scheme -notin @('http', 'https')) { throw '代理地址应使用 http:// 或 https://。' }
    $handler.Proxy = [Net.WebProxy]::new($proxyUri)
    $handler.UseProxy = $true
}
$http = [Net.Http.HttpClient]::new($handler)
$http.Timeout = [TimeSpan]::FromSeconds(30)
$http.DefaultRequestHeaders.UserAgent.ParseAdd('PigLauncher-LoginProbe/0.1')
$oauthBase = 'https://login.microsoftonline.com/consumers/oauth2/v2.0'
$scope = 'XboxLive.signin offline_access'
$report = [ordered]@{
    startedUtc = [DateTime]::UtcNow.ToString('o')
    state = 'running'
    stage = 'initializing'
    clientId = $clientId
    explicitProxy = [bool]$Proxy
    steps = [Collections.Generic.List[object]]::new()
}

function Get-SafeMessage([string]$Value) {
    $safe = $Value -replace '(?i)(Bearer\s+|XBL3\.0\s+)\S+', '[redacted]'
    $safe = $safe -replace '[A-Za-z0-9_.~+/=-]{80,}', '[redacted]'
    return $safe.Substring(0, [Math]::Min(500, $safe.Length))
}

function Invoke-LoginStep {
    param([string]$Stage, [string]$Uri, [hashtable]$Form, [hashtable]$Json,
          [string]$Bearer, [switch]$Polling)
    $report.stage = $Stage
    $method = if ($null -ne $Form -or $null -ne $Json) { 'POST' } else { 'GET' }
    $request = [Net.Http.HttpRequestMessage]::new([Net.Http.HttpMethod]::new($method), $Uri)
    $response = $null
    try {
        if ($null -ne $Form) {
            $fields = [Collections.Generic.Dictionary[string,string]]::new()
            foreach ($key in $Form.Keys) { $fields.Add($key, [string]$Form[$key]) }
            $request.Content = [Net.Http.FormUrlEncodedContent]::new($fields)
        } elseif ($null -ne $Json) {
            $request.Content = [Net.Http.StringContent]::new(
                ($Json | ConvertTo-Json -Depth 8 -Compress), [Text.Encoding]::UTF8, 'application/json')
        }
        if ($Bearer) { $request.Headers.Authorization = [Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer', $Bearer) }
        $response = $http.SendAsync($request).GetAwaiter().GetResult()
        $body = $null
        try { $body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult() | ConvertFrom-Json } catch { }
        if ($Polling -and $response.StatusCode -eq 400 -and $body.error -in @('authorization_pending', 'slow_down')) {
            return $body
        }
        $step = [ordered]@{ stage = $Stage; httpStatus = [int]$response.StatusCode; success = $response.IsSuccessStatusCode }
        if (-not $response.IsSuccessStatusCode -or $body.error) {
            $step.success = $false
            $step.error = Get-SafeMessage ((@($body.error, $body.XErr, $body.error_description, $body.errorMessage, $body.Message) | Where-Object { $_ }) -join ' | ')
            $report.steps.Add($step)
            throw "$Stage 失败：HTTP $($step.httpStatus) $($step.error)"
        }
        if ($null -eq $body) { throw "$Stage 未返回可解析的 JSON。" }
        $report.steps.Add($step)
        Write-Host "$Stage : HTTP $($step.httpStatus)"
        return $body
    } finally {
        if ($null -ne $response) { $response.Dispose() }
        $request.Dispose()
    }
}

try {
    $device = Invoke-LoginStep -Stage 'device-code' -Uri "$oauthBase/devicecode" -Form @{ client_id = $clientId; scope = $scope }
    if (-not $device.device_code -or -not $device.user_code -or -not $device.verification_uri) { throw '设备授权响应缺少必要字段。' }
    $deadline = [DateTime]::UtcNow.AddSeconds([int]$device.expires_in)
    $interval = [Math]::Max(5, [int]$device.interval)
    Write-Host "授权页面：$($device.verification_uri)"
    Write-Host "设备码：$($device.user_code)；有效期 $($device.expires_in) 秒。请确认应用名称为 PigLauncher。"
    $oauth = $null
    while ([DateTime]::UtcNow -lt $deadline) {
        Start-Sleep -Seconds $interval
        $reply = Invoke-LoginStep -Stage 'microsoft-token' -Uri "$oauthBase/token" -Polling -Form @{
            client_id = $clientId; grant_type = 'urn:ietf:params:oauth:grant-type:device_code'; device_code = $device.device_code
        }
        if ($reply.error -eq 'authorization_pending') { continue }
        if ($reply.error -eq 'slow_down') { $interval += 5; continue }
        $oauth = $reply
        break
    }
    if (-not $oauth.access_token) { throw '设备授权已过期或未返回令牌。' }
    $report.refreshTokenReturned = [bool]$oauth.refresh_token
    $xbl = Invoke-LoginStep -Stage 'xbox-live' -Uri 'https://user.auth.xboxlive.com/user/authenticate' -Json @{
        Properties = @{ AuthMethod = 'RPS'; SiteName = 'user.auth.xboxlive.com'; RpsTicket = "d=$($oauth.access_token)" }
        RelyingParty = 'http://auth.xboxlive.com'; TokenType = 'JWT'
    }
    if (-not $xbl.Token) { throw 'Xbox Live 未返回令牌。' }
    $xsts = Invoke-LoginStep -Stage 'xsts' -Uri 'https://xsts.auth.xboxlive.com/xsts/authorize' -Json @{
        Properties = @{ SandboxId = 'RETAIL'; UserTokens = @($xbl.Token) }
        RelyingParty = 'rp://api.minecraftservices.com/'; TokenType = 'JWT'
    }
    if (-not $xsts.Token -or -not $xsts.DisplayClaims.xui[0].uhs) { throw 'XSTS 未返回令牌或用户哈希。' }
    $minecraft = Invoke-LoginStep -Stage 'minecraft-token' -Uri 'https://api.minecraftservices.com/authentication/login_with_xbox' -Json @{
        identityToken = "XBL3.0 x=$($xsts.DisplayClaims.xui[0].uhs);$($xsts.Token)"
    }
    if (-not $minecraft.access_token) { throw 'Minecraft 未返回令牌。' }
    $entitlements = Invoke-LoginStep -Stage 'entitlements' -Uri 'https://api.minecraftservices.com/entitlements/mcstore' -Bearer $minecraft.access_token
    $report.javaEntitled = @($entitlements.items | Where-Object { $_.name -in @('product_minecraft', 'game_minecraft') }).Count -gt 0
    $profile = Invoke-LoginStep -Stage 'profile' -Uri 'https://api.minecraftservices.com/minecraft/profile' -Bearer $minecraft.access_token
    $report.profileFound = [bool]($profile.id -and $profile.name)
    if (-not $report.javaEntitled -or -not $report.profileFound) { throw '已取得 Minecraft 令牌，但所有权或角色检查未通过。' }
    $report.state = 'passed'
    Write-Host "首次登录链路通过；角色：$($profile.name)。尚未测试刷新令牌或实际游戏启动。"
} catch {
    $report.state = 'failed'
    $report.error = Get-SafeMessage $_.Exception.Message
    Write-Host "测试停止：$($report.error)"
} finally {
    $report.finishedUtc = [DateTime]::UtcNow.ToString('o')
    $reportDirectory = Join-Path $projectRoot '.local/login-probe'
    New-Item -ItemType Directory -Path $reportDirectory -Force | Out-Null
    $reportPath = Join-Path $reportDirectory ("result-{0}.json" -f [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff'))
    $report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $reportPath -Encoding utf8
    $device = $oauth = $reply = $xbl = $xsts = $minecraft = $entitlements = $profile = $null
    $http.Dispose()
    Write-Host "脱敏报告：$reportPath"
}
if ($report.state -ne 'passed') { exit 1 }
