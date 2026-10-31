using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Windows.Controls;
using PCL;
using PCL.Core.App;
using PCL.Core.IO.Net;
using PCL.Network;

internal static partial class Checks
{
    private static void SourceChecks()
    {
        const string official = "https://libraries.minecraft.net/example.jar";
        const string mirror = "https://bmclapi2.bangbang93.com/maven/example.jar";
        const string mod = "https://cdn.modrinth.com/data/example.jar";
        const string api = "https://api.curseforge.com/v1/mods/search";
        Run("source-defaults-official", () => Require(Config.Download.FileSource == 2 &&
            Config.Download.VersionListSource == 2 && Config.Download.Comp.CompSourceSolution == 2));
        Run("source-file-and-community-routing", () =>
        {
            var requestSources = typeof(ModDownload).GetMethod("DlModRequestSources", BindingFlags.Static | BindingFlags.NonPublic)!;
            foreach (var mode in new[] { 0, 1, 2 })
            {
                Config.Download.FileSource = Config.Download.VersionListSource = Config.Download.Comp.CompSourceSolution = mode;
                var groups = new[] {
                    ModDownload.DlSourceLibraryGet(official).ToList(),
                    ModDownload.DlSourceLibraryGet("https://maven.fabricmc.net/example.jar").ToList(),
                    ModDownload.DlSourceLibraryGet("https://maven.neoforged.net/releases/example.jar").ToList(),
                    ModDownload.DlSourceLauncherOrMetaGet("https://piston-data.mojang.com/example.jar").ToList(),
                    ModDownload.DlSourceAssetsGet("https://resources.download.minecraft.net/example").ToList(),
                    ModDownload.DlVersionListOrder(new[] { official }, new[] { mirror }).ToList(),
                    ModDownload.DlSourceModDownloadGet(mod),
                    ModDownload.DlSourceModDownloadGet("https://mediafilez.forgecdn.net/files/example.jar") };
                foreach (var urls in groups)
                {
                    Require(urls.Any(u => !OfficialSources.IsMirror(u)));
                    Require(urls.Any(OfficialSources.IsMirror) == (mode != 2));
                    if (mode == 0) Require(OfficialSources.IsMirror(urls[0]));
                }
                var selected = new DownloadFile(new[] { official, mirror }, Path.Combine(Root, "unused.jar")).Urls;
                Require(selected.Count == (mode == 2 ? 1 : 2));
                if (mode == 0) Require(selected[0] == mirror);
                var requests = (List<KeyValuePair<string, int>>)requestSources.Invoke(null, new object[] { api, true })!;
                Require(requests.Any(p => OfficialSources.IsMirror(p.Key)) == (mode != 2));
                Require(OfficialSources.IsMirror(requests[0].Key) == (mode == 0));
                var officialOnly = (List<KeyValuePair<string, int>>)requestSources.Invoke(null, new object[] { api, false })!;
                Require(officialOnly.Count == 1 && officialOnly[0].Key == api);
            }
        });
        Run("source-settings-independent", () =>
        {
            Config.Download.FileSource = 0;
            Config.Download.Comp.CompSourceSolution = 2;
            Require(OfficialSources.SelectDownloads(new[] { official, mirror })[0] == mirror);
            Require(ModDownload.DlSourceModDownloadGet(mod).SequenceEqual(new[] { mod }));
            Config.Download.FileSource = 2;
            Config.Download.Comp.CompSourceSolution = 0;
            Require(OfficialSources.SelectDownloads(new[] { mirror, official }).SequenceEqual(new[] { official }));
            Require(OfficialSources.IsMirror(OfficialSources.SelectDownloads(ModDownload.DlSourceModDownloadGet(mod))[0]));
        });
        Run("source-version-loader-switching", () =>
        {
            var run = typeof(ModDownload).GetMethod("DlSourceLoader", BindingFlags.Static | BindingFlags.NonPublic)!
                .MakeGenericMethod(typeof(int), typeof(string));
            foreach (var mode in new[] { 0, 2 })
            {
                Config.Download.VersionListSource = mode;
                var main = new ModLoader.LoaderTask<int, string>("Test Main", _ => { });
                var bmcl = new ModLoader.LoaderTask<int, string>("Test Bmclapi", _ => { }) { output = "mirror" };
                var mojang = new ModLoader.LoaderTask<int, string>("Test Mojang", _ => { }) { output = "official" };
                bmcl.State = mojang.State = ModBase.LoadState.Finished;
                var sources = new List<KeyValuePair<ModLoader.LoaderTask<int, string>, int>> { new(bmcl, 1), new(mojang, 2) };
                run.Invoke(null, new object[] { main, sources, false });
                Require(main.output == (mode == 0 ? "mirror" : "official"));
            }
        });
        Run("source-ui-saves-and-restores-choice", () =>
        {
            System.Windows.Application.Current.Resources["MyComboBoxItem"] = new System.Windows.Style(typeof(MyComboBoxItem));
            System.Windows.Application.Current.Resources["ColorBrushSemiTransparent"] = System.Windows.Media.Brushes.Transparent;
            Config.Download.FileSource = 0;
            Config.Download.VersionListSource = 1;
            Config.Download.Comp.CompSourceSolution = 0;
            var page = new PageSetupGameManage();
            page.Reload();
            ComboBox Get(PageSetupGameManage p, string name) => (ComboBox)p.FindName(name);
            var files = Get(page, "ComboDownloadSource");
            var versions = Get(page, "ComboDownloadVersion");
            var mods = Get(page, "ComboDownloadMod");
            Require(files.IsEnabled && versions.IsEnabled && mods.IsEnabled);
            Require(files.SelectedIndex == 0 && versions.SelectedIndex == 1 && mods.SelectedIndex == 0);
            files.SelectedIndex = 1;
            versions.SelectedIndex = 0;
            mods.SelectedIndex = 1;
            Require(Config.Download.FileSource == 1 && Config.Download.VersionListSource == 0 && Config.Download.Comp.CompSourceSolution == 1);
            var reopened = new PageSetupGameManage();
            reopened.Reload();
            Require(Get(reopened, "ComboDownloadSource").SelectedIndex == 1 && Get(reopened, "ComboDownloadVersion").SelectedIndex == 0 && Get(reopened, "ComboDownloadMod").SelectedIndex == 1);
        });
        Run("source-mirror-request-boundary", () =>
        {
            FunctionalTestPolicy.Check(mirror);
            FunctionalTestPolicy.Check("https://mod.mcimirror.top/modrinth/v2/search");
            using var message = new HttpRequestMessage(HttpMethod.Get, "https://mod.mcimirror.top/curseforge/v1/mods");
            typeof(RequestSigning).GetMethod("SecretHeadersSign", BindingFlags.Static | BindingFlags.NonPublic)!
                .Invoke(null, new object[] { message.RequestUri!.AbsoluteUri, message, false, "" });
            Require(!message.Headers.Contains("x-api-key"));
        });
        Config.Download.FileSource = Config.Download.VersionListSource = Config.Download.Comp.CompSourceSolution = 2;
    }
}
