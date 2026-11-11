using Playnite.SDK;
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

        public event EventHandler<string> FolderSelected;
        public event EventHandler Cancelled;

        public FolderPickerView(IPlayniteAPI api)
        {
            this.api = api;
            InitializeComponent();
            LoadDrives();
            this.PreviewKeyDown += FolderPickerView_PreviewKeyDown;
            this.Loaded += (s, e) => FoldersListBox.Focus();
        }

        private void LoadDrives()
        {
            var drives = DriveInfo.GetDrives()
                .Where(d => d.IsReady && d.DriveType == DriveType.Fixed)
                .Select(d => new FolderItem { Name = d.Name, FullPath = d.RootDirectory.FullName })
                .ToList();
            FoldersListBox.ItemsSource = drives;
            currentPath = null;
        }

        private void LoadFolders(string path)
        {
            try
            {
                var dirs = Directory.GetDirectories(path)
                    .Select(d => new FolderItem { Name = Path.GetFileName(d), FullPath = d })
                    .ToList();
                dirs.Insert(0, new FolderItem { Name = ".. (Up)", FullPath = Path.GetDirectoryName(path) });
                FoldersListBox.ItemsSource = dirs;
                currentPath = path;
                FoldersListBox.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                api.Dialogs.ShowErrorMessage($"Cannot access {path}: {ex.Message}", "Error");
            }
        }

        private void FolderPickerView_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter || e.Key == Key.Space)
            {
                if (FoldersListBox.SelectedItem is FolderItem item)
                {
                    if (item.Name == ".. (Up)")
                    {
                        if (currentPath != null) LoadFolders(item.FullPath);
                        else LoadDrives();
                    }
                    else
                    {
                        if (DriveInfo.GetDrives().Any(d => d.RootDirectory.FullName == item.FullPath))
                            LoadFolders(item.FullPath);
                        else
                            FolderSelected?.Invoke(this, item.FullPath);
                    }
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.Escape)
            {
                Cancelled?.Invoke(this, EventArgs.Empty);
                e.Handled = true;
            }
        }

        private void SelectButton_Click(object sender, RoutedEventArgs e)
        {
            if (FoldersListBox.SelectedItem is FolderItem item && item.Name != ".. (Up)")
                FolderSelected?.Invoke(this, item.FullPath);
        }

        private void UpButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentPath != null) LoadFolders(Path.GetDirectoryName(currentPath));
            else LoadDrives();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e) => Cancelled?.Invoke(this, EventArgs.Empty);
    }

    public class FolderItem
    {
        public string Name { get; set; }
        public string FullPath { get; set; }
    }
}