using Ryujinx.UI.App.Common;
using Ryujinx.UI.Common.Models;
using System.Linq;

namespace Ryujinx.Ava.UI.ViewModels
{
    public partial class DlcSelectViewModel : BaseModel
    {
        private DownloadableContentModel[] _dlcs;
        private DownloadableContentModel _selectedDlc;
        
        public DownloadableContentModel[] Dlcs
        {
            get => _dlcs;
            set
            {
                _dlcs = value;

                OnPropertyChanged();
            }
        }
        #nullable enable
        public DownloadableContentModel? SelectedDlc
        {
            get => _selectedDlc;
            set
            {
                _selectedDlc = value;

                OnPropertyChanged();
            }
        }
        #nullable disable
        
        public DlcSelectViewModel(ulong titleId, ApplicationLibrary appLibrary)
        {
            _dlcs = appLibrary.FindDlcsFor(titleId)
                .OrderBy(it => it.IsBundled ? 0 : 1)
                .ThenBy(it => it.TitleId)
                .ToArray();
        }
    }
}
