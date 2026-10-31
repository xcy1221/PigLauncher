"""Summarize the read-only CF audit; never reads or emits service credentials."""
import collections
import csv
import datetime as dt
import hashlib
import json
import pathlib
import sys

project = pathlib.Path(sys.argv[1]).resolve()
audit = pathlib.Path(sys.argv[2]).resolve()
dest = project / "reports" / "cf-audit-2026-10-31"
dest.mkdir(parents=True, exist_ok=True)
load = lambda name: json.loads((audit / name).read_text(encoding="utf-8-sig"))
inv = load("inventory.json")
files = {f["id"]: f for f in load("files.json")}
projects = {p["id"]: p for p in load("projects.json")}
matches = {m["sha1"]: m["file"]["id"] for m in load("fingerprint-matches.json")}
probes = {int(p.stem): json.loads(p.read_text(encoding="utf-8-sig")) for p in (audit / "get-probes").glob("*.json")}
diagnostics = {int(p.stem): json.loads(p.read_text(encoding="utf-8-sig")) for p in (audit / "diagnostics").glob("*.json")}
assert set(files) == set(probes), "Ordinary GET verification has not finished."
nulls = {i: f for i, f in files.items() if not f.get("downloadUrl")}
rechecked = {}
for path in (audit / "api").glob("null-recheck-*.json"):
    rechecked.update({f["id"]: f for f in json.loads(path.read_text())["data"]})
assert set(nulls) == set(diagnostics) == set(rechecked)
assert all(not rechecked[i].get("downloadUrl") for i in nulls)
assert all(projects[f["modId"]].get("allowModDistribution") is False for f in nulls.values())
assert all(diagnostics[i].get("status") == 403 for i in nulls)
assert all(probes[i]["attempts"][-1].get("fullDownload") and probes[i]["attempts"][-1]["success"] for i in nulls)

def csv_write(name, rows):
    if not rows:
        return
    with (dest / name).open("w", encoding="utf-8-sig", newline="") as stream:
        writer = csv.DictWriter(stream, fieldnames=list(rows[0]))
        writer.writeheader()
        writer.writerows(rows)

def pct(n, d):
    return f"{100*n/d:.2f}%" if d else "—"

rows, pack_rows, manifest_rows, changes = [], [], [], []
occurrences = collections.defaultdict(list)
for instance in inv["instances"]:
    name = instance["name"]
    for local in instance["files"]:
        fid = matches.get(local["sha1"])
        meta = files.get(fid, {})
        p = projects.get(meta.get("modId"), {})
        probe = probes.get(fid, {})
        last = (probe.get("attempts") or [{}])[-1]
        if fid:
            occurrences[fid].append(name)
        source_path = pathlib.Path(inv["source"]) / name / "mods" / local["filename"]
        stat = source_path.stat()
        original_time = dt.datetime.fromisoformat(local["lastWriteUtc"]).timestamp()
        if stat.st_size != local["size"] or abs(stat.st_mtime - original_time) > 0.00001:
            changes.append(str(source_path))
        rows.append({
            "instance": name, "minecraft_version": instance.get("minecraftVersion"),
            "local_filename": local["filename"], "disabled": local["disabled"], "bytes": local["size"],
            "sha1": local["sha1"], "cf_file_id": fid, "cf_project_id": meta.get("modId"),
            "cf_project": p.get("name"), "cf_filename": meta.get("fileName"),
            "null_download_url": (fid in nulls) if fid else None,
            "allow_mod_distribution": p.get("allowModDistribution"),
            "download_url_endpoint_status": diagnostics.get(fid, {}).get("status"),
            "cdn_status": last.get("status"), "cdn_verified": last.get("success"),
            "verification": ("full-size-and-sha1" if last.get("fullDownload") else "ordinary-get-first-1KiB-sha1") if fid else "unidentified",
        })
    local_rows = [r for r in rows if r["instance"] == name]
    known = [r for r in local_rows if r["cf_file_id"]]
    missing = [r for r in known if r["null_download_url"]]
    pack_rows.append({"instance": name, "minecraft_version": instance.get("minecraftVersion"),
        "local_jars": len(local_rows), "disabled": sum(r["disabled"] for r in local_rows),
        "cf_identified": len(known), "null_url": len(missing), "null_rate": pct(len(missing), len(known)),
        "fallback_verified": sum(r["cdn_verified"] for r in missing),
        "unidentified": len(local_rows)-len(known), "cf_manifest": bool(instance.get("manifest"))})
    if instance.get("manifest"):
        manifest = instance["manifest"]
        ids = [f["fileID"] for f in manifest["files"]]
        manifest_rows.append({"instance": name, "pack_version": manifest.get("version"),
            "minecraft_version": manifest["minecraft"]["version"], "manifest_entries": len(ids),
            "metadata_returned": sum(fid in files for fid in ids),
            "null_url": sum(fid in nulls for fid in ids),
            "fallback_verified": sum(fid in nulls and probes[fid]["attempts"][-1]["success"] for fid in ids)})

