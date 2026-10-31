# 猪咪启动器（PigLauncher）

更新：2026-11-01；记录截至当日的代码、构建与已有实测。

首个预览版本为 `0.1.0-preview.1`，仓库与 Release 草稿保持私有，CF 回信后再决定公开。版本说明见 [发布记录](releases/0.1.0-preview.1.md)。推送 `v*` 标签或手动运行 `Build release` 工作流，会从对应标签编译 Windows x64 自带 .NET 运行时的 ZIP 与 SHA-256，校验后替换草稿附件。构建通过仓库 Secrets `CURSEFORGE_API_KEY`、`MS_CLIENT_ID` 内嵌服务配置；包内 `build-info.json` 记录源码提交和工作流地址。

本地运行 `scripts/New-ReleasePackage.ps1` 默认仍不嵌入 CF Key，输出位于忽略提交的 `.local/releases/`；显式凭证构建使用 `-EmbedServiceCredentials` 及对应 `PCL_` 环境变量。源码和工作流仅保存 Secret 名称，账号数据不参与打包。

当前开发版位于 `dev/PCL-CE/`，名称为“猪咪启动器（PigLauncher）”，作者/维护者为 `xcy1221`。已形成可运行的个人开发版，完成品牌外观、默认官方源与可选镜像、CE 更新隔离和少量功能调整。用户已报告下载、启动及创建世界成功；完整正版登录、CF 资源和其余安装流程仍待验收，见 [验证记录](VALIDATION.md)。原始审查副本保持不变。

隔离试用：运行 `scripts/Start-PigLauncherTest.ps1`。它使用 `dev/PigLauncher/` 构建，新建 `.local/functional-tests/<编号>/`，退出后清理测试数据并保留证据。

默认外观已改为带厚度的 Minecraft 猪头图标与“猪咪粉”主题，包含标题栏、按钮、选中状态和粉色背景；浅色、深色均默认使用。已有手动配色保留，可在“设置 → 个性化”选择猪咪粉。图标源文件与生成提示词位于 `assets/`，运行 `scripts/Update-PigIcon.ps1` 可重新导出 PNG 和多尺寸 ICO。

空白主页新增水平旋转的原版成年粉猪，采用 Minecraft Java 1.21.1 原始纹理、模型尺寸和 UV；“设置 → 启动器 → 猪咪设置”可实时预览、自动保存和单独恢复默认。自定义主页保持原功能，不可见或最小化时暂停。资源来源见 [SOURCE.md](dev/PCL-CE/Plain%20Craft%20Launcher%202/Images/Pig/SOURCE.md)。

| 猪咪设置 | 当前范围 | 默认值 |
|---|---|---|
| 主页显示 / 自动旋转 / 反向 | 开关 | 开 / 开 / 关 |
| 转速 | 0–120°/秒，0 为暂停 | 45°/秒 |
| 大小 | 60%–140% | 100% |
| 俯视角 | 0–35° | 18° |
| 外观 | 固定原版成年粉猪，无自创换色 | 原版纹理 |

旋转动画目标为 60 FPS，实际流畅度受设备与渲染负载影响。已修复切回主页时配置观察器并发修改导致的集合枚举异常；文字改为 WPF Display 排版和像素对齐，实际显示器清晰度待复核。

当前范围：Minecraft 应用准入及独立首次登录已通过，CurseForge Key 已获批并接入；维护原有 PCL 功能、修复实际问题并增加少量独立小功能，暂不大规模改造原生功能。

CF / Modrinth 整合包现在先完成 Minecraft 与加载器安装，再获取整合包资源。基础安装失败明确报错；后续模组资源失败时保留实例和已下载文件，列表描述显示“安装未完成”及版本，详情写入实例的 `PCL/ModpackInstallFailure.txt`。保留 CF 官方 CDN 地址拼接回退。隔离检查已通过，真实网络故障与窗口提示待实测。

WebP 用于部分资源缩略图及 WebP 背景、本地图片，不影响游戏下载和启动。启动时使用小样本实测解码能力，缺少支持时说明为可选图片组件，可打开微软商店安装；不再仅凭商店包名称判定缺失。

设置中的“语言和区域”已合并到“杂项”，独立语言入口、国际化宣传卡片和反馈页已移除；个性化中的官方快照版推广模块也已移除。

游戏启动面板中的“你知道吗”、旧图标启动画面及对应开关已移除，专门的启动画面后续单独制作。

顶部新增“实例列表”，位于“启动”之后，复用现有实例搜索、收藏和管理功能；选择实例后回到启动页。列表顶部可选默认分类、按游戏版本、按加载器或不分类，自动保存选择。保留原有选择入口，沿用隐藏设置及启动期间的切换限制。

默认窗口基准为 1800×1100（原默认宽高各翻倍），启动时按所在屏幕可用区域的宽、高分别限制到 75%，并按 DPI 换算和居中。旧默认尺寸只迁移一次，手动尺寸继续记忆、下次启动仍受上限约束；启动后可手动放大。工具页已移除“下载自定义文件”，供自定义主页调用的共享下载方法保留。

