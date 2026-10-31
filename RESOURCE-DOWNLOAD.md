# 资源下载接入验证

探测日期：2026-10-22；文档维护：2026-10-31。以下保留历史接口与样本探测，不代表最新开发版完整验收。

后续进展（2026-10-31）：CF 已获批，Key 已接入本地配置；经启动器引擎和项目代理验证官方搜索、项目详情、1.21.1/Fabric 文件列表及 CDN 下载。样本为 `fabric-api-0.116.17+1.21.1.jar`，2,452,735 字节，大小及 SHA-1 匹配，JAR 可读；样本已清理。证据：`.local/functional-tests/20261031-cf/evidence/`。以下无 Key 探测与申请说明保留为历史记录；界面操作、依赖、整合包和受限分发仍待验收。

CF 全称 **CurseForge**；M 站通常指 **Modrinth**。资源探测结果保留如下；2026-10-23 登录测试已通过微软与 Xbox 授权，Minecraft 令牌返回 403，详情见[登录验证](MICROSOFT-LOGIN-TEST.md)。

## 下载源策略（2026-10-31）

默认仅使用官方 API/CDN。按用户要求重新开放镜像选择：文件、版本列表、社区资源可分别选择官方、优先镜像或官方缓慢时使用镜像；沿用 BMCLAPI、MCIM，镜像失败可回退官方。6 项设置与路由检查通过，未在本轮进行镜像联网或完整安装测试。

## 本轮结果

| 路径 | 只读实测 | 结论 |
|---|---|---|
| Modrinth 官方搜索 | `GET /v2/search`：200，首个结果为 Fabric API | 无 Token 可用 |
| Modrinth 官方版本列表 | Fabric API，Minecraft 1.21.1 / Fabric：200，返回 36 个版本 | 无 Token 可用 |
| Modrinth 官方 CDN | 下载 `fabric-api-0.116.17+1.21.1.jar`：200 | 2,452,735 字节，大小及 SHA-512 均匹配；压缩包可读，含 `fabric.mod.json` |
| CurseForge 官方 API | `GET /v1/mods/306612`：403 | 当前无 Key 的请求未通过；尚未验证有 Key 的路径 |

报告和下载样本保存在 `.local/resource-probe/`，样本未运行或安装。2026-10-22 探测中 MCIM 的 CF 搜索、文件列表及 Modrinth 搜索均返回 503；这不是当前可用性结论，恢复选项后仍待联网复核。

## 现有代码是否可复用

- **可以复用，无需另装下载工具或官方客户端。** 已有 .NET SDK、HTTP 客户端及 CE 的搜索、版本解析和下载器足够开展接入。
- `ModComp.cs` 已覆盖模组、整合包、资源包、光影等资源类型；本轮只验证了一个模组样本，其他类型和批量依赖尚未实测。
- 审查基线的 `ModDownload.cs` 包含官方与 MCIM 地址切换，`Config.cs` 默认策略为 1（平衡）；后续个人开发版已限制官方路径。其余代理及完整安装验证仍见验收清单。
- `RequestSigning.cs` 已向 CurseForge API 和相关 CDN 域名添加 `x-api-key`，但本项目尚无可用 Key。
- 此次探测使用 PowerShell/.NET 直接请求，不等同于启动器 UI 与整合包安装的端到端验收。

## CurseForge 的剩余条件

第三方启动器按[官方申请说明](https://support.curseforge.com/support/solutions/articles/9000208346-about-the-curseforge-api-and-how-to-apply-for-a-key)进入[申请表](https://forms.monday.com/forms/dce5ccb7afda9a1c21dab1a1aa1d84eb?r=use1)，填写联系方式、项目说明、相关链接及预计用途；Overwolf 审核通过后邮件联系。[API 文档](https://docs.curseforge.com/rest-api/)也将第三方服务引导至该表，不能假定注册 Studios 控制台后即有第三方访问权限。已依据用户的填写截图核对表单，待用户提交；完整案例与后续结果见[申请记录](CF-API-APPLICATION.md)。

申请表还要求 Discord 用户名、项目目标与动机、支持游戏、网站与 Git 地址、商业模式、作者支持方案及条款确认。目前仓库私有，需如实说明审核方无法直接查看；启动器提供平台外下载也属于表单所述分发，不能仅因自用填成“不涉及分发”。

用户决定：先保持仓库私有申请，若未通过再讨论是否公开。必填的 Website URL 与 Git URL 均填写 `https://github.com/xcy1221/PigLauncher`；附注说明暂无独立网站，因此使用仓库地址，且仓库目前私有。

附注草稿：The project is in early development and is intended for personal, non-commercial use by one user. There is no standalone website yet, so the repository URL is provided as the project website. The repository is currently private.

[CF 第三方 API 条款](https://support.curseforge.com/support/solutions/articles/9000207405-curse-forge-3rd-party-api-terms-and-conditions)第 2.3 节说明，超出额度后继续使用可能要求另行签署含付费条件的协议，不代表提交申请即产生费用；第 3.1 节包含通过代理/VPN 隐藏身份或地理位置的限制，CF 接入与本项目代理策略的兼容范围尚待确认。

项目用途草稿：

> PigLauncher is a personal, non-commercial Minecraft Java launcher based on PCL CE, currently under development. It will use the CurseForge API to search mods and modpacks, retrieve version and dependency metadata, and download permitted files for one user. It will respect authors' third-party distribution settings and will not mirror or redistribute files.

官方 API 需要开发者 Key。除此以外，CurseForge 于 2026-06-10 公告：从 2026-07-16 起，`edge.forgecdn.net` 文件下载也要求有效 Key，无 Key 请求将返回 401。本轮未取得 CF 文件样本，未单独验证其 CDN；有 Key 的官方路径待验证。

因此不能把“获取文件名后直接拼接 CDN 地址”当作可靠的无 Key 方案。取得 Key 后，还需验证搜索、版本列表、文件下载、文件校验以及第三方分发受限时的手动补齐流程。

## 当前路线

1. 优先采用已验证的 Modrinth 官方接口和 CDN，继续复用 CE 代码。
2. CurseForge Key 已取得，官方样本路径通过，继续验证界面及完整安装流程。
3. 验证官方 API、CDN 经代理访问，以及启动器内的搜索、版本筛选、依赖处理和下载失败提示，再扩展到常用整合包。
4. Microsoft Client ID 已验证至 XSTS，Minecraft 令牌返回 `403 Invalid app registration`；后续核实应用准入渠道，结果见登录记录。

证据：`.local/resource-probe/metadata-check.json`、`.local/resource-probe/download-check.json`。

参考：[Modrinth API](https://docs.modrinth.com/api/)、[CurseForge REST API](https://docs.curseforge.com/rest-api/)、[CurseForge CDN 鉴权公告](https://blog.curseforge.com/introducing-api-key-authentication-for-curseforge-file-downloads/)、[CurseForge Key 申请](https://support.curseforge.com/support/solutions/articles/9000208346-about-the-curseforge-api-and-how-to-apply-for-a-key)。
