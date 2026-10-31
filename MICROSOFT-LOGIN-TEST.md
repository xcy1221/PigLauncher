# Microsoft Client ID 登录验证

更新：2026-10-26。使用原 Client ID 重测，设备码至 Minecraft 所有权、角色读取全部 HTTP 200；历史 403 已消失。Client ID 已接入启动器，界面登录、账号恢复、刷新和实际启动另行验收。

脚本：`scripts/Test-MicrosoftLogin.ps1`。读取 `.local/service-config.json` 中已记录的 Client ID，使用与审查基线相同的消费者端点和 `XboxLive.signin offline_access` 权限范围。

## 运行方式

2026-10-26 脱敏报告：`.local/login-probe/result-20261026-082358-125.json`。本次未显式指定代理；令牌仅保留在测试进程内存。Release 从程序目录 `service-config.json` 读取 `MS_CLIENT_ID`，默认 MicrosoftProvider 已实测取得设备码；配置、白名单及联网检查共 20 项通过，证据位于 `.local/functional-tests/20261026-ms-client/evidence/`。用户提供的审核邮件及本轮结果覆盖后文历史阻塞结论。

```powershell
# 默认仅检查本地配置，不发送网络请求。
.\scripts\Test-MicrosoftLogin.ps1

# 显式发起设备码授权和后续登录请求。
.\scripts\Test-MicrosoftLogin.ps1 -Run

# 如需明确指定代理，以实际地址替换示例。
.\scripts\Test-MicrosoftLogin.ps1 -Run -Proxy 'http://127.0.0.1:端口'
```

实际执行时，脚本显示微软授权地址和设备码，由用户在浏览器中确认 `PigLauncher` 并登录。随后按服务返回的间隔轮询，遇到拒绝、过期或不可恢复错误即停止。

## 检查范围

设备码 → 微软令牌 → Xbox Live → XSTS → Minecraft 令牌 → Java 所有权 → 角色信息。记录每一步 HTTP 状态，定位 Client ID、账户条件、网络或 Minecraft 服务准入问题。

访问及刷新令牌只留在本次脚本内存中，不打印、不落盘；`.local/login-probe/` 仅保存阶段状态、脱敏错误和检查结果。默认使用系统网络设置，显式代理参数只影响本次请求。

此脚本验证首次登录链路，不修改上游源码；刷新令牌有效性、启动器账号持久化、游戏及正版服务器登录另行验收。

## 首轮实测（2026-10-23，UTC+8）

| 时间 | 路径 | 结果 |
|---|---|---|
| 09:48 | 沙箱内设备码请求 | TLS 失败；进一步诊断为“安全包中没有可用的凭证”，尚未进入应用校验 |
| 09:49 | 沙箱外 HTTPS 与设备码请求 | 微软 HTTPS 返回 200；设备码请求返回 HTTP 401 / `invalid_client` / `AADSTS70002`，提示 `The client application must be marked as 'mobile.'` |

首轮没有取得设备码，未进行用户授权、Xbox 或 Minecraft 调用；该失败不能判断 Minecraft 服务是否放行该 Client ID。

用户随后提供设置截图：“允许公共客户端流”和“启用 Live SDK 支持”均为已启用，保存按钮为灰色。用户确认昨日已设置，不能再将“忘记打开开关”当作结论。证据：`.local/login-probe/public-client-enabled-20261023.png`。

## 对照排查（2026-10-23 10:14，UTC+8）

| 请求 | 结果 | 判断 |
|---|---|---|
| `/common/devicecode`，`XboxLive.signin offline_access` | HTTP 400 / `AADSTS9002346`，服务返回应用名 PigLauncher，并明确该应用仅支持 Microsoft Account 用户，要求使用 `/consumers` | 请求中的 Client ID 与应用匹配；账户类型已确认，原消费者端点正确 |
| `/consumers/devicecode`，`openid profile offline_access` | HTTP 401 / `AADSTS70002`，仍提示应用需标记为 mobile | 失败不局限于 Xbox 权限范围，尚未到 Minecraft 服务阶段 |

