using Playnite.SDK;
using Playnite.SDK.Events;
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
                model.Emulators = plugin.PlayniteApi.Database.Emulators.ToList();
                InitializeComponent();
            }
            catch (Exception E)
            {
                logger.Error(E, "Error ScanView");
                plugin.PlayniteApi.Dialogs.ShowErrorMessage(E.Message, "Error");
            }
        }

        // --- CONTROLO GAMEPAD ---
        public void HandleControllerInput(OnControllerButtonStateChangedArgs args)
        {
            switch (args.Button)
            {
                case ControllerInputButton.B: // Fechar
                    Window.GetWindow(this)?.Close();
                    break;

                case ControllerInputButton.A: // Clicar no botão focado
                    if (Keyboard.FocusedElement is Button btn)
                        btn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    break;

                case ControllerInputButton.X: // Marcar/Desmarcar jogo
                    if (MainListView.SelectedItem is Candidate selected)
                    {
                        selected.Selected = !selected.Selected;
                        // Atualiza a UI para refletir
                        MainListView.Items.Refresh();
                    }
                    break;

                case ControllerInputButton.Y: // Abrir ComboBox de emulador
                    if (MainListView.SelectedItem is Candidate candidate && candidate.FileType == FileType.Rom)
                    {
                        var container = MainListView.ItemContainerGenerator.ContainerFromItem(candidate) as ListViewItem;
                        if (container != null)
                        {
                            var combo = FindVisualChild<ComboBox>(container);
                            if (combo != null) combo.IsDropDownOpen = true;
                        }
                    }
                    break;

                case ControllerInputButton.DPadUp:
                case ControllerInputButton.LeftStickUp:
                    if (MainListView.IsKeyboardFocusWithin)
                    {
                        if (MainListView.SelectedIndex > 0) MainListView.SelectedIndex--;
                    }
                    else
                    {
                        MoveFocus(FocusNavigationDirection.Up);
                    }
                    break;

                case ControllerInputButton.DPadDown:
                case ControllerInputButton.LeftStickDown:
                    if (MainListView.IsKeyboardFocusWithin)
                    {
                        if (MainListView.SelectedIndex < MainListView.Items.Count - 1) MainListView.SelectedIndex++;
                    }
                    else
                    {
                        MoveFocus(FocusNavigationDirection.Down);
                    }
                    break;

                case ControllerInputButton.DPadLeft:
                case ControllerInputButton.LeftStickLeft:
                    MoveFocus(FocusNavigationDirection.Left);
                    break;

                case ControllerInputButton.DPadRight:
                case ControllerInputButton.LeftStickRight:
                    MoveFocus(FocusNavigationDirection.Right);
                    break;
            }
        }

        private void MoveFocus(FocusNavigationDirection direction)
        {
            var request = new TraversalRequest(direction);
            if (Keyboard.FocusedElement is UIElement focusedElement)
            {
                focusedElement.MoveFocus(request);
            }
        }

        public static T FindVisualChild<T>(DependencyObject obj) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(obj, i);
                if (child != null && child is T) return (T)child;
                else
                {
                    T childOfChild = FindVisualChild<T>(child);
                    if (childOfChild != null) return childOfChild;
                }
            }
            return null;
        }

        // --- MÉTODOS DE CLIQUE ---
        private void ClickSelectFolder(object sender, RoutedEventArgs e)
        {
            string folder = null;
            if (plugin.PlayniteApi.ApplicationInfo.Mode == ApplicationMode.Fullscreen)
            {
                var picker = new FolderPickerView(plugin.PlayniteApi);
                var window = new Window
                {
                    Content = picker,
                    WindowStyle = WindowStyle.None,
                    WindowState = WindowState.Maximized,
                    ResizeMode = ResizeMode.NoResize,
                    Background = Brushes.Transparent,
                    Owner = Window.GetWindow(this)
                };
                picker.FolderSelected += (s, path) => { folder = path; window.Close(); };
                picker.Cancelled += (s, args) => { window.Close(); };
                window.ShowDialog();
            }
            else
            {
                var dialog = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog { Description = "Select a folder to scan", UseDescriptionForTitle = true };
                if (dialog.ShowDialog(Window.GetWindow(this)) == true) folder = dialog.SelectedPath;
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

            ScanBtn.IsEnabled = false;
            CancelBtn.IsEnabled = true;
            ScanProgressBar.Visibility = Visibility.Visible;
            ScanProgressBar.IsIndeterminate = true;
            cts = new CancellationTokenSource();

            try
            {
                var plist = settings.Settings.MatchPattern.Split(',').Select(x => x.Trim()).ToList();
                var roms = settings.Settings.RomExtensions.Split(',').Select(x => x.Trim());
                plist.AddRange(roms); // Incluir ROMs

                var slist = settings.Settings.SkipPattern.Split(',').Select(x => x.Trim());
                var exFolder = GetExcludedDir();
                bool recursive = settings.Settings.RecursiveSearch;

                var files = await Task.Run(() => Helper.Search(path, exFolder, plist, slist, recursive), cts.Token);

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
                        NewGameName = settings.Settings.OptionUseFolder ? new FileInfo(file.FullName).Directory.Name : Path.GetFileNameWithoutExtension(file.FullName)
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
                ScanBtn.IsEnabled = true;
                CancelBtn.IsEnabled = false;
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
                return System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(icon.Handle, new Int32Rect(0, 0, icon.Width, icon.Height), BitmapSizeOptions.FromEmptyOptions());
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
                        if (string.Equals(plugin.PlayniteApi.ExpandGameVariables(game, a.Path), filePath, StringComparison.OrdinalIgnoreCase)) return true;
                if (game.Roms != null)
                    foreach (var r in game.Roms)
                        if (string.Equals(plugin.PlayniteApi.ExpandGameVariables(game, r.Path), filePath, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private Game GenGame(Candidate c)
        {
            string folder = new FileInfo(c.EntryFilePath).Directory.ToString();
            string fpname = new FileInfo(c.EntryFilePath).Name;
            string root = plugin.PlayniteApi.Paths.ApplicationPath;
            string installDir = settings.Settings.OptionRelPath ? Helper.ToRelPath(folder, root, "{PlayniteDir}") : folder;

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
            if (!selected.Any()) { plugin.PlayniteApi.Dialogs.ShowMessage("No items selected.", "Info"); return; }

            int added = 0, skipped = 0;
            foreach (var item in selected)
            {
                if (settings.Settings.SkipDuplicates && IsGameAlreadyImported(item.EntryFilePath)) { skipped++; continue; }

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
        public object Convert(object value, Type t, object p, System.Globalization.CultureInfo c) => (value is string s && string.IsNullOrEmpty(s)) ? "(No Folder Selected)" : value;
        public object ConvertBack(object value, Type t, object p, System.Globalization.CultureInfo c) => throw new NotImplementedException();
    }

    public class IsRomConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, System.Globalization.CultureInfo c) => value is FileType ft && ft == FileType.Rom;
        public object ConvertBack(object value, Type t, object p, System.Globalization.CultureInfo c) => throw new NotImplementedException();
    }
}