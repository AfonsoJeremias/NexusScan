using Playnite.SDK;
using Playnite.SDK.Data;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace BetterScan
{
    public class BetterScanSettings : ObservableObject
    {
        private string matchPattern = "*.exe,*.bat,*.cmd,*.com,*.ps1";
        public string MatchPattern { get => matchPattern; set => SetValue(ref matchPattern, value); }

        private string skipPattern = "*\\playnite\\*";
        public string SkipPattern { get => skipPattern; set => SetValue(ref skipPattern, value); }

        private string romExtensions = ".nes,.iso,.bin,.cue,.sfc,.smc,.gb,.gba,.nds,.psx,.ps2,.wbfs,.nsp,.xci,.rvz,.gcm,.mds,.mdf,.z64,.n64,.v64,.rom";
        public string RomExtensions { get => romExtensions; set => SetValue(ref romExtensions, value); }

        private bool optionUseFolder = true;
        public bool OptionUseFolder { get => optionUseFolder; set => SetValue(ref optionUseFolder, value); }

        private bool optionRelPath = true;
        public bool OptionRelPath { get => optionRelPath; set => SetValue(ref optionRelPath, value); }

        private bool optionSearchAdjacentIco = true;
        public bool OptionSearchAdjacentIco { get => optionSearchAdjacentIco; set => SetValue(ref optionSearchAdjacentIco, value); }

        private bool recursiveSearch = true;
        public bool RecursiveSearch { get => recursiveSearch; set => SetValue(ref recursiveSearch, value); }

        private bool skipDuplicates = true;
        public bool SkipDuplicates { get => skipDuplicates; set => SetValue(ref skipDuplicates, value); }

        private string targetFolder = "";
        public string TargetFolder { get => targetFolder; set => SetValue(ref targetFolder, value); }

        private ObservableCollection<string> predefinedFolders = new ObservableCollection<string>();
        public ObservableCollection<string> PredefinedFolders { get => predefinedFolders; set => SetValue(ref predefinedFolders, value); }
    }

    public class BetterScanSettingsViewModel : ObservableObject, ISettings
    {
        private readonly BetterScan plugin;
        private BetterScanSettings editingClone;
        private BetterScanSettings settings;

        public BetterScanSettings Settings
        {
            get => settings;
            set { settings = value; OnPropertyChanged(); }
        }

        public BetterScanSettingsViewModel(BetterScan plugin)
        {
            this.plugin = plugin;
            var saved = plugin.LoadPluginSettings<BetterScanSettings>();
            Settings = saved ?? new BetterScanSettings();
        }

        public void BeginEdit() => editingClone = Serialization.GetClone(Settings);
        public void CancelEdit() => Settings = editingClone;
        public void EndEdit() => plugin.SavePluginSettings(Settings);
        public bool VerifySettings(out List<string> errors) { errors = new List<string>(); return true; }
    }
}