using System.IO;
using System.Text.Json.Nodes;
using PCL;
using PCL.Core.App;
using static PCL.ModLoader;

internal static partial class Checks
{
    private static void ModpackChecks()
    {
        var gameRoot = Path.Combine(Root, "instances") + "\\";
        ModFolder.mcFolderSelected = gameRoot;
        var path = Path.Combine(gameRoot, "versions", "1.21.1") + "\\";
        Directory.CreateDirectory(Path.Combine(path, "mods"));
        var versionJson = Path.Combine(path, "1.21.1.json");
        File.WriteAllText(versionJson, """
            {"id":"1.21.1","type":"release","releaseTime":"2024-08-08T00:00:00Z",
             "time":"2024-08-08T00:00:00Z","mainClass":"net.minecraft.client.main.Main",
             "libraries":[],"arguments":{"game":[],"jvm":[]},"assets":"17"}
            """);
        var installedMod = Path.Combine(path, "mods", "already-downloaded.jar");
        File.WriteAllText(installedMod, "completed mod sentinel");

        Run("modpack-base-before-resources", () =>
        {
            var game = new LoaderTask<string, int>("fixture game", _ => { });
            var resources = new LoaderTask<string, int>("fixture resources", _ => { });
            var finish = new LoaderTask<string, int>("fixture finalize", _ => { });
            var loader = ModpackInstallStatus.CreateLoader("fixture install", path, "1.21.1", "2.0",
                "CurseForge", "42", [game], [resources], finish);
            Require(((LoaderCombo)loader.loaders[0]).loaders.Single() == game && loader.loaders[0].block);
            Require(((LoaderCombo)loader.loaders[1]).loaders.Single() == resources && loader.loaders[1].block);
            Require(loader.loaders[2] == finish);
        });
        Run("modpack-core-failure-not-preserved", () =>
        {
            Require(!ModpackInstallStatus.ShouldKeepInstance(ModBase.LoadState.Failed,
                ModBase.LoadState.Failed, ModBase.LoadState.Waiting));
            Require(!ModpackInstallStatus.ShouldKeepInstance(ModBase.LoadState.Failed,
                ModBase.LoadState.Loading, ModBase.LoadState.Failed));
            Require(!File.Exists(Path.Combine(path, "PCL", ModpackInstallStatus.FailureReportName)));
        });
        Run("modpack-resource-failure-keeps-version", () =>
        {
            Require(ModpackInstallStatus.ShouldKeepInstance(ModBase.LoadState.Failed,
                ModBase.LoadState.Finished, ModBase.LoadState.Failed));
            File.WriteAllText(Path.Combine(path, ".pclignore"), "installation in progress");
            ModpackInstallStatus.SaveIncomplete(path, "1.21.1", "2.0", "CurseForge", "42",
                new IOException("fixture CDN download failed"), ["examplemod-1.21.1-2.0.jar"]);
            Require(File.Exists(versionJson) && File.ReadAllText(installedMod) == "completed mod sentinel");
            Require(!File.Exists(Path.Combine(path, ".pclignore")));
            Require(States.Instance.ModpackVersion[path] == "2.0" && States.Instance.ModpackSource[path] == "CurseForge");
            var instance = new McInstance(path).Load();
            Require(instance.state != McInstanceState.Error && instance.Info.VanillaName == "1.21.1");
            Require(instance.Desc.Contains("安装未完成") && instance.Desc.Contains("1.21.1"));
            var report = File.ReadAllText(Path.Combine(path, "PCL", ModpackInstallStatus.FailureReportName));
            Require(report.Contains("examplemod-1.21.1-2.0.jar") && report.Contains("fixture CDN download failed"));
        });
        Run("modpack-cancel-and-success-keep-distinct", () =>
        {
            Require(!ModpackInstallStatus.ShouldKeepInstance(ModBase.LoadState.Aborted,
                ModBase.LoadState.Finished, ModBase.LoadState.Aborted));
            Require(!ModpackInstallStatus.ShouldKeepInstance(ModBase.LoadState.Finished,
                ModBase.LoadState.Finished, ModBase.LoadState.Finished));
            ModpackInstallStatus.ClearIncomplete(path);
            Require(!File.Exists(Path.Combine(path, "PCL", ModpackInstallStatus.FailureReportName)));
            Require(States.Instance.CustomInfo[path] == "" && File.Exists(installedMod));
            States.Instance.CustomInfo[path] = "my pack description";
            ModpackInstallStatus.ClearIncomplete(path);
            Require(States.Instance.CustomInfo[path] == "my pack description");
        });
        Run("curseforge-null-url-keeps-official-cdn-fallback", () =>
        {
            var metadata = JsonNode.Parse("""
                {"id":1234567,"gameId":432,"modId":42,"displayName":"Example mod 2.0","fileName":"examplemod-1.21.1-2.0.jar",
                 "downloadCount":1,"fileDate":"2026-10-31T00:00:00Z","releaseType":1,"downloadUrl":null,
                 "hashes":[{"algo":1,"value":"0000000000000000000000000000000000000000"}],
                 "dependencies":[],"gameVersions":["1.21.1","Forge"]}
                """)!.AsObject();
            var file = new ModComp.CompFile(metadata, ModComp.CompType.Mod);
            Require(file.Available && file.DownloadUrls.Any(url =>
                url == "https://edge.forgecdn.net/files/1234/567/examplemod-1.21.1-2.0.jar"));
            file.DownloadUrls.Clear();
            Require(!file.Available);
        });
    }
}
