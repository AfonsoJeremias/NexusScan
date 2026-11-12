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
            if (FoldersListBox.Items.Count > 0) FoldersListBox.SelectedIndex = 0;
        }

        private void LoadFolders(string path)
        {
            try
            {
                var dirs = Directory.GetDirectories(path)
                    .Select(d => new FolderItem { Name = Path.GetFileName(d), FullPath = d })
                    .ToList();
                
                // Adiciona ".." se não for uma raiz
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
            if (currentPath == null) return; // Já está nas unidades, não faz nada
            
            var parent = Path.GetDirectoryName(currentPath);
            if (string.IsNullOrEmpty(parent)) 
                LoadDrives(); // Se for raiz de HD (ex: C:\), volta pras unidades
            else 
                LoadFolders(parent);
        }

        private void EnterSelectedFolder()
        {
            if (FoldersListBox.SelectedItem is FolderItem item)
            {
                if (item.Name == ".. (Up)") 
                    NavigateUp();
                else if (DriveInfo.GetDrives().Any(d => d.RootDirectory.FullName == item.FullPath))
                    LoadFolders(item.FullPath); // Se for uma unidade, entra
                else
                    LoadFolders(item.FullPath); // Se for pasta, entra
            }
        }

        private void FolderPickerView_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // B (Voltar / Sair)
            if (e.Key == Key.Escape)
            {
                if (currentPath == null)
                    Cancelled?.Invoke(this, EventArgs.Empty); // Fecha o picker
                else
                    NavigateUp(); // Volta uma pasta
                
                e.Handled = true;
                return;
            }

            // A (Entrar na pasta)
            if (e.Key == Key.Enter || e.Key == Key.Space)
            {
                EnterSelectedFolder();
                e.Handled = true;
                return;
            }

            // X (Selecionar a pasta atual)
            if (e.Key == Key.X)
            {
                SelectCurrentFolder();
                e.Handled = true;
                return;
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