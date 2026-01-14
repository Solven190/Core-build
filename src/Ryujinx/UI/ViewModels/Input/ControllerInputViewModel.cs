using Avalonia.Svg.Skia;
using CommunityToolkit.Mvvm.ComponentModel;
using Ryujinx.Ava.UI.Models.Input;
using Ryujinx.Ava.UI.Views.Input;

namespace Ryujinx.Ava.UI.ViewModels.Input
{
    public partial class ControllerInputViewModel : BaseModel
    {
        public GamepadInputConfig Config
        {
            get;
            set
            {
                field = value;

                OnPropertyChanged();
            }
        }

        public StickVisualizer Visualizer
        {
            get;
            set
            {
                field = value;

                OnPropertyChanged();
            }
        }

        public bool IsLeft
        {
            get;
            set
            {
                field = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSides));
            }
        }

        public bool IsRight
        {
            get;
            set
            {
                field = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSides));
            }
        }

        public bool HasSides => IsLeft ^ IsRight;
        
        [ObservableProperty]
        private SvgImage _image;

        public readonly InputViewModel ParentModel;
        
        public ControllerInputViewModel(InputViewModel model, GamepadInputConfig config, StickVisualizer visualizer)
        {
            ParentModel = model;
            Visualizer = visualizer;
            model.NotifyChangesEvent += OnParentModelChanged;
            OnParentModelChanged();
            Config = config;
        }

        public async void ShowMotionConfig()
        {
            await MotionInputView.Show(this);
        }

        public async void ShowRumbleConfig()
        {
            await RumbleInputView.Show(this);
        }

        public void OnParentModelChanged()
        {
            IsLeft = ParentModel.IsLeft;
            IsRight = ParentModel.IsRight;
            Image = ParentModel.Image;
        }
    }
}
