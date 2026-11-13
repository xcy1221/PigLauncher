using System.IO;
using System.Text;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.UI;
using PCL.Network;
using PCL.Network.Loaders;
using static PCL.ModLoader;

namespace PCL;

public static class ModpackInstallStatus
{
    public const string IncompleteLabel = "安装未完成：整合包模组或资源下载失败";
    public const string FailureReportName = "ModpackInstallFailure.txt";
    public const string ManualDownloadReportName = "CurseForgeManualDownloads.html";

    public static LoaderCombo<string> CreateLoader(string name, string instancePath,
        string minecraftVersion, string? packVersion, string source, string? resourceId,
        List<LoaderBase> gameLoaders, List<LoaderBase> resourceLoaders, LoaderBase finalizer)
    {
        // 基础版本就绪后再获取整合包资源，资源失败时仍能保留可识别的版本。
        var game = new LoaderCombo<string>(Lang.Text("Minecraft.Download.Modpack.Stage.GameInstall"), gameLoaders)
            { show = false, ProgressWeight = gameLoaders.Sum(l => l.ProgressWeight) };
        var resources = new LoaderCombo<string>(Lang.Text("Minecraft.Download.Modpack.Stage.ModpackInstall"), resourceLoaders)
            { show = false, ProgressWeight = resourceLoaders.Sum(l => l.ProgressWeight) };
        var loader = new LoaderCombo<string>(name, new[] { game, resources, finalizer });
        loader.OnStateChanged = task =>
        {
            if (ShouldKeepInstance(task.State, game.State, resources.State))
            {
                try
                {
                    var incompleteFiles = resources.GetLoaderList(false).OfType<LoaderDownload>()
                        .SelectMany(download => download.files).Where(file => file.State != NetState.Finished)
                        .Select(file => Path.GetFileName(file.LocalPath)).Distinct().ToArray();
                    SaveIncomplete(instancePath, minecraftVersion, packVersion, source, resourceId, task.Error,
                        incompleteFiles);
                }
                catch (Exception ex)
                {
                    // 状态记录失败也不应删除已完成的游戏本体。
                    ModBase.Log(ex, "记录整合包未完成状态失败");
                }
                HintService.Hint($"{name}安装未完成，已保留实例。请补齐下载失败的模组或资源。", HintType.Error);
                var manualReport = Path.Combine(instancePath, "PCL", ManualDownloadReportName);
                if (File.Exists(manualReport)
                    && ModMain.MyMsgBox("部分资源需要从 CurseForge 官网手动下载。\n"
                        + "游戏本体、加载器和已下载文件已保留；请查看清单中的官网链接、文件名与放置位置。",
                        "整合包需要补齐文件", "查看下载清单", "稍后处理") == 1)
                {
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(manualReport)
                            { UseShellExecute = true });
                    }
                    catch (Exception ex)
                    {
                        ModBase.Log(ex, "打开 CurseForge 手动下载清单失败");
                        ModBase.OpenExplorer(manualReport);
                    }
                }
                ModBase.WriteIni(ModFolder.mcFolderSelected + "PCL.ini", "InstanceCache", "");
                LoaderFolderRun(ModInstanceList.mcInstanceListLoader, ModFolder.mcFolderSelected,
                    LoaderFolderRunType.ForceRun, 1, @"versions\");
                return;
            }
            if (task.State == ModBase.LoadState.Failed && game.State == ModBase.LoadState.Failed)
                task.Error = new Exception("Minecraft 或 Mod 加载器下载/安装失败。" + task.Error?.Message, task.Error);
            if (task.State == ModBase.LoadState.Finished)
                ClearIncomplete(instancePath);
            ModDownloadLib.McInstallState(task);
        };
        return loader;
    }

    public static bool ShouldKeepInstance(ModBase.LoadState installState, ModBase.LoadState gameState,
        ModBase.LoadState resourceState) => installState == ModBase.LoadState.Failed
        && gameState == ModBase.LoadState.Finished && resourceState == ModBase.LoadState.Failed;

    public static void SaveManualDownloads(string instancePath,
        IEnumerable<(ModComp.CompFile File, string TargetFolder)> files)
    {
        var report = Path.Combine(instancePath, "PCL", ManualDownloadReportName);
        var entries = files.ToArray();
        if (entries.Length == 0)
        {
            File.Delete(report);
            return;
        }
        static string Encode(string text) => System.Net.WebUtility.HtmlEncode(text);
        var rows = entries.Select(entry => $"<tr><td>{Encode(entry.File.FileName)}</td>"
            + $"<td><a href=\"{Encode(entry.File.ManualDownloadUrl)}\">打开 CurseForge 文件页面</a></td>"
            + $"<td>{Encode(entry.TargetFolder)}</td></tr>");
        Directory.CreateDirectory(Path.GetDirectoryName(report)!);
        File.WriteAllText(report, "<!doctype html><html lang=\"zh-CN\"><meta charset=\"utf-8\">"
            + "<title>CurseForge 手动下载清单</title><style>body{font:16px sans-serif;margin:32px;line-height:1.6}"
            + "table{border-collapse:collapse}td,th{padding:12px;border:1px solid #ccc;text-align:left}"
            + "td{overflow-wrap:anywhere}</style><h1>需要手动下载的文件</h1>"
            + "<p>请通过下列 CurseForge 官网链接下载对应文件，并放入指定文件夹。"
            + "游戏本体、加载器和其他已下载文件已保留；补齐前整合包可能无法正常运行。</p>"
            + "<table><thead><tr><th>文件名</th><th>官网入口</th><th>放置位置</th></tr></thead><tbody>"
            + string.Join("", rows) + "</tbody></table></html>", new UTF8Encoding(false));
    }

    public static void SaveIncomplete(string instancePath, string minecraftVersion, string? packVersion,
        string source, string? resourceId, Exception? error, IEnumerable<string>? incompleteFiles = null)
    {
        var path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(instancePath)) + Path.DirectorySeparatorChar;
        States.Instance.ModpackSource[path] = source;
        States.Instance.ModpackVersion[path] = packVersion ?? "";
        States.Instance.ModpackId[path] = resourceId ?? "";
        States.Instance.CustomInfo[path] = $"{IncompleteLabel} · Minecraft {minecraftVersion}"
            + (string.IsNullOrEmpty(packVersion) ? "" : $" · 整合包 {packVersion}");
        File.Delete(Path.Combine(path, ".pclignore"));
        Directory.CreateDirectory(Path.Combine(path, "PCL"));
        File.WriteAllText(Path.Combine(path, "PCL", FailureReportName),
            $"{IncompleteLabel}\nMinecraft：{minecraftVersion}\n整合包：{packVersion}\n来源：{source}\n\n"
            + "游戏本体与 Mod 加载器已完成安装，实例及已下载文件已保留。\n"
            + "请根据以下错误补齐资源；必要资源未补齐前，整合包可能无法正常运行。\n\n"
            + "未完成文件：\n" + string.Join("\n", incompleteFiles ?? []) + "\n\n" + error,
            new UTF8Encoding(false));
    }

    public static void ClearIncomplete(string instancePath)
    {
        var path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(instancePath)) + Path.DirectorySeparatorChar;
        var report = Path.Combine(path, "PCL", FailureReportName);
        if (File.Exists(report)) File.Delete(report);
        File.Delete(Path.Combine(path, "PCL", ManualDownloadReportName));
        if (States.Instance.CustomInfo[path]?.StartsWith(IncompleteLabel, StringComparison.Ordinal) == true)
            States.Instance.CustomInfo[path] = "";
    }
}
