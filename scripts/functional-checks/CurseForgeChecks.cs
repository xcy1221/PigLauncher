using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using PCL;
using PCL.Core.App;
using PCL.Core.App.Configuration;
using PCL.Core.IO.Net;
using PCL.Network;

internal static partial class Checks
{
    private static void CurseForgeChecks()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "service-config.json");
        var original = File.ReadAllText(path);
        var key = Secrets.CurseForgeAPIKey;
        Run("cf-config-and-errors", () =>
        {
            try
            {
                foreach (var invalid in new[] { "{}", "{", "[]",
                    "{\"CURSEFORGE_API_KEY\":123}", "{\"CURSEFORGE_API_KEY\":\"\"}",
                    "{\"CURSEFORGE_API_KEY\":\"secret-marker\\r\\ninvalid\"}" })
                {
                    File.WriteAllText(path, invalid);
                    try { _ = Secrets.CurseForgeAPIKey; throw new Exception("Invalid config accepted"); }
                    catch (InvalidOperationException ex)
                    {
                        Require(ex.Message.Contains("CURSEFORGE_API_KEY"));
                        Require(!ex.ToString().Contains("secret-marker"));
                    }
                }
            }
            finally { File.WriteAllText(path, original); }
            Require(Secrets.CurseForgeAPIKey == key && Guid.TryParse(Secrets.MSOAuthClientId, out _));
        });
        Run("cf-header-host-boundary", () =>
        {
            var sign = typeof(RequestSigning).GetMethod("SecretHeadersSign", BindingFlags.NonPublic | BindingFlags.Static)!;
            foreach (var url in new[] { "https://api.curseforge.com/v1/mods/306612",
                "https://edge.forgecdn.net/files/test.jar", "https://mediafilez.forgecdn.net/files/test.jar",
                "https://api.modrinth.com/v2/search", "https://api.curseforge.com.example.org/",
                "http://api.curseforge.com/v1/mods/306612" })
            {
                using var message = new HttpRequestMessage(HttpMethod.Get, url);
                sign.Invoke(null, new object[] { url, message, false, "" });
                var expected = url.StartsWith("https://") && new Uri(url).Host is
                    "api.curseforge.com" or "edge.forgecdn.net" or "mediafilez.forgecdn.net";
                Require(message.Headers.Contains("x-api-key") == expected);
                if (expected) Require(message.Headers.GetValues("x-api-key").Single() == key);
            }
        });
        Config.Network.EnableDoH = false;
        var network = new NetworkService();
        network.StartAsync().GetAwaiter().GetResult();
        try
        {
            Run("cf-official-search", () =>
            {
                var result = Requester.FetchJson("https://api.curseforge.com/v1/mods/search?gameId=432&classId=6&searchFilter=Fabric%20API&sortField=6&sortOrder=desc&pageSize=5");
                File.WriteAllText(Path.Combine(Root, "evidence", "cf-search.json"), JsonSerializer.Serialize(
                    result["data"]!.AsArray().Select(p => new { id = p!["id"]!.GetValue<int>(), name = p["name"]!.GetValue<string>() })));
                Require(result["data"]!.AsArray().Any(p => p!["id"]!.GetValue<int>() == 306612));
            });
            Run("cf-core-client-project", () =>
            {
                using var client = NetworkService.GetClient(NetworkService.CurseForgeApi);
                var json = client.GetStringAsync("mods/306612").GetAwaiter().GetResult();
                var project = new ModComp.CompProject(JsonNode.Parse(json)!["data"]!.AsObject());
                Require(project.Id == "306612" && project.FromCurseForge);
            });
            Run("cf-file-list-and-download", () =>
            {
                var result = Requester.FetchJson("https://api.curseforge.com/v1/mods/306612/files?gameVersion=1.21.1&modLoaderType=4&pageSize=5");
                var data = result["data"]!.AsArray().Select(n => n!.AsObject())
                    .First(f => !string.IsNullOrWhiteSpace(f["downloadUrl"]?.ToString()) && f["fileLength"]!.GetValue<long>() < 10000000);
                var file = new ModComp.CompFile(data, ModComp.CompType.Mod);
                Require(file.FromCurseForge && file.GameVersions.Contains("1.21.1"));
                var url = data["downloadUrl"]!.GetValue<string>();
                Require(new Uri(url).Host is "edge.forgecdn.net" or "mediafilez.forgecdn.net");
                var target = Path.Combine(Root, "cf-sample.jar");
                try
                {
                    ModNet.NetDownloadByClient(url, target).GetAwaiter().GetResult();
                    var bytes = File.ReadAllBytes(target);
                    var expected = data["hashes"]!.AsArray().First(h => h!["algo"]!.GetValue<int>() == 1)!["value"]!.GetValue<string>();
                    var actual = Convert.ToHexString(System.Security.Cryptography.SHA1.HashData(bytes));
                    Require(bytes.LongLength == data["fileLength"]!.GetValue<long>() && actual.Equals(expected, StringComparison.OrdinalIgnoreCase));
                    using var jar = System.IO.Compression.ZipFile.OpenRead(target);
                    Require(jar.GetEntry("fabric.mod.json") is not null);
                    File.WriteAllText(Path.Combine(Root, "evidence", "cf-sample.json"), JsonSerializer.Serialize(new
                    { fileId = data["id"]!.GetValue<int>(), fileName = file.FileName, size = bytes.Length, sha1 = actual.ToLowerInvariant(), url }));
                }
                finally { if (File.Exists(target)) File.Delete(target); }
            });
        }
        finally { network.StopAsync().GetAwaiter().GetResult(); }
    }
}
