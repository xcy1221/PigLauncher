using System.IO;
using PCL.Core.App.Tools;
using PCL.Core.UI;
using PCL.Core.UI.Media;

internal static partial class Checks
{
    private static void WebpChecks()
    {
        var supported = ImageConverter.CanDecodeWebp();
        Run("webp-real-decoder", () =>
        {
            var decoded = false;
            try
            {
                using var input = File.OpenRead(Path.Combine(Root, "probe.webp"));
                using var png = input.FromWebpToPng();
                Require(png.Length > 0);
                decoded = true;
            }
            catch (NotSupportedException) { }
            Require(supported == decoded);
            Record("webp-support", "info", supported ? "available" : "unavailable");
        });
        Run("webp-optional-message", () =>
        {
            var shown = 0;
            void Inspect(string message, string caption, ICollection<MsgBoxButtonInfo> buttons,
                MsgBoxTheme theme, bool block, ref int result)
            {
                shown++;
                Require(caption == "可选图片支持" && message.Contains("WebP"));
                Require(message.Contains("不影响游戏下载和启动") && !message.Contains("运行所需依赖"));
                Require(buttons.First().Context == "打开商店安装");
                result = 2; // 模拟“稍后”，禁止测试打开商店。
            }
            MsgBoxWrapper.OnShow += Inspect;
            try
            {
                new DependencyCheckService().StartAsync().GetAwaiter().GetResult();
                Require(shown == (supported ? 0 : 1));
            }
            finally { MsgBoxWrapper.OnShow -= Inspect; }
        });
    }
}