Java 25 已安装在 `.local/java/25/`，隔离脚本会自动将其提供给启动器；当前已打开的旧窗口可手动导入其中的 `bin/java.exe`。项目代理配置位于 `.local/network-config.json`，已修正为实测可用的 IPv6 回环地址，并取消 Forge Maven 直连例外；Forge 官方文件站在自动协议模式使用 HTTP/1.1，不修改系统设置。启动器内置联机大厅及相关服务已移除，工具页保留工具箱。

工作目录：`D:\xcy\game\minecraft\PigLauncher`。当前为 Windows 个人自用启动器，基于 PCL CE 做最小改造，复用已有安装、启动、Java 与模组管理功能。

项目仓库：[xcy1221/PigLauncher](https://github.com/xcy1221/PigLauncher)（私有）。文档入口：[开发路线](ROADMAP.md)、[分项验证](VALIDATION.md)、[资源下载验证](RESOURCE-DOWNLOAD.md)、[源码审查](PCL-CE-review.md)。

## 设计决策

- 基线：PCL CE v2.15.1，提交 `1a7653127d578efdb2a9388999a3fdc67d9e49d5`，保存在 `upstream/PCL-CE/`。
- 源码改动只维护 `dev/PCL-CE/`；内部程序集名暂沿用 `Plain Craft Launcher 2`，保证资源路径兼容。
- 当前优先解决外围服务接入；代码编写与维护由 Codex 协助，不作为主要选型障碍。
- 地区策略由个人版本修改。当前仅自用，发布、脱敏与许可相关处理留到决定发布时评估。
- 保留成熟下载器和启动流程；个人版本需处理 CE 自动更新，防止上游包覆盖修改。
- 登录认证与下载、本地管理独立验证；Minecraft 审核不阻塞后两者，完整正版启动另行验收，见[分项计划](VALIDATION.md)。
- 下载默认“仅使用官方源（无镜像回退）”。2026-10-31 按用户要求恢复文件、版本列表和社区资源的源选择，可分别选择优先镜像或官方缓慢时使用镜像；沿用内置 BMCLAPI、MCIM 路径。镜像失效时允许回退官方，外部镜像当前可用性与完整安装仍待实测。
- 缺少的开发工具、缓存和本地配置放在本项目 `.local/`，不修改系统工具链或永久环境变量。

## 微软登录

2026-10-26 用户提供 AppID 审核通过邮件后，使用原 Client ID 重测：设备码、微软令牌、Xbox Live、XSTS、Minecraft 令牌、Java 所有权及角色接口全部返回 HTTP 200，历史 `403 Invalid app registration` 已消失。记录见[登录验证说明](MICROSOFT-LOGIN-TEST.md)。

原 Client ID 已接入 Release 启动器：从程序目录的 `service-config.json` 读取 `MS_CLIENT_ID`，没有该字段时保留原构建参数回退；配置错误会给出提示。修改 Client ID 不需要重新编译。

现有认证代码可复用：设备码登录 → 微软令牌 → Xbox Live → XSTS → Minecraft 令牌 → 所有权与角色信息。Client ID 是应用标识；本项目的设备码流程不需要 Client Secret，账号登录在微软页面完成。

获取步骤：

1. 登录 [Entra 管理中心](https://entra.microsoft.com/)，切换到自己的目录，进入“Entra ID → 应用注册 → 新注册”。
2. 名称填 `PigLauncher`，支持的账户类型选“仅个人 Microsoft 账户”；也可选择包含个人账户的多租户选项。本项目后来补充移动/桌面平台及 `https://login.microsoftonline.com/common/oauth2/nativeclient` 后，设备码请求才通过，详见测试记录。
3. 注册后，在“概述”复制“应用程序（客户端）ID / Application (client) ID”。
4. 新版页面进入“Authentication (Preview) → Settings”，将“Allow public client flows”设为“Enabled”并保存；旧版入口为“身份验证 → 高级设置”。无需创建 Client Secret。

以上依据[微软应用注册说明](https://learn.microsoft.com/en-us/entra/identity-platform/quickstart-register-app)与[公共客户端配置说明](https://learn.microsoft.com/en-us/entra/identity-platform/scenario-desktop-app-configuration)。应用注册成功与 Minecraft 服务准入需分别验证。

当前注册进展：

- Client ID 原始配置保存在忽略提交的 `.local/service-config.json`，交付副本为 `dev/PigLauncher/service-config.json`；隔离脚本每次复制最新 Client ID，不复制账号令牌。登录器已通过配置读取及设备码联网检查。
- 早前遇到 Entra 租户不匹配和普通试用银行卡验证失败；保留为历史问题，不再以重新绑卡或完成学生申请作为当前前置任务。
- 当前仅需目录和应用注册，不计划部署计费云资源或主动升级为即付即用。

AppID 已获准加入允许名单，见[申请记录](MINECRAFT-API-APPLICATION.md)。首次登录成功不等于全部验收完成；启动器内完整登录、账号重启恢复、令牌刷新和正版服务器认证仍待验收。隔离测试的官方主机白名单已补齐登录与皮肤接口。

验收：首次登录成功、正确显示拥有游戏的角色、重启后恢复账号、令牌刷新成功，并能进入需要正版认证的服务器。一次浏览器登录成功不算完成。

## 当前重点：CurseForge 与 Modrinth

- CF 全称 CurseForge，M 站通常指 Modrinth。2026-10-22 已完成无凭据接口及样本下载探测，见 [验证记录](RESOURCE-DOWNLOAD.md)。
- 2026-10-31 20:21（北京时间）已向 CF 回复补充范围：计划公开仓库及 Releases，不做任何形式的宣传，询问原批准 / Key、二进制内嵌 Key 及官方 CDN 回退；当前待答复，见 [发送记录](CF-API-APPLICATION.md)。
- Modrinth 官方搜索、版本列表和 CDN 下载均实测通过，样本大小和 SHA-512 匹配，无需 Token。
- CurseForge 于 2026-10-29 批准 PigLauncher。2026-10-31 已接入本地 Key，官方搜索、项目详情、版本列表与一个 CDN 样本下载校验通过。
- Key 读取程序目录 `service-config.json` 的 `CURSEFORGE_API_KEY`；原始配置位于忽略提交的 `.local/service-config.json`，交付及隔离测试副本同步该字段。审核记录见[申请案例](CF-API-APPLICATION.md)。
- 下载源选择已恢复，默认官方；CF 与 Modrinth 的镜像路径由社区资源设置单独控制，仍需逐项验证搜索、依赖与完整安装。
- Key 可用也不保证所有文件能自动获取。作者关闭第三方分发或缺少下载地址时，应提供手动补齐流程；CE 当前拼接 CDN 地址的兜底尚未实测。
- 保留 Modrinth 现有接入；它不能替代 CurseForge 独占内容。资源接入无需额外安装下载工具或官方客户端。
- Client ID、API Key 均已使用本地配置，更换值无需重新编译；API Key 和账号令牌不写入文档或提交。

## 本地工具

固定 .NET SDK `10.0.402 win-x64`，包含 Desktop Runtime；无需额外安装 Visual Studio。`global.json` 固定 SDK，`NuGet.Config` 指向官方包源。

```powershell
.\scripts\Install-ProjectTools.ps1
. .\scripts\Use-ProjectTools.ps1
& "$env:DOTNET_ROOT\dotnet.exe" build ".\dev\PCL-CE\Plain Craft Launcher 2\Plain Craft Launcher 2.csproj" -c Release '-p:Platform=x64' --verbosity minimal
```

SDK 位于 `.local/dotnet/`，NuGet 缓存位于 `.local/nuget/`，CLI 状态位于 `.local/dotnet-home/`；工具来源及校验值记录于 `.local/toolchain.json`。脚本只改变当前进程的环境；新终端继续使用封装命令即可。

PowerShell 封装命令中的 `-p:` 参数需要按示例加引号。

编译输出位于 `dev/PCL-CE/Plain Craft Launcher 2/bin/Release/net10.0-windows/win-x64/`；隔离脚本使用 `dev/PigLauncher/` 中已同步的交付副本，普通编译不会自动更新该副本。

首次编译后，SDK、依赖缓存与编译产物文件合计约 2.03 GiB；建议预留 3–5 GiB。游戏、Java、模组不在此预算内。

## 当前状态

- 已完成源码静态审查，见 [审查记录](PCL-CE-review.md)。
- 最近交付为 2026-10-31 Release/x64：构建 0 错误；设置与旧启动显示移除、语言区域读写 2 项检查通过，证据为 `.local/functional-tests/20261031-launch-display/evidence/`。下载源、CF、WebP 的历史检查见验证记录。
- 窗口尺寸与工具入口 8 项检查通过；原版猪 9 项、主页异常及文字设置 7 项检查通过。构建和离屏检查不替代实际窗口验收。
- Minecraft 与 CF 服务准入均已获批，阶段 2 的完整接入和阶段 4 的实际使用验收继续补齐；见 [开发路线](ROADMAP.md)。
- Forge、NeoForge、Fabric、LabyMod、OptiFine 列表经启动器引擎实取通过；用户报告下载、启动、创建世界成功，日志可确认 `26.3-NeoForge_26.3.0.95` 启动。其余加载器完整安装、批量依赖、整合包导入、取消及恢复仍待验收。
- Modrinth 官方接口及一个模组样本通过；试用日志另确认整合包列表取得 40 项，部分缩略图解码报错待查。Minecraft 独立首次登录和 CF 官方样本链路已通过。
- 项目远端为 `git@github.com:xcy1221/PigLauncher.git`；源码、文档与工具脚本纳入首个预览版快照，仓库保持私有，Release 保持草稿。

参考：[微软设备码流程](https://learn.microsoft.com/en-us/entra/identity-platform/v2-oauth2-device-code)、[Minecraft 应用注册错误的开发者报告](https://learn.microsoft.com/en-au/answers/questions/5984225/minecraft-services-http-403-invalid-app-registrati)、[CurseForge Key 申请](https://support.curseforge.com/support/solutions/articles/9000208346-about-the-curseforge-api-and-how-to-apply-for-a-key)、[Modrinth API](https://docs.modrinth.com/api/)。
