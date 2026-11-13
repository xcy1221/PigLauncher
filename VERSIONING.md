# 版本规则

PigLauncher 独立维护版本号，手动递增；当前版本为 `0.1.0-preview.2`。

| 部分 | 递增时机 |
|---|---|
| 主版本 | 重大改版；`0` 表示项目仍处于早期开发阶段 |
| 次版本 | 新增一轮功能 |
| 修订号 | 已发布稳定版本的问题修复 |
| `preview.N` | 同一目标版本的第 N 次预览，N 从 1 开始 |

例：`0.1.0-preview.1` → `0.1.0-preview.2` → `0.1.0`；之后修复问题为 `0.1.1`，下一轮功能为 `0.2.0-preview.1`。数字不使用前导零。

根目录 `VERSION` 是仓库版本索引；修改下述元数据和主程序版本时同步该文件。维护状态另见 [STATUS.md](STATUS.md)。

## 设置与构建

1. 修改 `dev/PCL-CE/Plain Craft Launcher 2/metadata.json`：`base` 为 `x.y.z`，`suffix` 为 `preview.N`；稳定版将 `suffix` 设为 `""`。
2. 同步主程序 `csproj`：`Version` 为完整版本，`AssemblyVersion` 和 `FileVersion` 为 `x.y.z.0`。`upstream`、`code` 是上游及内部信息，不按此规则自动递增。
3. 新建对应的 `releases/<完整版本>.md`，提交源码并推送 `v<完整版本>` 标签。
4. Actions 从标签构建，校验版本一致性，直接发布 `PigLauncher.exe` 和 `SHA256SUMS.txt`；不生成 ZIP、README、build-info 或 licenses 目录。也可手动运行 `Build release` 并选择已有标签，构建使用该标签中的脚本。

预览版设置 GitHub `prerelease=true`，稳定版设置 `prerelease=false`；两者均创建或更新 **Release 草稿**，不会自动公开，也不会覆盖已发布的 Release。核验后手动发布；已发布版本的标签保持固定，既有 `v0.1.0-preview.1` 保留原有构建。

仅调整下载形式且 EXE 内容不变时，可人工更新现有 Release 附件和校验文件，不移动标签或递增版本。程序或内嵌配置发生变化时发布新版本。
