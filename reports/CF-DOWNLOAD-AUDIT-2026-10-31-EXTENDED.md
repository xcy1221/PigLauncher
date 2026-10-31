# CF 下载地址缺失与 CDN 回退实测（扩展样本附录）

范围提示：本文件保留全部实例的扩展数据，不作为当前判断依据。当前主报告仅统计保留原始 CF 清单的三份整合包，见 [主报告](CF-DOWNLOAD-AUDIT-2026-10-31.md)。
采样：2026-10-31 19:34–19:48（北京时间）。源目录：`D:\xcy\game\mc\.minecraft\versions`。

## 结论

- 扫描 **22 个现有实例、4,727 个模组 JAR 文件条目**（含 6 个禁用文件），按 SHA-1 去重为 3,663 个本地文件。
- 确认 **3,538 个 CF 文件版本**；其中 **110 个（3.11%）没有 `downloadUrl`**，涉及 **90 个项目、19/22 个实例**。按实例重复计数为 118/4,602（2.56%）。
- 这 110 个文件再次批量查询后仍为空，其所属项目的 `allowModDistribution` 全为 `false`，专用下载地址接口全为 **HTTP 403**。本批稳定缺失与作者分发关闭标记完全重合；没有发现可支持“只是 API 偶发漏字段”的样本。
- 使用猪咪相同的文件编号/文件名规则拼接官方 CDN 地址并带项目 Key 后，**110/110 个完整下载成功，大小和 SHA-1 全部匹配**，累计 263.85 MiB。由此确认：本样本存在“API 拒绝给地址，但同一项目凭证通过 CDN 仍可取得正确文件”的实际行为。
- 其余 3,428 个带地址文件的普通 GET 检查通过 3,428 个；另有 20 个正常地址文件完整下载校验。当前未发现普通 GET 的实际不可达案例（失败 0 个）。**普通 GET 只读取前 1 KiB 的文件不算完整下载通过。**

该结果证明回退确实能减少本批样本的获取失败；不能据此认定 CF 允许该用法、允许再分发全部文件，或责任只属于某一方。技术返回与服务约定是不同结论。

## 范围与方法

1. 只读取实例的 `mods/`、版本线索和原 CF 清单；没有执行 JAR、启动游戏或改写原实例。
2. 计算完整文件 SHA-1 与 CF 使用的去空白 MurmurHash2 指纹，调用官方 `/v1/fingerprints/432`。只有 API 文件 SHA-1 与本地完整 SHA-1 一致才归入“已确认 CF”。
3. 按确定的 file ID 调用安装器使用的 `/v1/mods/files`，并通过 `/v1/mods` 查询项目分发标记。对空链接再次查询批量接口及 `/v1/mods/{modId}/files/{fileId}/download-url`。
4. 空链接使用现有实现的 CDN 拼接方式，向官方 HTTPS API/CDN 发送本项目获批 Key。所有回退文件完整流式读取、核对长度及 SHA-1；不落盘保存 JAR。
5. 正常地址全部做普通 GET 启始下载检查并与本地文件前 1 KiB 比对；每实例选择一个小型正常文件做完整对照，跨实例重复后共 20 个不同对照文件。
6. 使用项目现有代理线路，证书验证保持开启；凭证不写入报告、CSV 或响应快照。沙箱 TLS 初始化失败属于本地运行环境问题，切换正常执行环境后查询成功，不计为 CF 失败。
7. 结束后复核全部 4,727 个源模组文件的大小及修改时间，变化数为 **0**。

