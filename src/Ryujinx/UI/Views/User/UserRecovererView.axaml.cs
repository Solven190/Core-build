using Avalonia.Controls;
using Avalonia.Interactivity;
using FluentAvalonia.UI.Controls;
using FluentAvalonia.UI.Navigation;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Ava.UI.Controls;
using NavigationEventArgs = FluentAvalonia.UI.Navigation.FANavigationEventArgs;

namespace Ryujinx.Ava.UI.Views.User
{
    public partial class UserRecovererView : UserControl
    {
        private NavigationDialogHost _parent;

        public UserRecovererView()
        {
            InitializeComponent();
            AddHandler(FAFrame.NavigatedToEvent, (_, e) =>
            {
                NavigatedTo(e);
            }, RoutingStrategies.Direct);
        }

        private void NavigatedTo(NavigationEventArgs arg)
        {
            if (Program.PreviewerDetached)
            {
                switch (arg.NavigationMode)
                {
                    case FANavigationMode.New:
                        NavigationDialogHost parent = (NavigationDialogHost)arg.Parameter;

                        _parent = parent;

                        ((FAContentDialog)_parent.Parent)?.Title = $"{LocaleManager.Instance[LocaleKeys.UserProfileWindowTitle]} - {LocaleManager.Instance[LocaleKeys.UserProfilesRecoverHeading]}";

                        break;
                }
            }
        }

        private void GoBack(object sender, RoutedEventArgs e)
        {
            _parent?.GoBack();
        }

        private void Recover(object sender, RoutedEventArgs e)
        {
            _parent?.RecoverLostAccounts();
        }
    }
}
