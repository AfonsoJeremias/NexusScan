using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace BetterScan
{
    public class BetterScan : GenericPlugin
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private BetterScanSettingsViewModel Settings { get; set; }
        public override Guid Id { get; } = Guid.Parse("cfa84ecf-cb29-4aa2-bbac-c9aa5003cc22");

        private static Window currentScanWindow = null;
        private static FolderPickerView currentFolderPicker = null;

        public BetterScan(IPlayniteAPI api) : base(api)
        {
            Settings = new BetterScanSettingsViewModel(this);
            Properties = new GenericPluginProperties { HasSettings = true };
        }

        public override IEnumerable<TopPanelItem> GetTopPanelItems()
        {
            yield return new TopPanelItem
            {
                Title = "Better Scan",
                Icon = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/BetterScan;component/icon.png")),
                Activated = () => CreateView(null)
            };
        }

        public override IEnumerable<MainMenuItem> GetMainMenuItems(GetMainMenuItemsArgs args)
        {
            yield return new MainMenuItem { MenuSection = "@Better Scan", Description = "Scan Folder...", Action = CreateView };

            var folders = Settings.Settings.PredefinedFolders;
            if (folders != null && folders.Any(f => Directory.Exists(f)))
            {
                foreach (var folder in folders.Where(Directory.Exists))
                {
                    string captured = folder;
                    yield return new MainMenuItem { MenuSection = "@Better Scan|Scan Predefined Folders", Description = Path.GetFileName(captured) + " (" + captured + ")", Action = (a) => CreateViewWithFolder(captured) };
                }

                yield return new MainMenuItem { MenuSection = "@Better Scan|Scan Predefined Folders", Description = "📂 All Folders...", Action = CreateViewWithAllFolders };
                yield return new MainMenuItem { MenuSection = "@Better Scan|Scan Predefined Folders", Description = "⚙️ Manage Folders...", Action = (a) => PlayniteApi.MainView.OpenPluginSettings(Id) };
            }

            yield return new MainMenuItem { MenuSection = "@Better Scan", Description = "Verify Game Path", Action = CreateViewVerifyPath };
        }

        // Intercepta os botões do controle
        public override void OnControllerButtonStateChanged(OnControllerButtonStateChangedArgs args)
        {
            if (args.State != ControllerInputState.Pressed) return;

            if (currentFolderPicker != null && Window.GetWindow(currentFolderPicker)?.IsVisible == true)
            {
                currentFolderPicker.HandleControllerInput(args);
            }
            else if (currentScanWindow != null && currentScanWindow.IsVisible)
            {
                if (currentScanWindow.Content is ScanView scanView)
                {
                    scanView.HandleControllerInput(args);
                }
            }
        }

        private void ShowWindow(ScanView view, string title)
        {
            if (currentScanWindow != null && currentScanWindow.IsVisible)
            {
                currentScanWindow.Focus();
                return;
            }

            var window = PlayniteApi.Dialogs.CreateWindow(new WindowCreationOptions
            {
                ShowMinimizeButton = false,
                ShowMaximizeButton = false,
                ShowCloseButton = true
            });

            window.Title = title;
            window.Content = view;
            window.Owner = PlayniteApi.Dialogs.GetCurrentAppWindow();
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            window.Background = Brushes.Transparent;

            if (PlayniteApi.ApplicationInfo.Mode == ApplicationMode.Fullscreen)
            {
                window.WindowStyle = WindowStyle.None;
                window.WindowState = WindowState.Maximized;
                window.ResizeMode = ResizeMode.NoResize;
            }
            else
            {
                window.Height = 500;
                window.Width = 900;
            }

            window.Closed += (s, e) => { currentScanWindow = null; };
            currentScanWindow = window;
            window.ShowDialog();
        }

        private void CreateView(MainMenuItemActionArgs args)
        {
            try { ShowWindow(new ScanView(this, Settings), "Better Scan Folder"); }
            catch (Exception E) { logger.Error(E, "Error CreateView"); PlayniteApi.Dialogs.ShowErrorMessage(E.Message, "Error"); }
        }

        private void CreateViewWithFolder(string folder)
        {
            try
            {
                var view = new ScanView(this, Settings);
                view.model.TargetFolder = folder;
                ShowWindow(view, $"Better Scan - {folder}");
            }
            catch (Exception E) { logger.Error(E, "Error CreateViewWithFolder"); PlayniteApi.Dialogs.ShowErrorMessage(E.Message, "Error"); }
        }

        private void CreateViewWithAllFolders(MainMenuItemActionArgs args)
        {
            var folders = Settings.Settings.PredefinedFolders.Where(Directory.Exists).ToList();
            if (!folders.Any()) { PlayniteApi.Dialogs.ShowMessage("No valid folders configured.", "Warning"); return; }
            try
            {
                var view = new ScanView(this, Settings);
                view.model.TargetFolder = string.Join("; ", folders);
                ShowWindow(view, $"Better Scan - {folders.Count} folders");
            }
            catch (Exception E) { logger.Error(E, "Error CreateViewWithAllFolders"); PlayniteApi.Dialogs.ShowErrorMessage(E.Message, "Error"); }
        }

        private void CreateViewVerifyPath(MainMenuItemActionArgs args)
        {
            try
            {
                var res = Impl.VerifyPath(this.PlayniteApi);
                PlayniteApi.Dialogs.ShowMessage($"Found {res.ndir} invalid dir, {res.nrom} invalid ROM, {res.nact} invalid action.");
            }
            catch (Exception E) { logger.Error(E, "Error VerifyPath"); PlayniteApi.Dialogs.ShowErrorMessage(E.Message, "Error"); }
        }

        public override ISettings GetSettings(bool firstRun) => Settings;
        public override UserControl GetSettingsView(bool firstRun) => new BetterScanSettingsView();
    }
}