fallback_rows = []
for fid, meta in sorted(nulls.items()):
    probe = probes[fid]
    last = probe["attempts"][-1]
    p = projects.get(meta["modId"], {})
    fallback_rows.append({"cf_file_id": fid, "cf_project_id": meta["modId"], "project": p.get("name"),
        "filename": meta["fileName"], "display_name": meta["displayName"],
        "instances": " | ".join(occurrences[fid]), "allow_mod_distribution": p.get("allowModDistribution"),
        "null_after_recheck": not rechecked[fid].get("downloadUrl"),
        "download_url_endpoint_status": diagnostics[fid].get("status"),
        "cdn_status": last.get("status"), "size": last.get("bytesRead"),
        "expected_size": meta["fileLength"], "sha1": last.get("actualSha1"),
        "verified": last.get("success"), "cdn_url": probe["url"],
        "project_url": p.get("links", {}).get("websiteUrl"), "checked_utc": probe["checkedAt"]})

csv_write("per-instance.csv", pack_rows)
csv_write("original-manifests.csv", manifest_rows)
csv_write("fallback-files.csv", fallback_rows)
csv_write("all-local-files.csv", rows)
csv_write("unidentified-files.csv", [r for r in rows if not r["cf_file_id"]])

full = [p for p in probes.values() if p["attempts"][-1].get("fullDownload")]
normal = [p for p in probes.values() if not p["missingApiUrl"]]
bad = [p for p in probes.values() if not p["attempts"][-1]["success"]]
zone = dt.timezone(dt.timedelta(hours=8))
start = dt.datetime.fromisoformat(inv["collectedAt"]).astimezone(zone)
finish = max(dt.datetime.fromisoformat(p["checkedAt"]) for p in probes.values()).astimezone(zone)
range_probes = [json.loads(p.read_text()) for p in (audit / "probes").glob("*.json")]
range_404 = sum(not p["attempts"][-1].get("fullDownload") and p["attempts"][-1].get("status") == 404 for p in range_probes)
summary = {"source": inv["source"], "start_shanghai": start.isoformat(), "end_shanghai": finish.isoformat(),
    "instances": len(pack_rows), "instances_with_null": sum(p["null_url"] > 0 for p in pack_rows),
    "local_occurrences": len(rows), "disabled": sum(r["disabled"] for r in rows),
    "unique_local_sha1": len(inv["uniqueFiles"]), "cf_occurrences": sum(p["cf_identified"] for p in pack_rows),
    "unidentified_occurrences": sum(p["unidentified"] for p in pack_rows),
    "cf_unique_file_versions": len(files), "null_url_unique": len(nulls),
    "null_url_occurrences": sum(p["null_url"] for p in pack_rows),
    "null_url_projects": len({f["modId"] for f in nulls.values()}),
    "null_rate_unique": pct(len(nulls), len(files)),
    "null_rate_occurrences": pct(sum(p["null_url"] for p in pack_rows), sum(p["cf_identified"] for p in pack_rows)),
    "fallback_full_verified": sum(r["verified"] for r in fallback_rows),
    "fallback_download_bytes": sum(r["size"] for r in fallback_rows),
    "normal_url_verified": sum(p["attempts"][-1]["success"] for p in normal),
    "normal_full_controls": sum(not p["missingApiUrl"] for p in full),
    "all_full_verified": sum(p["attempts"][-1]["success"] for p in full),
    "full_download_bytes": sum(p["attempts"][-1]["bytesRead"] for p in full),
    "ordinary_get_failures": len(bad), "range_404": range_404, "source_file_stat_changes": changes}
