using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Styling;
using FluentAvalonia.UI.Controls;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Ava.UI.ViewModels;
using Ryujinx.UI.Common.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Ryujinx.Ava.UI.Windows
{
    public partial class XCITrimmerWindow : UserControl
    {
        public XCITrimmerViewModel ViewModel;

        public XCITrimmerWindow()
        {
            DataContext = this;

            InitializeComponent();
        }

        public XCITrimmerWindow(MainWindowViewModel mainWindowViewModel)
        {
            DataContext = ViewModel = new XCITrimmerViewModel(mainWindowViewModel);

            InitializeComponent();
        }

        public static async Task Show(MainWindowViewModel mainWindowViewModel)
        {
            FAContentDialog contentDialog = new()
            {
                PrimaryButtonText = "",
                SecondaryButtonText = "",
                CloseButtonText = "",
                Content = new XCITrimmerWindow(mainWindowViewModel),
                Title = string.Format(LocaleManager.Instance[LocaleKeys.XCITrimmerWindowTitle]),
            };

            Style bottomBorder = new(x => x.OfType<Grid>().Name("DialogSpace").Child().OfType<Border>());
            bottomBorder.Setters.Add(new Setter(IsVisibleProperty, false));

            contentDialog.Styles.Add(bottomBorder);

            await contentDialog.ShowAsync();
        }

        private void Trim(object sender, RoutedEventArgs e)
        {
            ViewModel.TrimSelected();
        }

        private void Untrim(object sender, RoutedEventArgs e)
        {
            ViewModel.UntrimSelected();
        }

        private void Close(object sender, RoutedEventArgs e)
        {
            ((FAContentDialog)Parent)?.Hide();
        }

        private void Cancel(Object sender, RoutedEventArgs e)
        {
            ViewModel.Cancel = true;
        }

        public void Sort_Checked(object sender, RoutedEventArgs args)
        {
            if (sender is RadioButton { IsChecked: true, Tag: string sortField })
                ViewModel.SortingField = Enum.Parse<XCITrimmerViewModel.SortField>(sortField);
        }

        public void Order_Checked(object sender, RoutedEventArgs args)
        {
            if (sender is RadioButton { IsChecked: true, Tag: string sortOrder })
                ViewModel.SortingAscending = sortOrder is "Ascending";
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            List<XCITrimmerFileModel> added = [.. e.AddedItems.OfType<XCITrimmerFileModel>()];
            List<XCITrimmerFileModel> removed = [.. e.RemovedItems.OfType<XCITrimmerFileModel>()];

            foreach (XCITrimmerFileModel applicationData in added)
            {
                ViewModel.Select(applicationData);
            }

            foreach (XCITrimmerFileModel applicationData in removed)
            {
                ViewModel.Deselect(applicationData);
            }
        }
    }
}
