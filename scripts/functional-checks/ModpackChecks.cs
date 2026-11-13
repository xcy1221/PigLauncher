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
        JsonObject FileMetadata(string? url = null) => JsonNode.Parse("""
            {"id":1234567,"gameId":432,"modId":42,"displayName":"Example mod 2.0","fileName":"examplemod-1.21.1-2.0.jar",
             "downloadCount":1,"fileDate":"2026-10-31T00:00:00Z","releaseType":1,"downloadUrl":null,
             "hashes":[{"algo":1,"value":"0000000000000000000000000000000000000000"}],
             "dependencies":[],"gameVersions":["1.21.1","Forge"]}
            """)!.AsObject().WithUrl(url);
        Run("curseforge-missing-url-requires-website", () =>
        {
            foreach (var url in new string?[] { null, "", "  " })
            {
                var file = new ModComp.CompFile(FileMetadata(url), ModComp.CompType.Mod);
                Require(!file.Available && file.RequiresManualDownload && file.DownloadUrls.Count == 0);
                Require(file.ManualDownloadUrl == "https://www.curseforge.com/projects/42/files/1234567");
                try { file.ToNetFile(Path.Combine(path, "mods") + "\\"); }
                catch (InvalidOperationException ex) { Require(ex.Message.Contains(file.ManualDownloadUrl)); continue; }
                throw new Exception("Restricted file reached the downloader");
            }
        });
        Run("curseforge-disabled-distribution-rejects-existing-url", () =>
        {
            var file = new ModComp.CompFile(FileMetadata("https://edge.forgecdn.net/files/1234/567/example.jar"),
                ModComp.CompType.Mod, false);
            Require(file.RequiresManualDownload && !file.Available && file.DownloadUrls.Count == 0);
        });
        Run("curseforge-allowed-file-preserves-api-url", () =>
        {
            const string url = "https://edge.forgecdn.net/files/1234/567/example.jar";
            var file = new ModComp.CompFile(FileMetadata(url), ModComp.CompType.Mod, true);
            Require(file.Available && !file.RequiresManualDownload && file.DownloadUrls.Contains(url));
            Require(file.ToNetFile(Path.Combine(path, "mods") + "\\").Urls.Contains(url));
            var restored = new ModComp.CompFile(file.ToJson(), ModComp.CompType.Mod);
            Require(restored.Available && restored.ProjectId == "42");
        });
        Run("curseforge-old-cache-cannot-restore-fabricated-url", () =>
        {
            var file = new ModComp.CompFile(FileMetadata("https://edge.forgecdn.net/files/1234/567/example.jar"),
                ModComp.CompType.Mod);
            var oldCache = file.ToJson();
            oldCache.Remove("CurseForgeDownloadPolicy");
            var restored = new ModComp.CompFile(oldCache, ModComp.CompType.Mod);
            Require(restored.RequiresManualDownload && !restored.Available && restored.DownloadUrls.Count == 0);
        });
        Run("curseforge-restricted-cache-stays-restricted", () =>
        {
            var file = new ModComp.CompFile(FileMetadata(), ModComp.CompType.Mod);
            var restored = new ModComp.CompFile(file.ToJson(), ModComp.CompType.Mod);
            Require(restored.RequiresManualDownload && restored.ProjectId == "42" && restored.DownloadUrls.Count == 0);
        });
        Run("curseforge-manual-report-links-and-destination", () =>
        {
            var file = new ModComp.CompFile(FileMetadata(), ModComp.CompType.Mod);
            var folder = Path.Combine(path, "mods", "<folder>");
            ModpackInstallStatus.SaveManualDownloads(path, [(file, folder)]);
            var reportPath = Path.Combine(path, "PCL", ModpackInstallStatus.ManualDownloadReportName);
            var report = File.ReadAllText(reportPath);
            Require(report.Contains(file.FileName) && report.Contains(file.ManualDownloadUrl));
            Require(report.Contains("&lt;folder&gt;") && !report.Contains("<folder>"));
            Require(File.ReadAllText(installedMod) == "completed mod sentinel");
            ModpackInstallStatus.ClearIncomplete(path);
            Require(!File.Exists(reportPath) && File.Exists(installedMod));
        });
        Run("curseforge-official-permissions-ignore-mirror-preference", () =>
        {
            var method = typeof(ModDownload).GetMethod("DlModRequestSources",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
            var previous = Config.Download.Comp.CompSourceSolution;
            try
            {
                Config.Download.Comp.CompSourceSolution = 0;
                foreach (var url in new[] { "https://api.curseforge.com/v1/mods/42",
                             "https://api.curseforge.com/v1/mods/files",
                             "https://api.curseforge.com/v1/mods/42/files?pageSize=10000" })
                {
                    var sources = (List<KeyValuePair<string, int>>)method.Invoke(null, [url, true])!;
                    Require(sources.Count == 1 && sources[0].Key == url);
                }
            }
            finally { Config.Download.Comp.CompSourceSolution = previous; }
        });
        Run("curseforge-project-opt-out-and-file-page-survive-cache", () =>
        {
            var project = new ModComp.CompProject(new JsonObject
            {
                ["DataSource"] = "CurseForge", ["Type"] = (int)ModComp.CompType.Mod,
                ["Slug"] = "example", ["Id"] = "43", ["RawName"] = "Example",
                ["Description"] = "fixture", ["Website"] = "https://www.curseforge.com/minecraft/mc-mods/example",
                ["DownloadCount"] = 1, ["Tags"] = new JsonArray(), ["AllowModDistribution"] = false
            });
            var restoredProject = new ModComp.CompProject(project.ToJson());
            Require(restoredProject.AllowModDistribution == false);
            var metadata = FileMetadata("https://edge.forgecdn.net/files/1234/567/example.jar");
            metadata["modId"] = 43;
            var file = new ModComp.CompFile(metadata, ModComp.CompType.Mod);
            Require(file.RequiresManualDownload && !file.Available && file.DownloadUrls.Count == 0);
            Require(file.ManualDownloadUrl == project.Website + "/files/1234567");
            file.DownloadUrls.Add("https://edge.forgecdn.net/files/1234/567/example.jar");
            Require(!file.Available);
            var previouslyAllowed = new ModComp.CompFile(metadata, ModComp.CompType.Mod, true).ToJson();
            var restoredFile = new ModComp.CompFile(previouslyAllowed, ModComp.CompType.Mod);
            Require(restoredFile.RequiresManualDownload && !restoredFile.Available);
        });
    }

    private static JsonObject WithUrl(this JsonObject metadata, string? url)
    {
        metadata["downloadUrl"] = url;
        return metadata;
    }
}
