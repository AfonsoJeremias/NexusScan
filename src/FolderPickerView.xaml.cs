using Playnite.SDK;
using Playnite.SDK.Events;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace BetterScan
{
    public partial class FolderPickerView : UserControl
    {
        private string currentPath;
        private readonly IPlayniteAPI api;
        private DateTime _lastInputTime = DateTime.MinValue;

        public event EventHandler<string> FolderSelected;
        public event EventHandler Cancelled;

        public FolderPickerView(IPlayniteAPI api)
        {
            this.api = api;
            InitializeComponent();
            LoadDrives();
            this.Loaded += (s, e) => FoldersListBox.Focus();
            this.PreviewKeyDown += FolderPickerView_PreviewKeyDown;
        }

        // --- TECLADO ---
        private void FolderPickerView_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                if (currentPath == null) Cancelled?.Invoke(this, EventArgs.Empty);
                else NavigateUp();
                e.Handled = true;
            }
            else if (e.Key == Key.Enter)
            {
                EnterSelectedFolder();
                e.Handled = true;
            }
            else if (e.Key == Key.X)
            {
                SelectCurrentFolder();
                e.Handled = true;
            }
        }

        // --- CONTROLO GAMEPAD ---
        public void HandleControllerInput(OnControllerButtonStateChangedArgs args)
        {
            if ((DateTime.Now - _lastInputTime).TotalMilliseconds < 150) return;
            _lastInputTime = DateTime.Now;

            switch (args.Button)
            {
                case ControllerInput.B:
                    if (currentPath == null) Cancelled?.Invoke(this, EventArgs.Empty);
                    else NavigateUp();
                    break;

                case ControllerInput.A:
                    EnterSelectedFolder();
                    break;

                case ControllerInput.X:
                    SelectCurrentFolder();
                    break;

                case ControllerInput.DPadUp:
                case ControllerInput.LeftStickUp:
                    if (FoldersListBox.SelectedIndex > 0) FoldersListBox.SelectedIndex--;
                    break;

                case ControllerInput.DPadDown:
                case ControllerInput.LeftStickDown:
                    if (FoldersListBox.SelectedIndex < FoldersListBox.Items.Count - 1) FoldersListBox.SelectedIndex++;
                    break;
            }
        }

        private void LoadDrives()
        {
            var drives = DriveInfo.GetDrives()
                .Where(d => d.IsReady && d.DriveType == DriveType.Fixed)
                .Select(d => new FolderItem { Name = d.Name, FullPath = d.RootDirectory.FullName })
                .ToList();
            FoldersListBox.ItemsSource = drives;
            currentPath = null;
            if (FoldersListBox.Items.Count > 0) FoldersListBox.SelectedIndex = 0;
        }

        private void LoadFolders(string path)
        {
            try
            {
                var dirs = Directory.GetDirectories(path)
                    .Select(d => new FolderItem { Name = Path.GetFileName(d), FullPath = d })
                    .ToList();
                
                if (Path.GetDirectoryName(path) != null)
                    dirs.Insert(0, new FolderItem { Name = ".. (Up)", FullPath = Path.GetDirectoryName(path) });
                
                FoldersListBox.ItemsSource = dirs;
                currentPath = path;
                if (FoldersListBox.Items.Count > 0) FoldersListBox.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                api.Dialogs.ShowErrorMessage($"Cannot access {path}: {ex.Message}", "Error");
            }
        }

        private void NavigateUp()
        {
            if (currentPath == null) return; 
            
            var parent = Path.GetDirectoryName(currentPath);
            if (string.IsNullOrEmpty(parent))
                LoadDrives(); 
            else
                LoadFolders(parent);
        }

        private void EnterSelectedFolder()
        {
            if (FoldersListBox.SelectedItem is FolderItem item)
            {
                if (item.Name == ".. (Up)")
                    NavigateUp();
                else
                    LoadFolders(item.FullPath);
            }
        }

        private void SelectCurrentFolder()
        {
            if (FoldersListBox.SelectedItem is FolderItem item && item.Name != ".. (Up)")
                FolderSelected?.Invoke(this, item.FullPath);
            else if (currentPath != null)
                FolderSelected?.Invoke(this, currentPath);
        }

        private void SelectButton_Click(object sender, RoutedEventArgs e) => SelectCurrentFolder();
        private void UpButton_Click(object sender, RoutedEventArgs e) => NavigateUp();
        private void CancelButton_Click(object sender, RoutedEventArgs e) => Cancelled?.Invoke(this, EventArgs.Empty);
    }

    public class FolderItem
    {
        public string Name { get; set; }
        public string FullPath { get; set; }
    }
}