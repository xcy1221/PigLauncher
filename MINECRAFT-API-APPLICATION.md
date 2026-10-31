# Minecraft Java API 准入申请

更新：2026-10-26。状态：用户提供“AppID Review Complete (10.23.2026)”邮件，通知本批次应用已获准加入允许名单。原 Client ID 首次完整登录链路已实测通过。

## 当前判断

2026-10-26 使用原 Client ID 和消费者端点，微软、Xbox Live、XSTS、Minecraft 令牌、Java 所有权及角色信息均 HTTP 200。应用准入阻塞已解除；账号恢复、刷新及正版服务器登录仍待验收。

本轮报告：`.local/login-probe/result-20261026-082358-125.json`。历史网络与 403 证据保留在 `.local/login-probe/`，详见 [登录验证](MICROSOFT-LOGIN-TEST.md)。

## 已核实的官方入口

- [Java Edition Game Service API Review or Application Process](https://help.minecraft.net/hc/en-us/articles/16254801392141)：已用浏览器读取正文，要求新应用经人工审核加入允许名单。
- [AppID Review 申请表](https://aka.ms/mce-reviewappid)：已沿官方页面链接打开，实际为 Microsoft Forms，可申请新 AppID 或报告现有 AppID。
- 官方帮助页另提供咨询邮箱 `enforce@minecraft.net`；未发送邮件。

此前网页文本抓取仅取得站点框架，不能据此认定说明或申请表失效。本轮浏览器已确认两者均可打开，无需先通过未经证实的 ID@Xbox 路线。

## 表单准备

| 字段 | 内容或状态 |
|---|---|
| 已阅读并理解 EULA 与使用指南 | 由用户阅读并决定，不代勾选 |
| 联系邮箱 | 已建议填写与 Azure 联系信息一致的邮箱；实际填值未留存 |
| 请求类型 | New AppID for Approval |
| 应用名称 | PigLauncher |
| Application ID | 使用 `.local/service-config.json` 中已有的 `MS_CLIENT_ID` |
| Tenant ID | 已指导从 Entra 应用概述复制 Directory (tenant) ID；实际填值未留存 |
| 相关网站或域名 | `https://github.com/xcy1221/PigLauncher`；目前私有，沿用用户决定，附注如实说明 |
| 申请理由 | 见下方草稿 |
| 其他信息 | 早期开发、个人非商业、仓库私有；可提供脱敏的阶段状态和错误时间，不提供令牌 |

申请理由草稿：

> PigLauncher is a personal, non-commercial Windows launcher based on PCL CE, currently under development. It uses its own Microsoft Entra public-client AppID and the device-code authentication flow. Microsoft OAuth, Xbox Live and XSTS authentication succeed, but Minecraft Services returns HTTP 403 with "Invalid app registration" at /authentication/login_with_xbox. I am requesting Java Edition game service API access to verify the signed-in user's game ownership and retrieve their profile before enabling gameplay. The intended integration will preserve authentication and ownership checks.

表单明确要求启动游戏、访问服务器或站点前认证并验证所有权；禁止绕过这些检查，且不因服务器配置而例外。PigLauncher 名称未包含表单列出的禁用词。表单称每周审核，重复提交不会加快；这不是具体通过时间或通过保证。

以上保留准备版本，完整提交内容尚未核验；所有权和角色接口未实测通过。2026-10-23 用户表示进入等待审核阶段，后续在此追加回执、编号、审核回复及需要补充的材料。
