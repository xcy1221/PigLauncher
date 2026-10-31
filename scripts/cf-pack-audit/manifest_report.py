"""Report only resources explicitly listed in the three retained CF manifests."""
import csv
import datetime as dt
import json
import pathlib
import sys

project = pathlib.Path(sys.argv[1]).resolve()
audit = pathlib.Path(sys.argv[2]).resolve()
out = project / "reports" / "cf-manifest-audit-2026-10-31"
out.mkdir(parents=True, exist_ok=True)
load = lambda name: json.loads((audit / name).read_text(encoding="utf-8-sig"))
instances = [i for i in load("inventory.json")["instances"] if i.get("manifest")]
files = {f["id"]: f for f in load("files.json")}
projects = {p["id"]: p for p in load("projects.json")}
rows, packs = [], []
ids = set()
for instance in instances:
    manifest = instance["manifest"]
    listed = manifest["files"]
    pack_null = 0
    for entry in listed:
        fid = entry["fileID"]
        ids.add(fid)
        meta = files[fid]
        assert meta["modId"] == entry["projectID"]
        probe = load(f"get-probes/{fid}.json")
        last = probe["attempts"][-1]
        assert last["success"]
        missing = not meta.get("downloadUrl")
        pack_null += missing
        diagnosis = load(f"diagnostics/{fid}.json") if missing else {}
        p = projects[meta["modId"]]
        if missing:
            assert p.get("allowModDistribution") is False
            assert diagnosis["status"] == 403
            assert last["fullDownload"] and last["bytesRead"] == meta["fileLength"]
            assert last["actualSha1"] == next(h["value"] for h in meta["hashes"] if h["algo"] == 1)
        rows.append({"instance": instance["name"], "pack_version": manifest["version"],
            "minecraft_version": manifest["minecraft"]["version"], "required": entry.get("required", True),
            "cf_project_id": meta["modId"], "project": p["name"], "cf_file_id": fid,
            "filename": meta["fileName"], "null_download_url": missing,
            "allow_mod_distribution": p.get("allowModDistribution"),
            "download_url_endpoint_status": diagnosis.get("status"), "cdn_status": last["status"],
            "verification": "full-size-and-sha1" if last["fullDownload"] else "ordinary-get-first-1KiB-sha1",
            "verified": last["success"], "bytes_read": last["bytesRead"],
            "expected_size": meta["fileLength"], "actual_sha1": last["actualSha1"],
            "expected_sha1": last["expectedSha"], "url": probe["url"], "checked_utc": probe["checkedAt"]})
    packs.append({"instance": instance["name"], "pack_version": manifest["version"],
        "minecraft_version": manifest["minecraft"]["version"], "entries": len(listed),
        "null_url": pack_null, "normal_url": len(listed)-pack_null})

def write_csv(name, values):
    with (out/name).open("w", encoding="utf-8-sig", newline="") as stream:
        writer = csv.DictWriter(stream, fieldnames=list(values[0]))
        writer.writeheader()
        writer.writerows(values)

fallback = [r for r in rows if r["null_download_url"]]
assert len({r["cf_file_id"] for r in fallback}) == len(fallback)
write_csv("manifest-files.csv", rows)
write_csv("fallback-files.csv", fallback)
write_csv("per-pack.csv", packs)
unique_null = {r["cf_file_id"] for r in fallback}
probes = {i: load(f"get-probes/{i}.json") for i in ids}
normal_full = [i for i in ids-unique_null if probes[i]["attempts"][-1]["fullDownload"]]
summary = {"scope": "retained-curseforge-manifests-only", "packs": packs,
    "manifest_entries": len(rows), "required_entries": sum(r["required"] for r in rows),
    "unique_file_versions": len(ids), "null_url_entries": len(fallback),
    "null_url_unique_versions": len(unique_null),
    "null_rate_entries": f"{100*len(fallback)/len(rows):.2f}%",
    "null_rate_unique_versions": f"{100*len(unique_null)/len(ids):.2f}%",
    "null_projects": len({r["cf_project_id"] for r in fallback}),
    "fallback_download_bytes": sum(r["bytes_read"] for r in fallback),
    "normal_full_controls": len(normal_full),
    "normal_prefix_checks": len(ids)-len(unique_null)-len(normal_full),
    "new_network_requests_for_scope_revision": 0}
(out/"summary.json").write_text(json.dumps(summary, ensure_ascii=False, indent=2), encoding="utf-8")

