# 猪咪启动器（PigLauncher）

基于 PCL CE 维护的 Minecraft Java 版 Windows 启动器。

[下载预览版](https://github.com/xcy1221/PigLauncher/releases/tag/v0.1.0-preview.2) · [版本说明](releases/0.1.0-preview.2.md) · [问题反馈](https://github.com/xcy1221/PigLauncher/issues)

## 主要功能

- Minecraft 实例、Java 和常用模组加载器管理。
- CurseForge / Modrinth 资源搜索与整合包安装。
- 默认官方下载源，文件、版本列表与社区资源可分别选择镜像策略。
- 猪咪粉主题、主页旋转猪与独立外观设置。
- 实例搜索、收藏，以及按游戏版本或加载器分类。

## 下载与运行

1. 下载 Release 中的 Windows x64 `PigLauncher.exe`。
2. 运行 `PigLauncher.exe`。
3. 准备 Java，或通过启动器下载所需版本。

支持 Windows 10 1809 及以上的 x64 系统；发布包已包含 .NET 运行时。文件完整性可通过 Release 中的 `SHA256SUMS.txt` 校验。

部分图片使用 WebP；系统缺少解码支持时，可能影响缩略图或背景显示。程序会提供相应提示。

## 受限文件与整合包

CurseForge 不允许第三方下载或未提供 API 下载地址的文件，需要前往官网手动下载。

整合包遇到这类文件时，会保留实例和已下载内容，标记“安装未完成”，并生成 `PCL/CurseForgeManualDownloads.html`。清单包含文件名、官网链接和放置位置；补齐前整合包可能无法正常运行。

## 从源码构建

使用项目内工具链：

```powershell
./scripts/Install-ProjectTools.ps1
./scripts/dotnet.ps1 build "dev/PCL-CE/Plain Craft Launcher 2/Plain Craft Launcher 2.csproj" -c Release '-p:Platform=x64'
```

开发环境需要 .NET SDK，固定版本见 `global.json`。脚本将工具和缓存放入 `.local/`，只设置当前进程的环境变量。

本地打包：

```powershell
./scripts/New-ReleasePackage.ps1
```

服务参数与 CI 配置见 [服务配置](SERVICE-CONFIGURATION.md)。账号、本地配置、工具链和游戏实例不纳入源码。

## 目录与文档

| 路径 | 内容 |
|---|---|
| `dev/PCL-CE/` | 启动器源码 |
| `scripts/` | 构建、打包与功能检查工具 |
| `assets/` | 品牌图片素材 |
| `releases/` | 版本说明 |

[版本规则](VERSIONING.md) · [开发路线](ROADMAP.md) · [验证范围](VALIDATION.md) · [下载策略](RESOURCE-DOWNLOAD.md) · [许可与来源](LICENSES.md)

当前版本见 [VERSION](VERSION)，维护状态见 [STATUS.md](STATUS.md)。项目由 [xcy1221](https://github.com/xcy1221) 维护，基于 PCL CE v2.15.1；上游版权和组件声明保留。
