using Playnite.SDK.Models;
using System.Collections.Generic;
using System.Windows.Media;

namespace BetterScan
{
    public enum FileType { Executable, Rom, Link, Unknown }

    public class Candidate : ObservableObject
    {
        private bool _selected;
        private FileType _fileType;
        private Emulator _selectedEmulator;
        private string _newGameName;

        public string EntryFilePath { get; set; }
        public ImageSource IconData { get; set; }

        public bool Selected { get => _selected; set { _selected = value; OnPropertyChanged(); } }
        public FileType FileType { get => _fileType; set { _fileType = value; OnPropertyChanged(); OnPropertyChanged("FileTypeDescription"); } }
        public string FileTypeDescription => FileType.ToString();
        public Emulator SelectedEmulator { get => _selectedEmulator; set { _selectedEmulator = value; OnPropertyChanged(); } }
        public string NewGameName { get => _newGameName; set { _newGameName = value; OnPropertyChanged(); } }
    }

    public class ScanViewModel : ObservableObject
    {
        private string targetFolder = "";
        private List<Candidate> candidateList = new List<Candidate>();
        private bool isAllSelected;
        private List<Emulator> emulators = new List<Emulator>();

        public string TargetFolder { get => targetFolder; set => SetValue(ref targetFolder, value); }
        public List<Candidate> CandidateList { get => candidateList; set => SetValue(ref candidateList, value); }
        public List<Emulator> Emulators { get => emulators; set => SetValue(ref emulators, value); }

        public bool IsAllSelected
        {
            get => isAllSelected;
            set
            {
                SetValue(ref isAllSelected, value);
                foreach (var c in CandidateList) c.Selected = value;
            }
        }
    }
}