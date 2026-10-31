using System.IO;
using System.Text.Json;
using PCL;
using PCL.Core.App;
using PCL.Core.App.Configuration;
using PCL.Core.App.Configuration.Storage;
using PCL.Core.IO.Net;
using PCL.Core.Minecraft.Java.Parser;
using PCL.Core.Minecraft.Profile;
using PCL.Core.Minecraft.Profile.Models;
using PCL.Network;

internal static partial class Checks
{
    private static string Root = null!;
    private static int Failures;
    [STAThread]
    public static int Main(string[] args)
    {
        Root = Environment.GetEnvironmentVariable("PIGLAUNCHER_TEST_ROOT")
            ?? throw new InvalidOperationException("An isolated run root is required");
        var app = new System.Windows.Application();
        app.Resources.MergedDictionaries.Add(new System.Windows.ResourceDictionary
        {
            Source = new Uri("/PCL.Core;component/App/Localization/Languages/zh-CN.xaml", UriKind.Relative)
        });
        var config = new ConfigService();
        config.StartAsync().GetAwaiter().GetResult();
        if (args.Contains("--modpack-only"))
        {
            ModpackChecks();
            return Finish();
        }
        if (args.Contains("--settings-only"))
        {
            SettingsChecks();
            return Finish();
        }
        if (args.Contains("--source-only"))
        {
            SourceChecks();
            return Finish();
        }
        if (args.Contains("--webp-only"))
        {
            WebpChecks();
            return Finish();
        }
        if (args.Contains("--curseforge-only"))
        {
            CurseForgeChecks();
            return Finish();
        }
        if (args.Contains("--forge-only") || args.Contains("--module-only"))
        {
            Run("bundled-multiplayer-removed", () =>
            {
                var launcher = typeof(ModMain).Assembly;
                var core = typeof(Basics).Assembly;
                Require(!launcher.GetTypes().Any(t => t.Name is "ModLink" or "ModWebServer" or "PageToolsGameLink" or "PageSetupGameLink"));
                Require(!core.GetTypes().Any(t => t.Namespace?.StartsWith("PCL.Core.Link.") == true && !t.Namespace.StartsWith("PCL.Core.Link.McPing")));
                Require(typeof(PCL.Core.Link.McPing.McPingService).Assembly == core);
                Require(!Enum.GetNames<FormMain.PageSubType>().Any(n => n is "ToolsGameLink" or "SetupGameLink"));
            });
            if (args.Contains("--module-only")) return Finish();
            Config.Network.EnableDoH = false;
            var network = new NetworkService();
            network.StartAsync().GetAwaiter().GetResult();
            var requestedGame = args.FirstOrDefault(a => a.StartsWith("--forge-game="))?.Split('=', 2)[1];
            if (requestedGame is null)
                Run("forge-supported-game-list", () =>
                {
                    ModDownload.dlForgeListLoader.WaitForExitTime(65000, 1);
                    Require(ModDownload.dlForgeListLoader.output.Value.Contains("26.3"));
                });
            foreach (var game in requestedGame is null ? new[] { "26.3", "1.21.1" } : new[] { requestedGame })
                Run("forge-versions-" + game, () =>
                {
                    var timer = System.Diagnostics.Stopwatch.StartNew();
                    var loader = new ModLoader.LoaderTask<string, List<ModDownload.DlForgeVersionEntry>>(
                        "Forge regression " + game, ModDownload.DlForgeVersionMain);
                    loader.WaitForExitTime(65000, game);
                    Require(loader.output.Count > 0 && loader.output.All(v => v.Inherit == game));
                    Require(loader.output.Any(v => v.Category == "installer" && !string.IsNullOrEmpty(v.Hash)));
                    Record("forge-version-details", "info", $"game={game}; count={loader.output.Count}; first={loader.output[0].VersionName}; elapsedMs={timer.ElapsedMilliseconds}");
                });
            network.StopAsync().GetAwaiter().GetResult();
            return Finish();
        }
        if (args.Contains("--fabric-only"))
        {
            Config.Network.EnableDoH = false;
            var network = new NetworkService();
            network.StartAsync().GetAwaiter().GetResult();
            Run("fabric-list-recheck", () =>
            {
                ModDownload.dlFabricListLoader.WaitForExitTime(65000, 1);
                var value = ModDownload.dlFabricListLoader.output.Value;
                Require(value["loader"]!.AsArray().Count > 0 && value["game"]!.AsArray().Count > 0 && value["installer"]!.AsArray().Count > 0);
                Record("fabric-list-details", "info", $"loaders={value["loader"]!.AsArray().Count}; game26.3={value["game"]!.AsArray().Any(v => v!["version"]!.ToString() == "26.3")}");
            });
            network.StopAsync().GetAwaiter().GetResult();
            return Finish();
        }
        Run("offline-profile-without-microsoft", () =>
        {
            var profiles = new ProfileService();
            profiles.StartAsync().GetAwaiter().GetResult();
            Require(ProfileService.Profiles.Count == 0 && !ProfileService.HasMicrosoftProfile);
            Require(ProfileUi.CanCreateOtherProfile());
            var uuid = ProfileUi.GetOfflineUuid("PigTester");
            ProfileService.Add(new McProfile
            {
                ProfileType = ProfileType.Offline, UserName = "PigTester", Uuid = uuid,
                AccessToken = uuid, ClientToken = uuid, ProfileId = Guid.NewGuid().ToString("N")
            });
            Require(ProfileService.Current?.ProfileType == ProfileType.Offline);
            Require(string.IsNullOrEmpty(ProfileUi.IsProfileValid()));
            var restored = new ProfileManagement<McProfile>();
            restored.LoadFromString(Config.System.Profiles);
            Require(restored.Current?.UserName == "PigTester" && restored.Current.Uuid == uuid);
            profiles.StopAsync().GetAwaiter().GetResult();
        });
        Run("branding-metadata", () => Require(Basics.Metadata.Name == "猪咪启动器（PigLauncher）"));
        Run("official-source-selection", () =>
        {
            var sample = "https://libraries.minecraft.net/example.jar";
            Require(ModDownload.DlSourceLibraryGet(sample).SequenceEqual(new[] { sample }));
            var file = new DownloadFile(new[] { "https://bmclapi2.bangbang93.com/example.jar", sample }, Path.Combine(Root, "sample.jar"));
            Require(file.Urls.SequenceEqual(new[] { sample }));
            Require(ModDownload.DlSourceModDownloadGet("https://cdn.modrinth.com/sample.jar").Count == 1);
            try { FunctionalTestPolicy.Check("https://unlisted.example/example"); }
            catch (System.Net.Http.HttpRequestException) { return; }
            throw new Exception("Source guard did not reject the unlisted URL");
        });
        Run("settings-storage-roundtrip", () =>
        {
            var path = Path.Combine(Root, "config", "roundtrip.json");
            var first = new JsonFileProvider(path);
            first.Set("java", "sample-java");
            first.Set("threads", 3);
            first.Sync();
            var second = new JsonFileProvider(path);
            Require(second.Get<string>("java") == "sample-java" && second.Get<int>("threads") == 3);
        });
        Run("file-checker-rejects-corruption", () =>
        {
            var path = Path.Combine(Root, "checksum-sample.bin");
            var bytes = Enumerable.Range(0, 2048).Select(i => (byte)i).ToArray();
            File.WriteAllBytes(path, bytes);
            var sha1 = Convert.ToHexString(System.Security.Cryptography.SHA1.HashData(bytes)).ToLowerInvariant();
            var checker = new ModBase.FileChecker(actualSize: bytes.Length, hash: sha1);
            Require(checker.Check(path) is null);
            bytes[0] ^= 1;
            File.WriteAllBytes(path, bytes);
            Require(checker.Check(path) is not null);
        });
        Run("instance-path-isolation", () =>
        {
            ModFolder.mcFolderSelected = Path.Combine(Root, "instances") + "\\";
            var a = new McInstance("sample-a");
            var b = new McInstance("sample-b");
            Directory.CreateDirectory(a.PathInstance);
            Directory.CreateDirectory(b.PathInstance);
            Config.Instance.IndieV2[a.PathInstance] = true;
            Config.Instance.IndieV2[b.PathInstance] = true;
            Require(a.PathIndie == a.PathInstance && b.PathIndie == b.PathInstance && a.PathIndie != b.PathIndie);
            Directory.CreateDirectory(Path.Combine(a.PathIndie, "mods"));
            File.WriteAllText(Path.Combine(a.PathIndie, "mods", "sentinel.txt"), "a");
            Require(!File.Exists(Path.Combine(b.PathIndie, "mods", "sentinel.txt")));
        });
        if (args.Length > 0) Run("mod-recognition-disabled-suffix", () =>
        {
            var target = Path.Combine(Root, "sample-mod.jar");
            File.Copy(args[0], target, true);
            var mod = new ModLocalComp.LocalCompFile(target);
            mod.Load();
            Require(mod.ModId == "fabric-api" && mod.State == ModLocalComp.LocalCompFile.LocalFileStatus.Fine);
            File.Move(target, target + ".disabled");
            var disabled = new ModLocalComp.LocalCompFile(target + ".disabled");
            Require(disabled.ModId == "fabric-api" && disabled.State == ModLocalComp.LocalCompFile.LocalFileStatus.Disabled);
            File.Move(target + ".disabled", target);
            Require(new ModLocalComp.LocalCompFile(target).State == ModLocalComp.LocalCompFile.LocalFileStatus.Fine);
        });
        if (args.Length > 1) Run("java-identification", () =>
        {
            var java = new PeHeaderParser().Parse(args[1]);
            Require(java is not null && java.IsStillAvailable && java.MajorVersion >= 8);
            Record("java-details", "info", java!.ToDetailedString());
        });
        if (args.Contains("--network-loaders"))
        {
            Config.Network.EnableDoH = false;
            var network = new NetworkService();
            network.StartAsync().GetAwaiter().GetResult();
            Run("official-redirect", () =>
                Require(Requester.FetchString("https://files.minecraftforge.net/maven/net/minecraftforge/forge/index_1.2.4.html").Contains("1.2.4")));
            Run("forge-list", () =>
            {
                ModDownload.dlForgeListLoader.WaitForExitTime(65000, 1);
                Require(ModDownload.dlForgeListLoader.output.Value.Count > 0);
            });
            Run("fabric-list", () =>
            {
                ModDownload.dlFabricListLoader.WaitForExitTime(65000, 1);
                Require(ModDownload.dlFabricListLoader.output.Value["loader"]!.AsArray().Count > 0);
            });
            Run("neoforge-list", () => ModDownload.dlNeoForgeListLoader.WaitForExitTime(65000, 1));
            Run("labymod-list", () => ModDownload.dlLabyModListLoader.WaitForExitTime(65000, 1));
            Run("optifine-list", () =>
            {
                ModDownload.dlOptiFineListLoader.WaitForExitTime(65000, 1);
                Require(ModDownload.dlOptiFineListLoader.output.Value.Count > 0);
            });
            network.StopAsync().GetAwaiter().GetResult();
        }
        return Finish();
    }
    private static int Finish()
    {
        foreach (var source in new[] { ConfigSource.Shared, ConfigSource.Local, ConfigSource.GameInstance })
            ((ConfigStorage)ConfigService.GetProvider(source)).Stop();
        return Failures == 0 ? 0 : 1;
    }
    private static void Require(bool condition) { if (!condition) throw new Exception("Assertion failed"); }
    private static void Run(string name, Action test)
    {
        try { test(); Record(name, "pass", "Launcher engine/storage invocation; not a UI acceptance test"); }
        catch (Exception ex) { Failures++; Record(name, "fail", ex.ToString()); }
    }
    private static void Record(string name, string status, string detail)
    {
        var json = JsonSerializer.Serialize(new { name, status, detail });
        File.AppendAllText(Path.Combine(Root, "evidence", "engine-checks.jsonl"), json + "\n");
        Console.WriteLine(json);
    }
}