以上仅申请设备码用于定位，均未取得设备码，没有轮询或账号令牌。报告：`.local/login-probe/comparison-20261023-021446.json`。

用户提供的清单已确认：`appId` 与本地一致、`isFallbackPublicClient: true`、`signInAudience: PersonalMicrosoftAccount`、`publicClient.redirectUris: []`。原始摘录位于 `.local/login-probe/manifest-excerpt-20261023.json`。开关和账户类型均不能作为已发现的配置错误；设备码协议本身不需要重定向 URI，不能仅凭空列表认定失败原因。

排查操作：建议在认证页添加“移动和桌面应用程序”平台，使用 `https://login.microsoftonline.com/common/oauth2/nativeclient`。用户反馈已添加并保存；保持 `/consumers`、Client ID 和权限范围不变复测，设备码请求返回 HTTP 200。此前 `AADSTS70002` 在本次复测中已消失。

## 授权后结果（2026-10-23 10:39–10:42，UTC+8）

用户在微软官方页面完成 `PigLauncher` 授权后，脚本自动继续：

| 阶段 | HTTP 状态 | 结论 |
|---|---|---|
| 设备码申请 | 200 | 通过 |
| 微软令牌 | 200 | 通过，返回刷新令牌；未单独验证刷新 |
| Xbox Live | 200 | 通过 |
| XSTS | 200 | 通过 |
| Minecraft 令牌 | 403 | `Invalid app registration, see https://aka.ms/AppRegInfo for more information` |
| Java 所有权、角色信息 | 未请求 | 因上一步失败而停止，不能据此判断账号是否拥有游戏 |

报告：`.local/login-probe/result-20261023-024209-103.json`。令牌未输出或保存，进程已结束；本次验证说明 Client ID 可完成微软与 Xbox 授权，但未通过 Minecraft 服务端应用准入，不能宣称启动器正版登录成功。

错误给出的 `https://aka.ms/AppRegInfo` 实测 301 跳转至 `https://help.minecraft.net/hc/en-us/articles/16254801392141`。此前文本抓取仅得到站点框架；本轮已用浏览器读取正文，官方明确要求新应用人工审核加入允许名单，并链接到有效的 `https://aka.ms/mce-reviewappid` 申请表。当前只读核验，未提交申请或联系支持。

## 网络与服务端拒绝的区分（2026-10-23 10:50，UTC+8）

| 不带账号令牌的请求 | HTTP 状态 | 耗时 |
|---|---|---|
| 默认网络 GET `/publickeys` | 200，JSON，返回公钥 | 623 ms |
| 强制直连 GET `/publickeys` | 200，JSON，返回公钥 | 343 ms |
| 强制直连 GET `/minecraft/profile` | 401，JSON，未认证 | 530 ms |

证书校验保持开启。报告：`.local/login-probe/network-20261023-025021.json`。当前可正常直连 Minecraft 官方 API；结合已认证请求的明确错误与官方允许名单规则，可将本次失败定位为应用准入拒绝，而非普通连接故障。没有为此重复用户登录，也未用其他启动器的 Client ID 对照。

下一步见 [Minecraft API 申请准备](MINECRAFT-API-APPLICATION.md)。官方表单要求提供 Tenant ID 与可在 Azure 核验的联系邮箱；目前只准备资料，未对外提交。

实际登录使用已验证 HTTPS 可用的沙箱外进程，不关闭证书校验。

报告：`.local/login-probe/result-20261023-014825-823.json`、`result-20261023-014935-046.json`。完整错误与阶段状态已保留。

参考：[微软设备码协议](https://learn.microsoft.com/en-us/entra/identity-platform/v2-oauth2-device-code)、[桌面应用平台配置](https://learn.microsoft.com/en-us/entra/identity-platform/scenario-desktop-app-configuration)；基线实现 `PCL.Core/Minecraft/Profile/Authentication/MicrosoftProvider.cs`。