(dest / "summary.json").write_text(json.dumps(summary, ensure_ascii=False, indent=2), encoding="utf-8")

lines = [
    "# CF 下载地址缺失与 CDN 回退实测报告", "",
    f"采样：2026-10-31 {start:%H:%M}–{finish:%H:%M}（北京时间）。源目录：`{inv['source']}`。", "",
    "## 结论", "",
    f"- 扫描 **{len(pack_rows)} 个现有实例、{len(rows):,} 个模组 JAR 文件条目**（含 {summary['disabled']} 个禁用文件），按 SHA-1 去重为 {len(inv['uniqueFiles']):,} 个本地文件。",
    f"- 确认 **{len(files):,} 个 CF 文件版本**；其中 **{len(nulls)} 个（{summary['null_rate_unique']}）没有 `downloadUrl`**，涉及 **{summary['null_url_projects']} 个项目、{summary['instances_with_null']}/{len(pack_rows)} 个实例**。按实例重复计数为 {summary['null_url_occurrences']}/{summary['cf_occurrences']:,}（{summary['null_rate_occurrences']}）。",
    f"- 这 {len(nulls)} 个文件再次批量查询后仍为空，其所属项目的 `allowModDistribution` 全为 `false`，专用下载地址接口全为 **HTTP 403**。本批稳定缺失与作者分发关闭标记完全重合；没有发现可支持“只是 API 偶发漏字段”的样本。",
    f"- 使用猪咪相同的文件编号/文件名规则拼接官方 CDN 地址并带项目 Key 后，**{summary['fallback_full_verified']}/{len(nulls)} 个完整下载成功，大小和 SHA-1 全部匹配**，累计 {summary['fallback_download_bytes']/1048576:.2f} MiB。由此确认：本样本存在“API 拒绝给地址，但同一项目凭证通过 CDN 仍可取得正确文件”的实际行为。",
    f"- 其余 {len(normal):,} 个带地址文件的普通 GET 检查通过 {summary['normal_url_verified']:,} 个；另有 {summary['normal_full_controls']} 个正常地址文件完整下载校验。当前未发现普通 GET 的实际不可达案例（失败 {len(bad)} 个）。**普通 GET 只读取前 1 KiB 的文件不算完整下载通过。**", "",
    "该结果证明回退确实能减少本批样本的获取失败；不能据此认定 CF 允许该用法、允许再分发全部文件，或责任只属于某一方。技术返回与服务约定是不同结论。", "",
    "## 范围与方法", "",
    "1. 只读取实例的 `mods/`、版本线索和原 CF 清单；没有执行 JAR、启动游戏或改写原实例。",
    "2. 计算完整文件 SHA-1 与 CF 使用的去空白 MurmurHash2 指纹，调用官方 `/v1/fingerprints/432`。只有 API 文件 SHA-1 与本地完整 SHA-1 一致才归入“已确认 CF”。",
    "3. 按确定的 file ID 调用安装器使用的 `/v1/mods/files`，并通过 `/v1/mods` 查询项目分发标记。对空链接再次查询批量接口及 `/v1/mods/{modId}/files/{fileId}/download-url`。",
    "4. 空链接使用现有实现的 CDN 拼接方式，向官方 HTTPS API/CDN 发送本项目获批 Key。所有回退文件完整流式读取、核对长度及 SHA-1；不落盘保存 JAR。",
    "5. 正常地址全部做普通 GET 启始下载检查并与本地文件前 1 KiB 比对；每实例选择一个小型正常文件做完整对照，跨实例重复后共 20 个不同对照文件。",
    "6. 使用项目现有代理线路，证书验证保持开启；凭证不写入报告、CSV 或响应快照。沙箱 TLS 初始化失败属于本地运行环境问题，切换正常执行环境后查询成功，不计为 CF 失败。",
    f"7. 结束后复核全部 {len(rows):,} 个源模组文件的大小及修改时间，变化数为 **{len(changes)}**。", "",
    "API 结构依据：[CF 官方 REST 文档](https://docs.curseforge.com/rest-api/)；分发标记含义依据：[Project Distribution Toggle](https://support.curseforge.com/support/solutions/articles/9000207877-project-distribution-toggle)。", "",
    "## 各实例结果", "",
    "“CF”与“空地址”按实例内文件条目统计；同一文件出现在不同实例会重复计数。未识别文件不加入 CF 分母，也不认定为作者禁止。", "",
    "| 实例 | MC | JAR | 已确认 CF | 空地址 | 空地址/CF | 回退完整校验 | 未识别 |",
    "|---|---|---:|---:|---:|---:|---:|---:|",
]
for p in pack_rows:
    lines.append(f"| {p['instance']} | {p['minecraft_version'] or '未保留'} | {p['local_jars']} | {p['cf_identified']} | {p['null_url']} | {p['null_rate']} | {p['fallback_verified']} | {p['unidentified']} |")
