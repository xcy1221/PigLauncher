using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using PCL;
using PCL.Core.App;

internal static partial class Checks
{
    private static void SettingsChecks()
    {
        var resources = System.Windows.Application.Current.Resources;
        resources["MyComboBoxItem"] = new Style(typeof(MyComboBoxItem));
        resources["ColorBrushSemiTransparent"] = System.Windows.Media.Brushes.Transparent;
        resources["BooleanToVisibilityConverter"] = new BooleanToVisibilityConverter();
        Run("settings-removed-pages-and-hide-options", () =>
        {
            var assembly = typeof(ModMain).Assembly;
            Require(assembly.GetType("PCL.PageSetupFeedback") is null);
            Require(assembly.GetType("PCL.PageSetupLauncherLanguage") is null);
            Require(!Enum.GetNames<FormMain.PageSubType>().Any(n => n is "SetupFeedback" or "SetupLauncherLanguage"));
            foreach (var name in new[] { "ItemFeedback", "ItemLauncherLanguage" })
                Require(typeof(PageSetupLeft).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public) is null);
            foreach (var name in new[] { "CheckHiddenSetupFeedback", "CheckHiddenSetupLauncherLanguage", "CheckLauncherLogo", "CheckShowLaunchingHint" })
                Require(typeof(PageSetupUI).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public) is null);
            Require(typeof(PageLaunchLeft).GetField("PanLaunchingHint", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public) is null);
            Require(typeof(ModMain).GetField("frmStart", BindingFlags.Static | BindingFlags.Public) is null);
            Require(Config.Preference.GetType().GetProperty("ShowStartupLogo") is null && Config.Preference.GetType().GetProperty("ShowLaunchingHint") is null);
        });
        Run("settings-language-region-in-misc", () =>
        {
            Config.Preference.Localization.Language = "zh-CN";
            Config.Preference.Localization.FormatCulture = "en-GB";
            PageSetupLauncherMisc page;
            ModAnimation.AniControlEnabled++;
            try { page = new PageSetupLauncherMisc(); }
            finally { ModAnimation.AniControlEnabled--; }
            var settings = (LauncherLanguageSettings)page.FindName("LanguageSettings");
            settings.Reload();
            var language = (ComboBox)settings.FindName("ComboUiLanguage");
            var culture = (ComboBox)settings.FindName("ComboUiFormatCulture");
            Require(((MyComboBoxItem)language.SelectedItem).Tag.ToString() == "zh-CN");
            Require(((MyComboBoxItem)culture.SelectedItem).Tag.ToString() == "en-GB");
            Require(settings.FindName("BannerInternationalization") is null && settings.FindName("BtnTranslateWebsite") is null);
            language.SelectedItem = language.Items.OfType<MyComboBoxItem>().Single(i => i.Tag.ToString() == "en-US");
            culture.SelectedItem = culture.Items.OfType<MyComboBoxItem>().Single(i => i.Tag.ToString() == "zh-TW");
            Require(Config.Preference.Localization.Language == "en-US" && Config.Preference.Localization.FormatCulture == "zh-TW");
            var reopened = new LauncherLanguageSettings();
            reopened.Reload();
            Require(((MyComboBoxItem)((ComboBox)reopened.FindName("ComboUiLanguage")).SelectedItem).Tag.ToString() == "en-US");
            Require(((MyComboBoxItem)((ComboBox)reopened.FindName("ComboUiFormatCulture")).SelectedItem).Tag.ToString() == "zh-TW");
        });
    }
}
