# 原始 CF 清单中的下载地址缺失实测

范围修订：仅统计指定目录中保留下来、含 CF projectID/fileID 的三份 `manifest.json`；其他实例、额外添加的模组、未识别文件全部排除。复用 2026-10-31 19:34–19:48（北京时间）的实测响应及下载证据，本次没有重复联网下载。

## 结论

- 三份清单共 **617 条必需资源**，按 CF file ID 去重后为 **529 个文件版本**。每一条都取得了与清单 projectID 一致的官方文件元数据。
- **5 条未提供 `downloadUrl`**：按清单条目计 **0.81%**，按去重版本计 **0.95%**。三份整合包均至少出现一条。
- 这 5 个文件涉及 **4 个项目**，项目的 `allowModDistribution` 均为 `false`；重复批量查询仍为空，专用地址接口均返回 **403**。
- 按猪咪现有逻辑拼接官方 CDN，并带项目 Key 后，**5/5 个完整下载成功，文件长度与 SHA-1 全部匹配**，合计 1,687,414 字节（1.61 MiB）。
- 其余 **612 条带下载地址的资源**对应 **524 个去重版本**，普通 GET 可开始下载。其中 5 个做完整下载对照，其余 519 个读取前 1 KiB 并与本地对应文件比较。没有把这些起始下载检查称为全文件安装通过。

本报告样本均由保留的 CF 清单直接指定，其他来源未明的本地文件不参与统计。结果显示接口拒绝给地址与 CDN 仍可取回文件同时存在；不据此判定服务条款许可或责任归属。

## 三份清单

| 整合包 | 清单版本 | Minecraft | 资源条目 | 有地址 | 空地址 | 回退完整校验 |
|---|---|---|---:|---:|---:|---:|
| A New Technological Era | Release v2.0.7 | 1.18.2 | 207 | 206 | 1 | 1 |
| Construct Technological Innovation | 0.4b | 1.19.2 | 258 | 255 | 3 | 3 |
| Mechanical Mastery | r1.5.0 | 1.18.2 | 152 | 151 | 1 | 1 |

全部清单条目的 `required` 均为 `true`。总条目 617 与去重版本 529 的差别来自跨清单共用文件；5 个空地址版本互不重复。

## 五个具体文件

| 整合包 | 项目 | 文件版本名 | CF 文件 ID | 地址接口 | CDN 完整校验 |
|---|---|---|---:|---:|---|
| A New Technological Era | Entity Culling Fabric/Forge | `entityculling-forge-1.6.1-mc1.18.2.jar` | 4406217 | 403 | 200，大小及 SHA-1 匹配 |
| Construct Technological Innovation | Entity Culling Fabric/Forge | `entityculling-forge-1.6.1-mc1.19.2.jar` | 4404949 | 403 | 200，大小及 SHA-1 匹配 |
| Construct Technological Innovation | Gobber Delight (A Farmer's Delight Add-on) | `gobber_delight_2.0.0_forge_1.19.2.jar` | 4715381 | 403 | 200，大小及 SHA-1 匹配 |
| Construct Technological Innovation | Structory | `Structory-1.19-v1.3.jar` | 4033419 | 403 | 200，大小及 SHA-1 匹配 |
| Mechanical Mastery | More Overlays Updated | `moreoverlays-1.20.11-mc1.18.2.jar` | 4459158 | 403 | 200，大小及 SHA-1 匹配 |

逐文件完整哈希、大小、地址及检查时间见 [五项回退明细](cf-manifest-audit-2026-10-31/fallback-files.csv)。

## 验证依据与边界

1. 以三份原清单中的 fileID 为样本选择依据，关联已保存的官方 `/v1/mods/files` 响应，并核对响应 modId 与清单 projectID 一致；没有按本地 JAR 名称推断来源。
2. 对空地址再次批量查询，并调用 `/v1/mods/{modId}/files/{fileId}/download-url`；再执行带本项目凭证的官方 CDN 普通 GET，完整读取并核对发布的长度与 SHA-1。
3. 全程使用项目既有代理线路，保持证书验证；原实例没有被修改，报告和证据不含 API Key。
4. 最初的 Range 探测在本线路返回 404，随后已改用普通 GET 复核。本报告仅采用最终普通 GET 结果，不将 Range 探测差异当作文件下载失败。
5. 这是这些原清单在本次时间窗口的重下载检查，并非当初安装日志；不能还原当年的作者设置，也没有重装整包或验证启动、依赖完整性。

接口字段与分发开关说明：[CF REST 文档](https://docs.curseforge.com/rest-api/)、[Project Distribution Toggle](https://support.curseforge.com/support/solutions/articles/9000207877-project-distribution-toggle)。

## 回信第 3 项可据此收窄

补充回信第 3 项已采用本报告的原清单口径，并于 2026-10-31 20:21（北京时间）经用户确认后发送，待 CF 答复。以下为实测事实表述；最终邮件及后续进展见 [发送记录](../CF-API-APPLICATION.md)：

> We tested the files listed in three retained CurseForge modpack manifests (617 entries, representing 529 distinct file versions). Five versions returned no downloadUrl; their projects had allowModDistribution set to false, and the dedicated download-URL endpoint returned HTTP 403. Authenticated downloads from the official CDN using URLs derived from the file metadata succeeded for all five, with matching file sizes and SHA-1 hashes. Is this CDN fallback permitted in this situation, or should users be directed to the CurseForge website?

## 附件

- [三份清单统计](cf-manifest-audit-2026-10-31/per-pack.csv)
- [617 条清单资源及验证结果](cf-manifest-audit-2026-10-31/manifest-files.csv)
- [五项回退文件明细](cf-manifest-audit-2026-10-31/fallback-files.csv)
- [机器可读摘要](cf-manifest-audit-2026-10-31/summary.json)
- [扩展取样附录](CF-DOWNLOAD-AUDIT-2026-10-31-EXTENDED.md)：保留此前其他实例的取样数据，不参与本报告结论。

原始脱敏证据：`.local/cf-pack-audit/20261031/`；原清单位于指定源目录下对应实例的 `manifest.json`。复核脚本：`scripts/cf-pack-audit/manifest_report.py`。