lines += ["", "各实例中包括普通 Fabric 实例及来源并非 CF 的整合包，不能将“22 个目录”称为“22 份完整 CF 原始发行包”。6 个禁用文件均不属于空地址案例；当前启用模组中的空地址仍为 118 条。", "",
    "## 有原 CF 清单的三例", "",
    "这三例按原 `manifest.json` 的 file ID 清单检查，可以区别原包资源与后来添加的本地模组。它们的全部清单文件都在本批确认集合中。", "",
    "| 实例 | 整合包版本 | MC | 清单文件数 | API 返回数 | 空地址 | 回退完整校验 |",
    "|---|---|---|---:|---:|---:|---:|",
]
for p in manifest_rows:
    lines.append(f"| {p['instance']} | {p['pack_version']} | {p['minecraft_version']} | {p['manifest_entries']} | {p['metadata_returned']} | {p['null_url']} | {p['fallback_verified']} |")
manifest_total = sum(p['manifest_entries'] for p in manifest_rows)
manifest_null = sum(p['null_url'] for p in manifest_rows)
lines += ["", f"三份清单合计 {manifest_total} 个文件条目，其中 {manifest_null} 个空地址（{pct(manifest_null, manifest_total)}），全部回退校验成功。**这是当前原清单重下载结果，不能还原这些文件当年安装时的分发开关或服务状态。**", "",
    "## 可核对的案例", "",
    "完整 110 项见 [fallback-files.csv](cf-audit-2026-10-31/fallback-files.csv)，每项包含文件版本名、受影响实例、项目标记、403、CDN 状态、大小及 SHA-1。以下仅列代表性项目，每行是一个具体文件版本。", "",
    "| 项目 | 文件版本名 | CF 文件 ID | 出现实例 | 地址接口 | CDN 完整校验 |",
    "|---|---|---:|---|---:|---|",
]
examples = []
seen_projects = set()
for row in sorted(fallback_rows, key=lambda r: (-len(occurrences[r['cf_file_id']]), r['cf_file_id'])):
    if row['cf_project_id'] in seen_projects:
        continue
    seen_projects.add(row['cf_project_id'])
    examples.append(row)
    if len(examples) == 10:
        break
for r in examples:
    title = f"[{r['project']}]({r['project_url']})" if r['project_url'] else r['project']
    instance_names = r['instances'].replace(' | ', '、')
    lines.append(f"| {title} | `{r['filename']}` | {r['cf_file_id']} | {instance_names} | {r['download_url_endpoint_status']} | {r['cdn_status']}，SHA-1 匹配 |")
