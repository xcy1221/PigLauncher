# 服务配置

官方发布包包含运行所需的项目服务配置。自行构建时，可在程序同目录创建 `service-config.json`：

```json
{
  "MS_CLIENT_ID": "<your-microsoft-application-client-id>",
  "CURSEFORGE_API_KEY": "<your-curseforge-api-key>"
}
```

该文件中的字段优先于内置配置。请使用自己的有效参数，不要将真实配置提交到仓库；`service-config.json` 已加入忽略规则。

## 本地开发

项目脚本从 `.local/service-config.json` 读取服务参数。独立登录检查默认不发起网络请求：

```powershell
./scripts/Test-MicrosoftLogin.ps1
```

显式添加 `-Run` 才开始登录测试，账号授权在微软官方页面完成。

## GitHub Actions

构建工作流使用以下仓库 Secrets：

| Secret | 用途 |
|---|---|
| `MS_CLIENT_ID` | 微软应用标识 |
| `CURSEFORGE_API_KEY` | CurseForge API 凭证 |

推送版本标签后，Actions 构建 Windows x64 包并生成 Release 草稿。核验版本、附件和校验文件后再发布。

本地凭证构建使用 `New-ReleasePackage.ps1 -EmbedServiceCredentials`，从当前进程的 `PCL_MS_CLIENT_ID` 与 `PCL_CURSEFORGE_API_KEY` 环境变量读取参数；不要在命令文本中写入真实 Key。
