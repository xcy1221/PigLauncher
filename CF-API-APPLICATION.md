# CurseForge API 申请记录

## 2026-11-13：Discord 补充确认与受限文件处理

用户提供 MarmAmic 的私信截图：先回复第 1、2 项可以，第 3 项需向开发团队确认；随后明确第 3 项不允许，必须引导用户通过 CF 网站下载。

- 已确认：现有批准 / Key 可覆盖已说明的公开源码与 GitHub Releases 计划；项目 Key 可内嵌公开二进制，继续排除在公开源码之外。
- 不允许：作者关闭分发、API 无下载地址且下载地址接口 403 时，自行拼接官方 CDN 地址下载。
- 本轮实现：移除空地址 CDN 拼接；禁止分发或缺少地址时提供官网文件页。整合包保留实例及已下载文件，生成含文件名、官网入口和放置位置的手动补齐清单。
- CF 项目权限及文件元数据只取官方 API；旧文件缓存中的拼接地址不再被采用。允许分发的文件仍按官方 API 地址下载，可沿用已选文件镜像。
- 截图仅本地存档：`.local/curseforge-application/2026-11-12/marmamic-confirmation.png`；原截图显示回复时间为“昨天 22:27 / 22:50”，用户于 11 月 13 日提供。
- 用户随后授权发布 `0.1.0-preview.2` 并公开仓库；项目保留适用许可及来源声明，执行决定见 [记录](LICENSE-REVIEW.md)。

以下保留早期申请及待答复记录，当前结论以上述确认及本轮实现为准。

## 案例 CF-2026-10-22-01

- 状态：已获批。2026-10-31 通过 Gmail 核对官方批准邮件。
- 原申请策略：仓库保持私有，按单用户、非商业、开发中的项目申请；后续范围变更与回信见下文。
- 原始截图：`.local/curseforge-application/2026-10-22/form-01.png`、`form-02.png`。含个人联系信息，仅本地保存，不纳入提交。
- 可见必填项已填写，三个确认框均已勾选；Website URL 与 Git URL 一致，Discord 使用新版无数字后缀的用户名。
- 项目范围、商业模式、作者支持方案与附注在截图中被省略号截断；下表长文本依据此前填写草稿记录，尚未逐字核对隐藏部分。

| 字段 | 填写记录 |
|---|---|
| Project name | PigLauncher |
| 身份及联系方式 | 已填写；原始值见本地截图 |
| Project goal and scope | A personal, non-commercial Minecraft Java launcher based on PCL CE, supporting mod and modpack search, dependency information, and permitted downloads for one user. |
| Why are you building this project? | To maintain a stable, customizable launcher for my own Minecraft installations and modpacks. |
| Supported games | Minecraft |
| Website URL / Git URL | https://github.com/xcy1221/PigLauncher |
| Monetization | No |
| Business model | None. This project is for personal use only, with no monetization planned. |
| Contribution to mod authors | The launcher will preserve author attribution and original project links, respect third-party distribution preferences, and direct restricted downloads to the original CurseForge pages. No file rehosting is planned. |
| Additional notes | The project is in early development. Its GitHub repository is currently private, and there is no public website. Expected usage is limited to one user. |
| 确认框 | 作者分发许可、超量可能减少调用或涉及带宽费、已阅读并同意 API TOS：截图均为勾选状态 |

## 审核结果

- 批准时间：2026-10-29 16:20（北京时间）；发件人 `api@curseforge.com`，主题 `Your CurseForge API Application - Approved`，明确批准 PigLauncher 使用第三方 API。
- 已取得 API Key，保存在忽略提交的 `.local/service-config.json`，不在文档记录原值。另有 Studios 欢迎邮件，不作为批准依据。
- 提交时间和申请编号：邮件未提供。
- 如需重申：保留本案例，记录新版本的字段变更和对方理由，供结果对比；不预先认定私有仓库是拒绝原因。

## 2026-11-01 当前交付状态