API 结构依据：[CF 官方 REST 文档](https://docs.curseforge.com/rest-api/)；分发标记含义依据：[Project Distribution Toggle](https://support.curseforge.com/support/solutions/articles/9000207877-project-distribution-toggle)。

## 各实例结果

“CF”与“空地址”按实例内文件条目统计；同一文件出现在不同实例会重复计数。未识别文件不加入 CF 分母，也不认定为作者禁止。

| 实例 | MC | JAR | 已确认 CF | 空地址 | 空地址/CF | 回退完整校验 | 未识别 |
|---|---|---:|---:|---:|---:|---:|---:|
| 1.20.1_BasicPackage | 1.20.1 | 44 | 40 | 2 | 5.00% | 2 | 4 |
| 1.20.1-Fabric 0.16.13 | 1.20.1 | 7 | 7 | 0 | 0.00% | 0 | 0 |
| 1.21.4-Fabric 0.16.9 | 1.21.4 | 6 | 6 | 0 | 0.00% | 0 | 0 |
| 地下酒吧 | 1.20.1 | 116 | 110 | 5 | 4.55% | 5 | 6 |
| 剑与王国 | 1.20.1 | 296 | 289 | 12 | 4.15% | 12 | 7 |
| 异宠乱斗 | 1.20.1 | 139 | 136 | 7 | 5.15% | 7 | 3 |
| 勇者之章三 | 1.20.1 | 342 | 293 | 9 | 3.07% | 9 | 49 |
| A New Technological Era | 1.18.2 | 210 | 210 | 1 | 0.48% | 1 | 0 |
| atm9 | 1.20.1 | 444 | 443 | 5 | 1.13% | 5 | 1 |
| Beebeeblock_The_Skyhive | 1.21.1 | 376 | 376 | 3 | 0.80% | 3 | 0 |
| Construct Technological Innovation | 1.19.2 | 276 | 262 | 4 | 1.53% | 4 | 14 |
| Creative Co-Operation | 1.21.1 | 6 | 6 | 0 | 0.00% | 0 | 0 |
| Creative Drawers Producer 2 | 1.21.1 | 199 | 198 | 1 | 0.51% | 1 | 1 |
| DeceasedCraft_Beta | 1.20.1 | 312 | 312 | 8 | 2.56% | 8 | 0 |
| Enigmatica9Expert | 1.19.2 | 243 | 243 | 7 | 2.88% | 7 | 0 |
| Journey To Infinity | 1.21.1 | 217 | 217 | 3 | 1.38% | 3 | 0 |
| Mechanical Mastery | 1.18.2 | 156 | 156 | 1 | 0.64% | 1 | 0 |
| Mechanomania | 1.21.1 | 165 | 157 | 7 | 4.46% | 7 | 8 |
| NAST hard d0.9.2 | 1.21.1 | 265 | 264 | 4 | 1.52% | 4 | 1 |
| No Flesh Within Chest 2 | 1.20.1 | 205 | 174 | 13 | 7.47% | 13 | 31 |
| Not Too Complicated2 | 1.16.5 | 304 | 304 | 3 | 0.99% | 3 | 0 |
| Restless Horizons [Modernized RLCraft] | 1.20.1 | 399 | 399 | 23 | 5.76% | 23 | 0 |

各实例中包括普通 Fabric 实例及来源并非 CF 的整合包，不能将“22 个目录”称为“22 份完整 CF 原始发行包”。6 个禁用文件均不属于空地址案例；当前启用模组中的空地址仍为 118 条。

## 有原 CF 清单的三例

这三例按原 `manifest.json` 的 file ID 清单检查，可以区别原包资源与后来添加的本地模组。它们的全部清单文件都在本批确认集合中。

| 实例 | 整合包版本 | MC | 清单文件数 | API 返回数 | 空地址 | 回退完整校验 |
|---|---|---|---:|---:|---:|---:|
| A New Technological Era | Release v2.0.7 | 1.18.2 | 207 | 207 | 1 | 1 |
| Construct Technological Innovation | 0.4b | 1.19.2 | 258 | 258 | 3 | 3 |
| Mechanical Mastery | r1.5.0 | 1.18.2 | 152 | 152 | 1 | 1 |

三份清单合计 617 个文件条目，其中 5 个空地址（0.81%），全部回退校验成功。**这是当前原清单重下载结果，不能还原这些文件当年安装时的分发开关或服务状态。**

## 可核对的案例

完整 110 项见 [fallback-files.csv](cf-audit-2026-10-31/fallback-files.csv)，每项包含文件版本名、受影响实例、项目标记、403、CDN 状态、大小及 SHA-1。以下仅列代表性项目，每行是一个具体文件版本。

| 项目 | 文件版本名 | CF 文件 ID | 出现实例 | 地址接口 | CDN 完整校验 |
|---|---|---:|---|---:|---|
| [Structory](https://www.curseforge.com/minecraft/mc-mods/structory) | `Structory_1.20.x_v1.3.5.jar` | 5351581 | atm9、Restless Horizons [Modernized RLCraft] | 403 | 200，SHA-1 匹配 |
| [ServerCore](https://www.curseforge.com/minecraft/mc-mods/servercore) | `servercore-forge-1.5.2+1.20.1.jar` | 5929264 | 勇者之章三、Restless Horizons [Modernized RLCraft] | 403 | 200，SHA-1 匹配 |
| [Species](https://www.curseforge.com/minecraft/mc-mods/species) | `species-3.5.jar` | 6886321 | 异宠乱斗、Restless Horizons [Modernized RLCraft] | 403 | 200，SHA-1 匹配 |
| [More Overlays Updated](https://www.curseforge.com/minecraft/mc-mods/more-overlays-updated) | `moreoverlays-1.24.1-mc1.20.1-forge.jar` | 6945288 | 剑与王国、atm9 | 403 | 200，SHA-1 匹配 |
| [Entity Culling Fabric/Forge](https://www.curseforge.com/minecraft/mc-mods/entityculling) | `entityculling-forge-1.9.5-mc1.20.1.jar` | 7396677 | 剑与王国、勇者之章三 | 403 | 200，SHA-1 匹配 |
| [Cell⁴](https://www.curseforge.com/minecraft/mc-mods/cell4) | `Cell4-1.21.1-NeoForge-1.0.4fix3.jar` | 8465243 | Creative Drawers Producer 2、NAST hard d0.9.2 | 403 | 200，SHA-1 匹配 |
| [Spawner Bug Fix](https://www.curseforge.com/minecraft/mc-mods/spawner-fix) | `SpawnerFix-1.16.2-1.0.0.2.jar` | 3161147 | Not Too Complicated2 | 403 | 200，SHA-1 匹配 |
| [Emendatus Enigmatica](https://www.curseforge.com/minecraft/mc-mods/emendatus-enigmatica) | `EmendatusEnigmatica-1.2.16-build.46+mc1.16.5.jar` | 4174294 | Not Too Complicated2 | 403 | 200，SHA-1 匹配 |
| [EE: Occultism](https://www.curseforge.com/minecraft/mc-mods/emendatus-enigmatica-occultism-addon) | `EE_Occultism_Addon-1.0.0-build.4+mc1.19.2.jar` | 4361737 | Enigmatica9Expert | 403 | 200，SHA-1 匹配 |
| [EE: Thermal Expansion](https://www.curseforge.com/minecraft/mc-mods/emendatus-enigmatica-thermal-addon) | `EE_Thermal_Addon-1.0.0-build.7+mc1.19.2.jar` | 4361742 | Enigmatica9Expert | 403 | 200，SHA-1 匹配 |

## 排除 Range 探测误判

初始探测向正常地址添加 `Range: bytes=0-1023`，本线路下 3,408 个此类请求均为 404；相同文件改用不带 Range 的普通 GET 后可取得与本地一致的数据。另有 20 个正常地址文件完整校验成功。**这些 404 是本次探测方式与链路的差异，未作为文件删除、作者禁用或安装失败计数。**
本轮没有定位该差异发生在 CDN、代理还是其组合，也没有据此修改启动器下载器。若排查分片下载，应以实际下载器的普通 GET 回退能力另作验证。原始 Range 记录保留在证据目录 `probes/`，最终普通 GET 记录在 `get-probes/`。

## 对回信与实现的影响

- 回信已纠正为：公开源码仓库，通过公开 GitHub Releases 提供二进制，不做任何形式的宣传；预计最初 4–5 人使用，而非限制只有四五人能取得程序。后续用户确认的回信已发送，状态见 [发送记录](../CF-API-APPLICATION.md)。
- 第 2 项保留明确的应用二进制内嵌（硬编码）Key 询问。当前运行版仍从本地配置读取凭证，未因修改邮件而改动实现。
- 下段是扩展研究阶段的备选表述，未用于最终邮件。已发回信第 3 项只引用三份原 CF 清单的五个文件版本，见 [主报告](CF-DOWNLOAD-AUDIT-2026-10-31.md)。

> In tests of 3,538 exact CurseForge file versions from our installed instances, 110 returned no downloadUrl. For all of these files, allowModDistribution was false and the dedicated download-URL endpoint returned HTTP 403, while authenticated requests to the official CDN completed successfully with matching file sizes and SHA-1 hashes. Is this CDN fallback permitted in this situation, or should the launcher direct users to download these files through the CurseForge website?

- 现有回退只能处理“已拿到文件 ID、文件名等元数据，但地址缺失”的情况。API 整体不可用且无元数据时，不能仅靠拼接保证安装成功。
- 本次扩展研究保留回退与已完成的安装未完成提示，没有再次修改启动器；后续邮件状态以 [发送记录](../CF-API-APPLICATION.md)为准。

## 限制与证据

- 125 个本地文件未获得 CF SHA-1 一致的匹配；可能是其他平台、定制或修改文件，也可能无法由当前接口识别，原因未逐一查明。它们不是已证实的 CF 下载失败。
- 没有重放全部整合包安装，没有测试 MC、Java、加载器、启动、依赖闭包及取消恢复；普通 GET 的 1 KiB 校验只能证明开始下载时地址可用及内容前缀一致。完整下载通过的文件仅为 110 个回退文件及 20 个正常对照。
- 本次单一线路、单一时间窗口不能估计全平台长期故障率，也不能推断未来分发标记与 CDN 策略不变。

附件：[逐实例统计](cf-audit-2026-10-31/per-instance.csv)、[原清单统计](cf-audit-2026-10-31/original-manifests.csv)、[回退文件明细](cf-audit-2026-10-31/fallback-files.csv)、[全部本地文件](cf-audit-2026-10-31/all-local-files.csv)、[未识别文件](cf-audit-2026-10-31/unidentified-files.csv)、[机器可读摘要](cf-audit-2026-10-31/summary.json)。

原始脱敏响应及逐文件下载记录位于 `D:\xcy\game\minecraft\PigLauncher\.local\cf-pack-audit\20261031`。证据索引见附件目录的 `evidence-index.json`；原始 API 快照不含请求头或 Key。

复核脚本：`scripts/cf-pack-audit/Program.cs`（inventory / query / diagnose / probe / probe-get）及 `scripts/cf-pack-audit/report.py`。在线步骤需要本机项目配置；复用已有快照时不会重复请求接口。
