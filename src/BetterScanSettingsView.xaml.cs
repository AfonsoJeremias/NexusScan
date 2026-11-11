using System.IO;
using System.Windows;
using System.Windows.Controls;
using Ookii.Dialogs.Wpf;

namespace BetterScan
{
    public partial class BetterScanSettingsView : UserControl
    {
        public BetterScanSettingsView() { InitializeComponent(); }

        private void AddFolderButton_Click(object sender, RoutedEventArgs e)
        {
            string folder = NewFolderTextBox.Text.Trim();
            if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
            {
                var settings = (DataContext as BetterScanSettingsViewModel)?.Settings;
                if (settings != null && !settings.PredefinedFolders.Contains(folder))
                {
                    settings.PredefinedFolders.Add(folder);
                    NewFolderTextBox.Clear();
                }
            }
            else MessageBox.Show("Please enter a valid folder path.", "Warning");
        }

        private void BrowseFolderButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new VistaFolderBrowserDialog
            {
                Description = "Select a folder to add",
                UseDescriptionForTitle = true
            };
            if (dialog.ShowDialog(Window.GetWindow(this)) == true)
                NewFolderTextBox.Text = dialog.SelectedPath;
        }

        private void RemoveFolderMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var settings = (DataContext as BetterScanSettingsViewModel)?.Settings;
            string sel = FoldersListBox.SelectedItem as string;
            if (!string.IsNullOrEmpty(sel)) settings?.PredefinedFolders.Remove(sel);
        }
    }
}