lines = ["# 原始 CF 清单中的下载地址缺失实测", "",
    "范围修订：仅统计指定目录中保留下来、含 CF projectID/fileID 的三份 `manifest.json`；其他实例、额外添加的模组、未识别文件全部排除。复用 2026-10-31 19:34–19:48（北京时间）的实测响应及下载证据，本次没有重复联网下载。", "",
    "## 结论", "",
    f"- 三份清单共 **{len(rows)} 条必需资源**，按 CF file ID 去重后为 **{len(ids)} 个文件版本**。每一条都取得了与清单 projectID 一致的官方文件元数据。",
    f"- **{len(fallback)} 条未提供 `downloadUrl`**：按清单条目计 **{summary['null_rate_entries']}**，按去重版本计 **{summary['null_rate_unique_versions']}**。三份整合包均至少出现一条。",
    f"- 这 {len(fallback)} 个文件涉及 **{summary['null_projects']} 个项目**，项目的 `allowModDistribution` 均为 `false`；重复批量查询仍为空，专用地址接口均返回 **403**。",
    f"- 按猪咪现有逻辑拼接官方 CDN，并带项目 Key 后，**{len(fallback)}/{len(fallback)} 个完整下载成功，文件长度与 SHA-1 全部匹配**，合计 {summary['fallback_download_bytes']:,} 字节（{summary['fallback_download_bytes']/1048576:.2f} MiB）。",
    f"- 其余 **{len(rows)-len(fallback)} 条带下载地址的资源**对应 **{len(ids)-len(unique_null)} 个去重版本**，普通 GET 可开始下载。其中 {len(normal_full)} 个做完整下载对照，其余 {summary['normal_prefix_checks']} 个读取前 1 KiB 并与本地对应文件比较。没有把这些起始下载检查称为全文件安装通过。", "",
    "本报告样本均由保留的 CF 清单直接指定，其他来源未明的本地文件不参与统计。结果显示接口拒绝给地址与 CDN 仍可取回文件同时存在；不据此判定服务条款许可或责任归属。", "",
    "## 三份清单", "",
    "| 整合包 | 清单版本 | Minecraft | 资源条目 | 有地址 | 空地址 | 回退完整校验 |",
    "|---|---|---|---:|---:|---:|---:|",
]
for p in packs:
    lines.append(f"| {p['instance']} | {p['pack_version']} | {p['minecraft_version']} | {p['entries']} | {p['normal_url']} | {p['null_url']} | {p['null_url']} |")
lines += ["", "全部清单条目的 `required` 均为 `true`。总条目 617 与去重版本 529 的差别来自跨清单共用文件；5 个空地址版本互不重复。", "",
    "## 五个具体文件", "",
    "| 整合包 | 项目 | 文件版本名 | CF 文件 ID | 地址接口 | CDN 完整校验 |",
    "|---|---|---|---:|---:|---|",
]
for r in fallback:
    lines.append(f"| {r['instance']} | {r['project']} | `{r['filename']}` | {r['cf_file_id']} | 403 | 200，大小及 SHA-1 匹配 |")
lines += ["", "逐文件完整哈希、大小、地址及检查时间见 [五项回退明细](cf-manifest-audit-2026-10-31/fallback-files.csv)。", "",
    "## 验证依据与边界", "",
    "1. 以三份原清单中的 fileID 为样本选择依据，关联已保存的官方 `/v1/mods/files` 响应，并核对响应 modId 与清单 projectID 一致；没有按本地 JAR 名称推断来源。",
    "2. 对空地址再次批量查询，并调用 `/v1/mods/{modId}/files/{fileId}/download-url`；再执行带本项目凭证的官方 CDN 普通 GET，完整读取并核对发布的长度与 SHA-1。",
    "3. 全程使用项目既有代理线路，保持证书验证；原实例没有被修改，报告和证据不含 API Key。",
    "4. 最初的 Range 探测在本线路返回 404，随后已改用普通 GET 复核。本报告仅采用最终普通 GET 结果，不将 Range 探测差异当作文件下载失败。",
    "5. 这是这些原清单在本次时间窗口的重下载检查，并非当初安装日志；不能还原当年的作者设置，也没有重装整包或验证启动、依赖完整性。", "",
    "接口字段与分发开关说明：[CF REST 文档](https://docs.curseforge.com/rest-api/)、[Project Distribution Toggle](https://support.curseforge.com/support/solutions/articles/9000207877-project-distribution-toggle)。", "",
    "## 回信第 3 项可据此收窄", "",
    "回信第 3 项采用本报告的原清单口径。以下为实测事实表述；邮件发送及回复进展见 [申请记录](../CF-API-APPLICATION.md)：", "",
    "> We tested the files listed in three retained CurseForge modpack manifests (617 entries, representing 529 distinct file versions). Five versions returned no downloadUrl; their projects had allowModDistribution set to false, and the dedicated download-URL endpoint returned HTTP 403. Authenticated downloads from the official CDN using URLs derived from the file metadata succeeded for all five, with matching file sizes and SHA-1 hashes. Is this CDN fallback permitted in this situation, or should users be directed to the CurseForge website?", "",
    "## 附件", "",
    "- [三份清单统计](cf-manifest-audit-2026-10-31/per-pack.csv)",
    "- [617 条清单资源及验证结果](cf-manifest-audit-2026-10-31/manifest-files.csv)",
    "- [五项回退文件明细](cf-manifest-audit-2026-10-31/fallback-files.csv)",
    "- [机器可读摘要](cf-manifest-audit-2026-10-31/summary.json)",
    "- [扩展取样附录](CF-DOWNLOAD-AUDIT-2026-10-31-EXTENDED.md)：保留此前其他实例的取样数据，不参与本报告结论。", "",
    "原始脱敏证据：`.local/cf-pack-audit/20261031/`；原清单位于指定源目录下对应实例的 `manifest.json`。复核脚本：`scripts/cf-pack-audit/manifest_report.py`。", "",
]
path = project/"reports"/"CF-DOWNLOAD-AUDIT-2026-10-31.md"
path.write_text("\n".join(lines), encoding="utf-8")
print(json.dumps(summary, ensure_ascii=False, indent=2))
print(path)
