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
        if (States.Instance.CustomInfo[path]?.StartsWith(IncompleteLabel, StringComparison.Ordinal) == true)
            States.Instance.CustomInfo[path] = "";
    }
}
