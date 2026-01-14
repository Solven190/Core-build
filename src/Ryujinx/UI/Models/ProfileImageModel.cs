using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Ryujinx.Ava.UI.ViewModels;

namespace Ryujinx.Ava.UI.Models
{
    public partial class ProfileImageModel(string name, byte[] data) : BaseModel
    {
        public string Name { get; set; } = name;
        public byte[] Data { get; set; } = data;

        [ObservableProperty]
        private SolidColorBrush _backgroundColor = new(Colors.White);
    }
}
