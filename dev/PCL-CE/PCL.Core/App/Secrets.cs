using System;
using System.IO;
using System.Text.Json;
using PCL.Core.Utils.Exts;
using PCL.Core.Utils.OS;

namespace PCL.Core.App;

// ReSharper disable InconsistentNaming
public static class Secrets
{
    /// <summary>
    /// 微软 OAuth 的 Client ID
    /// </summary>
    public static string MSOAuthClientId => ReadMicrosoftClientId();

    private static string ReadMicrosoftClientId()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "service-config.json");
        if (File.Exists(path))
        {
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(path));
                if (document.RootElement.TryGetProperty("MS_CLIENT_ID", out var value))
                {
                    if (!Guid.TryParse(value.GetString()?.Trim(), out var clientId) || clientId == Guid.Empty)
                        throw new InvalidDataException("MS_CLIENT_ID 必须是有效的应用 Client ID。");
                    return clientId.ToString();
                }
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or InvalidOperationException)
            {
                throw new InvalidOperationException($"无法读取微软登录配置，请检查 {path} 中的 MS_CLIENT_ID。", ex);
            }
        }
        return EnvironmentInterop.GetSecret("MS_CLIENT_ID", readEnvDebugOnly: true).ReplaceNullOrEmpty();
    }

    /// <summary>
    /// CurseForge API Key，优先读取程序目录的本地配置。
    /// </summary>
    public static string CurseForgeAPIKey
    {
        get
        {
            var path = Path.Combine(AppContext.BaseDirectory, "service-config.json");
            if (File.Exists(path))
            {
                try
                {
                    using var document = JsonDocument.Parse(File.ReadAllText(path));
                    if (document.RootElement.TryGetProperty("CURSEFORGE_API_KEY", out var value))
                    {
                        var key = value.GetString()?.Trim();
                        if (string.IsNullOrEmpty(key) || System.Linq.Enumerable.Any(key, c => c < '!' || c > '~'))
                            throw new InvalidDataException("CURSEFORGE_API_KEY 必须是非空、无空白的 API Key。");
                        return key;
                    }
                }
                catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or InvalidOperationException)
                {
                    // 不包含原始异常，避免 JSON 解析错误携带 Key 片段进入日志。
                    throw new InvalidOperationException($"无法读取 CurseForge 配置，请检查 {path} 中的 CURSEFORGE_API_KEY。");
                }
            }
            var fallback = EnvironmentInterop.GetSecret("CURSEFORGE_API_KEY", readEnvDebugOnly: true).ReplaceNullOrEmpty();
            if (string.IsNullOrWhiteSpace(fallback))
                throw new InvalidOperationException($"缺少 CurseForge API Key，请在 {path} 中设置 CURSEFORGE_API_KEY。");
            return fallback;
        }
    }

    /// <summary>
    /// 遥测密钥
    /// </summary>
    public static string TelemetryKey { get; } = EnvironmentInterop.GetSecret("TELEMETRY_KEY", readEnvDebugOnly: true).ReplaceNullOrEmpty();

    /// <summary>
    /// Natayark ID OAuth 的 Client ID
    /// </summary>
    public static string NatayarkClientId { get; } = EnvironmentInterop.GetSecret("NAID_CLIENT_ID", readEnvDebugOnly: true).ReplaceNullOrEmpty();

    /// <summary>
    /// Natayark ID OAuth 的 Client ID
    /// </summary>
    public static string NatayarkClientSecret { get; } = EnvironmentInterop.GetSecret("NAID_CLIENT_SECRET", readEnvDebugOnly: true).ReplaceNullOrEmpty();

    /// <summary>
    /// 联机根服务器
    /// </summary>
    public static string[] LinkServers { get; } = EnvironmentInterop.GetSecret("LINK_SERVER_ROOT", readEnvDebugOnly: true).ReplaceNullOrEmpty().Split("|");

    /// <summary>
    /// 当前版本的 Git 提交 SHA
    /// </summary>
    public static string CommitHash { get; } = EnvironmentInterop.GetSecret("GITHUB_SHA", readEnvDebugOnly: true).ReplaceNullOrEmpty();
}