- 以本人自用为核心，朋友试用为附加需求；仓库保持私有，Release 保持草稿，补充邮件所述公开计划尚未实施。
- 按用户要求，将项目 Key 存入仓库 Secret `CURSEFORGE_API_KEY`，由 [Actions](https://github.com/xcy1221/PigLauncher/actions/runs/41218535388) 编译内嵌；已验证内嵌结果并替换草稿附件。本地 `service-config.json` 仍可覆盖，源码与文档不记录 Key 原值。
- CF 补充确认仍按待答复记录；自动构建成功不作为审批结果。附件与校验记录见 [验证记录](VALIDATION.md)。

## 2026-10-31 使用范围补充（已发信，待 CF 答复）

- 用户计划公开源码仓库，并通过公开 GitHub Releases 提供应用二进制，不进行任何形式的宣传。预计最初 4–5 人使用；公开 Releases 不限制其他人获取。
- 已重读原批准邮件：批准 PigLauncher，未写明单用户上限；“单用户”是申请预期，不应直接当作审批人数限制。
- 已回复原批准邮件更新范围，请求确认原批准 / Key 可继续使用，以及桌面构建的凭证交付方式；当前待 CF 答复。
- Key 继续排除在公开源码之外；不把编译进 EXE 当作保密保证。按用户要求保留下载地址为空时的官方 CDN 拼接回退，客户端交付及适用边界仍待 CF 补充确认。
- 条款依据、代码边界见 [许可核查](LICENSE-REVIEW.md)；发送记录如下。

### 回信发送记录

- 用户审阅中英对照后明确同意发送；已于 **2026-10-31 20:21:55（北京时间）**发送，Gmail 回查标签为 `SENT`。
- 收件人：`api@curseforge.com`；主题：`Re: Your CurseForge API Application - Approved`；回复原批准邮件。
- 邮件说明公开源码和 GitHub Releases、预计最初 4–5 人使用但任何人可获取发布版本，不商业化、不重托管文件、不做任何形式的宣传。
- 三项待答复问题：① 原批准及 Key 是否覆盖上述分发计划；② 能否在公开 Releases 的应用二进制中内嵌（硬编码）项目 Key，且不将 Key 写入公开源码；③ 项目分发标记关闭、专用地址接口 403 时，是否允许官方 CDN 拼接回退，或应引导用户从 CF 网页下载。
- 第 3 项只引用三份原 CF 清单的 617 条资源 / 529 个去重版本，以及 5 个 API 无地址、接口 403、CDN 完整下载校验成功的文件版本；附 Entity Culling（4406217、4404949）、Gobber Delight（4715381）、Structory（4033419）、More Overlays Updated（4459158）。
- 已发送邮件 ID：`1a191e8ca694b9d5`；会话 ID：`1a186beb8046f1d7`；原草稿 ID：`r-3910760169432010914`。发信不等于 CF 已批准补充范围；发信当时程序凭证实现未改动，2026-11-01 的内嵌实现见上节。

### 原 CF 清单实测（2026-10-31，当前判断范围）

- 按用户要求只考虑保留原 CF 清单的三份整合包：A New Technological Era、Construct Technological Innovation、Mechanical Mastery。其他实例的扩展取样只保留为附录。
- 三份清单共 617 条必需资源，去重为 529 个 CF 文件版本；5 个版本无地址（按条目 0.81%，按去重版本 0.95%），涉及 4 个项目。5 个均为项目分发标记关闭、专用地址接口 403；官方 CDN 回退全部完整下载并通过大小与 SHA-1 校验。没有因本次收窄范围再次联网下载。
- 完整报告与逐文件明细见 [CF 下载实测](reports/CF-DOWNLOAD-AUDIT-2026-10-31.md)。上述原清单口径已用于发出的补充回信。

## 上游 Key 与受限下载核查

- 已核对 CE 基线：构建工作流将 GitHub Secret 注入 `PCL_CURSEFORGE_API_KEY`；`EnvironmentInteropGenerator` 把它写入生成源码的字典，随程序集编译。运行时向官方 API/CDN 添加 `x-api-key`。这是避免公开源码泄露，不是避免凭证随二进制交付；未提取上游 Key，也未核实其单独授权。PCL 主线各历史版本的交付实现未核验。
- 作者的 CF 分发开关控制第三方 API 分发项目资料和文件；已批准的应用 Key 不自动覆盖作者拒绝。开源许可与使用 CF 服务获取文件的权限应分别判断。
- 空 `downloadUrl` 不能单独证明作者禁用。用户要求将官方 CDN 拼接保留为兼容回退；未据服务端可获取性认定许可或责任归属。401 仍应按认证问题处理。
- 已调整 CF / Modrinth 整合包：基础安装失败明确报错；基础完成后资源获取失败，保留对应实例及文件，标记“安装未完成”并记录失败详情。原本静默跳过无法下载文件的路径也会报告未完成。
- PCL 指南处理暂缓。5 项隔离检查与构建通过，见 [验证记录](VALIDATION.md)；CF 补充回信已发送，待答复。
