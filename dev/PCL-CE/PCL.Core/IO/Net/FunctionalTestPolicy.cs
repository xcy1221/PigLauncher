using System;
using System.IO;
using System.Net.Http;
using System.Net;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PCL.Core.IO.Net;

// The built-in source host allowlist and network audit apply only to isolated tests.
public static class FunctionalTestPolicy
{
    public static string? Root => Environment.GetEnvironmentVariable("PIGLAUNCHER_TEST_ROOT");
    private static readonly object Gate = new();
    public static IWebProxy? CreateProxy()
    {
        var address = Environment.GetEnvironmentVariable("PIGLAUNCHER_TEST_PROXY");
        if (string.IsNullOrEmpty(Root) || string.IsNullOrWhiteSpace(address)) return null;
        var bypass = Environment.GetEnvironmentVariable("PIGLAUNCHER_TEST_PROXY_BYPASS") ?? "";
        return new WebProxy(address)
        {
            BypassList = bypass.Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(System.Text.RegularExpressions.Regex.Escape).ToArray()
        };
    }
    public static void Check(string url)
    {
        if (string.IsNullOrEmpty(Root)) return;
        var uri = new Uri(url);
        var host = uri.Host;
        var allowed = uri.Scheme == "https" && host is
            "launchermeta.mojang.com" or "launcher.mojang.com" or
            "piston-meta.mojang.com" or "piston-data.mojang.com" or
            "resources.download.minecraft.net" or "libraries.minecraft.net" or
            "login.microsoftonline.com" or "user.auth.xboxlive.com" or
            "xsts.auth.xboxlive.com" or "api.minecraftservices.com" or
            "api.mojang.com" or "sessionserver.mojang.com" or "textures.minecraft.net" or
            "meta.fabricmc.net" or "maven.fabricmc.net" or
            "meta2.fabricmc.net" or "maven2.fabricmc.net" or
            "files.minecraftforge.net" or "maven.minecraftforge.net" or
            "maven.neoforged.net" or "meta.quiltmc.org" or "maven.quiltmc.org" or
            "api.modrinth.com" or "cdn.modrinth.com" or
            "bmclapi2.bangbang93.com" or "mod.mcimirror.top" or "alist.8mi.tech" or
            "api.curseforge.com" or "edge.forgecdn.net" or
            "mediafilez.forgecdn.net" or "media.forgecdn.net" or
            "optifine.net" or "optifined.net" or
            "releases.labymod.net" or "releases.r2.labymod.net" or
            "meta.legacyfabric.net" or "dl.liteloader.com";
        lock (Gate)
            File.AppendAllText(Path.Combine(Root!, "evidence", "network.jsonl"),
                System.Text.Json.JsonSerializer.Serialize(new { url, allowed }) + "\n");
        if (!allowed) throw new HttpRequestException("Functional test blocked unlisted source URL: " + host);
    }
}

internal sealed class FunctionalTestHandler : DelegatingHandler
{
    public FunctionalTestHandler(HttpMessageHandler inner) : base(inner) { }
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var current = request;
        try
        {
            for (var redirects = 0; ; redirects++)
            {
                FunctionalTestPolicy.Check(current.RequestUri!.AbsoluteUri);
                var response = await base.SendAsync(current, ct).ConfigureAwait(false);
                var code = (int)response.StatusCode;
                if (string.IsNullOrEmpty(FunctionalTestPolicy.Root) || response.Headers.Location is null ||
                    code is not (301 or 302 or 303 or 307 or 308)) return response;
                if (redirects >= 10 || (current.Method != HttpMethod.Get && current.Method != HttpMethod.Head))
                {
                    response.Dispose();
                    throw new HttpRequestException("Unsupported redirect or redirect limit exceeded");
                }
                var target = new Uri(current.RequestUri, response.Headers.Location);
                response.Dispose();
                FunctionalTestPolicy.Check(target.AbsoluteUri);
                var next = new HttpRequestMessage(current.Method, target)
                { Version = current.Version, VersionPolicy = current.VersionPolicy };
                foreach (var header in current.Headers)
                {
                    if (header.Key.Equals("Host", StringComparison.OrdinalIgnoreCase)) continue;
                    if (target.Authority != current.RequestUri.Authority &&
                        (header.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase) ||
                         header.Key.Equals("Cookie", StringComparison.OrdinalIgnoreCase) ||
                         header.Key.Equals("x-api-key", StringComparison.OrdinalIgnoreCase))) continue;
                    next.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
                if (current != request) current.Dispose();
                current = next;
            }
        }
        finally { if (current != request) current.Dispose(); }
    }
}
