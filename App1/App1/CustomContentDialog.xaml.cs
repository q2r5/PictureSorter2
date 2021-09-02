using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace App1
{
    public sealed partial class CustomContentDialog : ContentDialog
    {
        private readonly ApplicationDataContainer appData = ApplicationData.Current.LocalSettings;

        public CustomContentDialog()
        {
            InitializeComponent();
        }

        private void CustomContentDialog_Opened(ContentDialog sender, ContentDialogOpenedEventArgs args)
        {
            NeverShowCheckBox.IsChecked = false;
        }

        private void NeverShowCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            appData.Values["HideMoveDialog"] = true;
        }

        private void NeverShowCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            appData.Values["HideMoveDialog"] = false;
        }

        private void CustomContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (NeverShowCheckBox.IsChecked ?? false)
            {
                appData.Values["MoveConflictOption"] = 1;
            }
        }

        private void CustomContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (NeverShowCheckBox.IsChecked ?? false)
            {
                appData.Values["MoveConflictOption"] = 2;
            }
        }
    }
}
