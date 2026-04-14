using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Ryujinx.Ava.Common;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.UI.Common.Configuration;
using System;

namespace Ryujinx.Ava.UI.ViewModels
{
    public partial class AboutWindowViewModel : BaseModel, IDisposable
    {
        [ObservableProperty]
        private Bitmap _forgejoLogo;

        [ObservableProperty]
        private Bitmap _discordLogo;

        [ObservableProperty]
        private string _version;

        public string FormerDevelopers => LocaleManager.Instance.UpdateAndGetDynamicValue(LocaleKeys.AboutPageDeveloperListMore, "gdkchan, Ac_K, marysaka, rip in peri peri, LDj3SNuD, emmaus, Thealexbarney, GoffyDude, TSRBerry, IsaacMarovitz");

        public string Developers => "KeatonTheBot";
        
        public AboutWindowViewModel()
        {
            Version = Program.Version;
            UpdateLogoTheme(ConfigurationState.Instance.UI.BaseStyle.Value);

            ThemeManager.ThemeChanged += ThemeManager_ThemeChanged;
        }

        private void ThemeManager_ThemeChanged(object sender, EventArgs e)
        {
            Dispatcher.UIThread.Post(() => UpdateLogoTheme(ConfigurationState.Instance.UI.BaseStyle.Value));
        }

        private void UpdateLogoTheme(string theme)
        {
            bool isDarkTheme = theme == "Dark" || (theme == "Auto" && App.DetectSystemTheme() == ThemeVariant.Dark);

            string basePath = "resm:Ryujinx.UI.Common.Resources.";
            string themeSuffix = isDarkTheme ? "Dark.png" : "Light.png";

            ForgejoLogo = LoadBitmap($"{basePath}Logo_Forgejo.png?assembly=Ryujinx.UI.Common");
            DiscordLogo = LoadBitmap($"{basePath}Logo_Discord_{themeSuffix}?assembly=Ryujinx.UI.Common");
        }

        private Bitmap LoadBitmap(string uri)
        {
            return new Bitmap(Avalonia.Platform.AssetLoader.Open(new Uri(uri)));
        }

        public void Dispose()
        {
            ThemeManager.ThemeChanged -= ThemeManager_ThemeChanged;
            GC.SuppressFinalize(this);
        }
    }
}
