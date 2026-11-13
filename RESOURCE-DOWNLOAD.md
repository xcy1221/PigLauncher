# 下载策略

默认使用官方 API 与 CDN。文件、版本列表和社区资源可分别选择官方优先或镜像策略；镜像可用性受网络及服务状态影响。

## CurseForge

- 项目分发状态及文件元数据以官方 API 为准，避免旧镜像地址覆盖作者的当前选择。
- 项目禁止第三方分发或 API 未提供下载地址时，不构造 CDN 地址，改为提供官网文件页面。
- 允许下载的文件可按设置使用镜像；CF Key 不发送给镜像站。
- 整合包需要手动下载的文件记录在 `PCL/CurseForgeManualDownloads.html`，同时保留已完成的安装内容。

## Modrinth

使用项目和版本元数据获取资源，按文件校验信息验证下载内容。依赖和整合包安装失败会显示错误或未完成状态。

服务参数见 [SERVICE-CONFIGURATION.md](SERVICE-CONFIGURATION.md)，已验证范围见 [VALIDATION.md](VALIDATION.md)。
