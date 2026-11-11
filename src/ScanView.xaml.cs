using Playnite.SDK;
using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace BetterScan
{
    public partial class ScanView : UserControl
    {
        private readonly BetterScan plugin;
        private readonly BetterScanSettingsViewModel settings;
        private static readonly ILogger logger = LogManager.GetLogger();
        public ScanViewModel model = new ScanViewModel();
        private CancellationTokenSource cts;

        public ScanView() { InitializeComponent(); }

        public ScanView(BetterScan plugin, BetterScanSettingsViewModel settings)
        {
            try
            {
                this.plugin = plugin;
                this.settings = settings;
                model.TargetFolder = settings.Settings.TargetFolder;
                DataContext = model;
                model.Emulators = plugin.PlayniteApi.Emulators.GetAll().ToList();
                InitializeComponent();
                this.PreviewKeyDown += ScanView_PreviewKeyDown;
            }
            catch (Exception E)
            {
                logger.Error(E, "Error during initializing ScanView");
                plugin.PlayniteApi.Dialogs.ShowErrorMessage(E.Message, "Error");
            }
        }

        private void ScanView_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Down || e.Key == Key.Up || e.Key == Key.Left || e.Key == Key.Right)
            {
                var direction = FocusNavigationDirection.Next;
                if (e.Key == Key.Up) direction = FocusNavigationDirection.Up;
                if (e.Key == Key.Down) direction = FocusNavigationDirection.Down;
                if (e.Key == Key.Left) direction = FocusNavigationDirection.Left;
                if (e.Key == Key.Right) direction = FocusNavigationDirection.Right;

                var request = new TraversalRequest(direction);
                if (Keyboard.FocusedElement is UIElement focusedElement)
                {
                    focusedElement.MoveFocus(request);
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.Enter || e.Key == Key.Space)
            {
                if (Keyboard.FocusedElement is Button btn)
                {
                    btn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    e.Handled = true;
                }
                else if (Keyboard.FocusedElement is CheckBox chk)
                {
                    chk.IsChecked = !chk.IsChecked;
                    e.Handled = true;
                }
            }
        }

        private void ClickSelectFolder(object sender, RoutedEventArgs e)
        {
            string folder = null;

            if (plugin.PlayniteApi.ApplicationInfo.Mode == ApplicationMode.Fullscreen)
            {
                var picker = new FolderPickerView(plugin);
                var window = new Window
                {
                    Content = picker,
                    WindowStyle = WindowStyle.None,
                    WindowState = WindowState.Maximized,
                    ResizeMode = ResizeMode.NoResize,
                    Owner = Window.GetWindow(this)
                };
                picker.FolderSelected += (s, path) => { folder = path; window.Close(); };
                picker.Cancelled += (s, args) => { window.Close(); };
                window.ShowDialog();
            }
            else
            {
                var dialog = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog
                {
                    Description = "Select a folder to scan",
                    UseDescriptionForTitle = true
                };
                if (dialog.ShowDialog(Window.GetWindow(this)) == true)
                    folder = dialog.SelectedPath;
            }

            if (string.IsNullOrEmpty(folder)) return;
            model.TargetFolder = folder;
            Window.GetWindow(this).Title = "Scan Result of " + folder;
        }

        private IEnumerable<string> GetExcludedDir()
        {
            const string varname = "{PlayniteDir}";
            string basePath = plugin.PlayniteApi.Paths.ApplicationPath;
            return plugin.PlayniteApi.Database.Games
                .Where(g => !string.IsNullOrEmpty(g.InstallDirectory))
                .Select(g => Helper.ResolveRelPath(g.InstallDirectory.Replace(varname, basePath)));
        }

        private FileType DetectFileType(string filePath)
        {
            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            if (new[] { ".exe", ".bat", ".cmd", ".com", ".ps1" }.Contains(ext)) return FileType.Executable;
            if (ext == ".lnk") return FileType.Link;
            var romExts = settings.Settings.RomExtensions.Split(',').Select(x => x.Trim().ToLowerInvariant());
            if (romExts.Contains(ext)) return FileType.Rom;
            return FileType.Unknown;
        }

        private async void ClickScan(object sender, RoutedEventArgs e)
        {
            string path = model.TargetFolder;
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            {
                plugin.PlayniteApi.Dialogs.ShowMessage("Select a valid folder.", "Warning");
                return;
            }

            ScanButton.IsEnabled = false;
            CancelButton.IsEnabled = true;
            ScanProgressBar.Visibility = Visibility.Visible;
            ScanProgressBar.IsIndeterminate = true;
            cts = new CancellationTokenSource();

            try
            {
                var plist = settings.Settings.MatchPattern.Split(',').Select(x => x.Trim());
                var slist = settings.Settings.SkipPattern.Split(',').Select(x => x.Trim());
                var exFolder = GetExcludedDir();
                bool recursive = settings.Settings.RecursiveSearch;

                var files = await Task.Run(() =>
                    Helper.Search(path, exFolder, plist, slist, recursive), cts.Token);

                var arr = new List<Candidate>();
                foreach (var file in files)
                {
                    if (cts.IsCancellationRequested) break;
                    var ft = DetectFileType(file.FullName);
                    var c = new Candidate
                    {
                        Selected = false,
                        EntryFilePath = file.FullName,
                        IconData = GetIconData(file.FullName),
                        FileType = ft,
                        NewGameName = settings.Settings.OptionUseFolder
                            ? new FileInfo(file.FullName).Directory.Name
                            : Path.GetFileNameWithoutExtension(file.FullName)
                    };
                    if (ft == FileType.Rom && model.Emulators.Any())
                        c.SelectedEmulator = model.Emulators.First();
                    arr.Add(c);
                }
                model.CandidateList = arr;
                SavePath();
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                logger.Error(ex.Message);
                plugin.PlayniteApi.Dialogs.ShowErrorMessage(ex.Message, "Error");
            }
            finally
            {
                ScanButton.IsEnabled = true;
                CancelButton.IsEnabled = false;
                ScanProgressBar.Visibility = Visibility.Collapsed;
                cts = null;
            }
        }

        private void ClickCancel(object sender, RoutedEventArgs e) => cts?.Cancel();

        private void SavePath()
        {
            settings.BeginEdit();
            settings.Settings.TargetFolder = model.TargetFolder;
            settings.EndEdit();
        }

        private static ImageSource GetIconData(string fp)
        {
            try
            {
                var iconex = new TsudaKageyu.IconExtractor(fp);
                if (iconex.Count == 0) return null;
                var icon = iconex.GetIcon(0);
                return System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                    icon.Handle,
                    new Int32Rect(0, 0, icon.Width, icon.Height),
                    BitmapSizeOptions.FromEmptyOptions());
            }
            catch { return null; }
        }

        private string GetIconTempPath(string fp)
        {
            string res = null;
            try
            {
                var iconex = new TsudaKageyu.IconExtractor(fp);
                if (iconex.Count > 0)
                {
                    string tempDir = Path.Combine(plugin.PlayniteApi.Paths.ConfigurationPath, "temp_icons");
                    Directory.CreateDirectory(tempDir);
                    string tempfp = Path.Combine(tempDir, Guid.NewGuid() + ".png");
                    using (var f = new FileStream(tempfp, FileMode.CreateNew)) iconex.Save(0, f);
                    res = tempfp;
                }
            }
            catch (Exception ex) { logger.Error(ex, "Error extracting icon"); }

            if (res == null && settings.Settings.OptionSearchAdjacentIco)
            {
                string iconPath = Helper.SearchIconFile(fp);
                if (iconPath != null)
                {
                    string tempDir = Path.Combine(plugin.PlayniteApi.Paths.ConfigurationPath, "temp_icons");
                    Directory.CreateDirectory(tempDir);
                    string tempfp = Path.Combine(tempDir, Guid.NewGuid() + ".png");
                    File.Copy(iconPath, tempfp);
                    res = tempfp;
                }
            }
            return res;
        }

        private bool IsGameAlreadyImported(string filePath)
        {
            foreach (var game in plugin.PlayniteApi.Database.Games)
            {
                if (game.GameActions != null)
                    foreach (var a in game.GameActions)
                        if (string.Equals(plugin.PlayniteApi.ExpandGameVariables(game, a.Path), filePath, StringComparison.OrdinalIgnoreCase))
                            return true;
                if (game.Roms != null)
                    foreach (var r in game.Roms)
                        if (string.Equals(plugin.PlayniteApi.ExpandGameVariables(game, r.Path), filePath, StringComparison.OrdinalIgnoreCase))
                            return true;
            }
            return false;
        }

        private Game GenGame(Candidate c)
        {
            string folder = new FileInfo(c.EntryFilePath).Directory.ToString();
            string fpname = new FileInfo(c.EntryFilePath).Name;
            string root = plugin.PlayniteApi.Paths.ApplicationPath;

            string installDir = settings.Settings.OptionRelPath
                ? Helper.ToRelPath(folder, root, "{PlayniteDir}")
                : folder;

            var g = new Game { Name = c.NewGameName, InstallDirectory = installDir, IsInstalled = true };
            var action = new GameAction { IsPlayAction = true, Name = fpname, WorkingDir = "{InstallDir}" };

            if (c.FileType == FileType.Rom && c.SelectedEmulator != null)
            {
                action.Type = GameActionType.Emulator;
                action.Path = c.EntryFilePath;
                action.EmulatorId = c.SelectedEmulator.Id;
            }
            else
            {
                action.Type = GameActionType.File;
                action.Path = "{InstallDir}\\" + fpname;
            }

            g.GameActions = new ObservableCollection<GameAction> { action };
            return g;
        }

        private void ClickAdd(object sender, RoutedEventArgs e)
        {
            var selected = model.CandidateList.Where(c => c.Selected).ToList();
            if (!selected.Any())
            {
                plugin.PlayniteApi.Dialogs.ShowMessage("No items selected.", "Info");
                return;
            }

            int added = 0, skipped = 0;
            foreach (var item in selected)
            {
                if (settings.Settings.SkipDuplicates && IsGameAlreadyImported(item.EntryFilePath))
                {
                    skipped++;
                    continue;
                }

                try
                {
                    Game g = GenGame(item);
                    plugin.PlayniteApi.Database.Games.Add(g);

                    string tempIcon = GetIconTempPath(item.EntryFilePath);
                    if (!string.IsNullOrEmpty(tempIcon) && File.Exists(tempIcon))
                    {
                        try
                        {
                            string persistentPath = plugin.PlayniteApi.Database.AddFile(tempIcon, g.Id);
                            g.Icon = persistentPath;
                            plugin.PlayniteApi.Database.Games.Update(g);
                        }
                        finally { File.Delete(tempIcon); }
                    }
                    added++;
                }
                catch (Exception ex) { logger.Error(ex, "Error adding game " + item.EntryFilePath); }
            }

            model.CandidateList = model.CandidateList.Where(c => !c.Selected).ToList();
            string msg = $"Added {added} game(s).";
            if (skipped > 0) msg += $" Skipped {skipped} duplicate(s).";
            plugin.PlayniteApi.Dialogs.ShowMessage(msg, "Complete");
        }
    }

    public class EmptyPathConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, System.Globalization.CultureInfo c)
            => (value is string s && string.IsNullOrEmpty(s)) ? "(No Folder Selected)" : value;
        public object ConvertBack(object value, Type t, object p, System.Globalization.CultureInfo c) => throw new NotImplementedException();
    }

    public class IsRomConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, System.Globalization.CultureInfo c)
            => value is FileType ft && ft == FileType.Rom;
        public object ConvertBack(object value, Type t, object p, System.Globalization.CultureInfo c) => throw new NotImplementedException();
    }
}