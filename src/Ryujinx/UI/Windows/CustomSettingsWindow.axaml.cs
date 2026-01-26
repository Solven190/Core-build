using Avalonia.Controls;
using Avalonia.Controls.Platform;
using Avalonia.Threading;
using FluentAvalonia.Core;
using FluentAvalonia.UI.Controls;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Ava.UI.ViewModels;
using Ryujinx.HLE.FileSystem;
using System;
using System.Runtime.InteropServices;

namespace Ryujinx.Ava.UI.Windows
{
    public partial class CustomSettingsWindow : StyleableWindow
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        internal readonly CustomSettingsViewModel ViewModel;

        public CustomSettingsWindow(VirtualFileSystem virtualFileSystem, string titleId, string titleName, byte[] icon, string titlePath)
        {
            Title = $"Ryujinx {Program.Version} - {LocaleManager.Instance[LocaleKeys.Settings]} - {titleName} ";
            DataContext = ViewModel = new CustomSettingsViewModel(titleId,icon,titleName);
            ViewModel.CloseWindow += Close;
            InitializeComponent();
            ApplyDarkTitleBar();
            Load();
        }

        private void ApplyDarkTitleBar()
        {
            if (!OperatingSystem.IsWindows())
                return;

            // Apply immediately if possible
            if (PlatformImpl != null)
            {
                ForceDarkTitleBar();
            }
            else
            {
                // Wait until PlatformImpl is available
                this.PropertyChanged += (_, e) =>
                {
                    if (e.Property.Name == nameof(PlatformImpl) && PlatformImpl != null)
                    {
                        ForceDarkTitleBar();
                    }
                };
            }

            // Backup: Try it even when the window is active
            this.Activated += (_, _) => ForceDarkTitleBar();
        }

        private void ForceDarkTitleBar()
        {
            try
            {
                var handleField = PlatformImpl?.GetType().GetProperty("Handle")?.GetValue(PlatformImpl);
                if (handleField?.GetType().GetProperty("Handle")?.GetValue(handleField) is IntPtr hwnd && hwnd != IntPtr.Zero)
                {
                    int dark = 1;
                    DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int)); // Modern Windows
                    DwmSetWindowAttribute(hwnd, 19, ref dark, sizeof(int)); // Older Windows 10
                }
            }
            catch
            {
                // Silent fail
            }
        }

        private void Load()
        {
            Pages.Children.Clear();
            NavPanel.SelectionChanged += NavPanelOnSelectionChanged;
            NavPanel.SelectedItem = NavPanel.MenuItems.ElementAt(0);
        }

        private void NavPanelOnSelectionChanged(object sender, NavigationViewSelectionChangedEventArgs e)
        {
            if (e.SelectedItem is NavigationViewItem navItem && navItem.Tag is not null)
            {
                switch (navItem.Tag.ToString())
                {
                    case "SystemPage":
                        NavPanel.Content = SystemPage;
                        break;
                    case "CpuPage":
                        NavPanel.Content = CpuPage;
                        break;
                    case "GraphicsPage":
                        NavPanel.Content = GraphicsPage;
                        break;
                    case "AudioPage":
                        NavPanel.Content = AudioPage;
                        break;
                    default:
                        throw new NotImplementedException();
                }
            }
        }

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            base.OnClosing(e);
        }
    }
}