lines += ["", "## 排除 Range 探测误判", "",
    f"初始探测向正常地址添加 `Range: bytes=0-1023`，本线路下 {range_404:,} 个此类请求均为 404；相同文件改用不带 Range 的普通 GET 后可取得与本地一致的数据。另有 {summary['normal_full_controls']} 个正常地址文件完整校验成功。**这些 404 是本次探测方式与链路的差异，未作为文件删除、作者禁用或安装失败计数。**",
    "本轮没有定位该差异发生在 CDN、代理还是其组合，也没有据此修改启动器下载器。若排查分片下载，应以实际下载器的普通 GET 回退能力另作验证。原始 Range 记录保留在证据目录 `probes/`，最终普通 GET 记录在 `get-probes/`。", "",
    "## 对回信与实现的影响", "",
    "- 分发计划为公开源码仓库，通过公开 GitHub Releases 提供二进制，不做任何形式的宣传；预计最初 4–5 人使用，而非限制只有四五人能取得程序。邮件状态见 [申请记录](../CF-API-APPLICATION.md)。",
    "- 第 2 项保留明确的应用二进制内嵌（硬编码）Key 询问。当前运行版仍从本地配置读取凭证，未因修改邮件而改动实现。",
    "- 下段是扩展研究阶段的备选表述，未用于最终邮件。已发回信第 3 项只引用三份原 CF 清单的五个文件版本，见 [主报告](CF-DOWNLOAD-AUDIT-2026-10-31.md)。", "",
    f"> In tests of {len(files):,} exact CurseForge file versions from our installed instances, {len(nulls)} returned no downloadUrl. For all of these files, allowModDistribution was false and the dedicated download-URL endpoint returned HTTP 403, while authenticated requests to the official CDN completed successfully with matching file sizes and SHA-1 hashes. Is this CDN fallback permitted in this situation, or should the launcher direct users to download these files through the CurseForge website?", "",
    "- 现有回退只能处理“已拿到文件 ID、文件名等元数据，但地址缺失”的情况。API 整体不可用且无元数据时，不能仅靠拼接保证安装成功。",
    "- 本次扩展研究保留回退与已完成的安装未完成提示，没有再次修改启动器；后续邮件状态以 [申请记录](../CF-API-APPLICATION.md)为准。", "",
    "## 限制与证据", "",
    f"- {summary['unidentified_occurrences']} 个本地文件未获得 CF SHA-1 一致的匹配；可能是其他平台、定制或修改文件，也可能无法由当前接口识别，原因未逐一查明。它们不是已证实的 CF 下载失败。",
    "- 没有重放全部整合包安装，没有测试 MC、Java、加载器、启动、依赖闭包及取消恢复；普通 GET 的 1 KiB 校验只能证明开始下载时地址可用及内容前缀一致。完整下载通过的文件仅为 110 个回退文件及 20 个正常对照。",
    "- 本次单一线路、单一时间窗口不能估计全平台长期故障率，也不能推断未来分发标记与 CDN 策略不变。", "",
    "附件：[逐实例统计](cf-audit-2026-10-31/per-instance.csv)、[原清单统计](cf-audit-2026-10-31/original-manifests.csv)、[回退文件明细](cf-audit-2026-10-31/fallback-files.csv)、[全部本地文件](cf-audit-2026-10-31/all-local-files.csv)、[未识别文件](cf-audit-2026-10-31/unidentified-files.csv)、[机器可读摘要](cf-audit-2026-10-31/summary.json)。", "",
    f"原始脱敏响应及逐文件下载记录位于 `{audit}`。证据索引见附件目录的 `evidence-index.json`；原始 API 快照不含请求头或 Key。", "",
    "复核脚本：`scripts/cf-pack-audit/Program.cs`（inventory / query / diagnose / probe / probe-get）及 `scripts/cf-pack-audit/report.py`。在线步骤需要本机项目配置；复用已有快照时不会重复请求接口。", "",
]
lines[0] = "# CF 下载地址缺失与 CDN 回退实测（扩展样本附录）"
lines.insert(2, "范围提示：本文件保留全部实例的扩展数据，不作为当前判断依据。当前主报告仅统计保留原始 CF 清单的三份整合包，见 [主报告](CF-DOWNLOAD-AUDIT-2026-10-31.md)。")
report_path = project / "reports" / "CF-DOWNLOAD-AUDIT-2026-10-31-EXTENDED.md"
report_path.write_text("\n".join(lines), encoding="utf-8")
evidence = []
for path in sorted(audit.rglob("*.json")):
    evidence.append({"file": str(path.relative_to(audit)), "bytes": path.stat().st_size,
        "sha256": hashlib.sha256(path.read_bytes()).hexdigest()})
(dest / "evidence-index.json").write_text(json.dumps(evidence, ensure_ascii=False, indent=2), encoding="utf-8")
print(json.dumps(summary, ensure_ascii=False, indent=2))
print(report_path)
