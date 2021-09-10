using Microsoft.UI.Xaml;
using System;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace App1
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        public static Window CurrentWindow { get; private set; }

        public static MainPage MainPage { get; private set; }

        public static IntPtr HWND => WinRT.Interop.WindowNative.GetWindowHandle(CurrentWindow);

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();
        }

        /// <summary>
        /// Invoked when the application is launched normally by the end user.  Other entry points
        /// will be used such as when the application is launched to open a specific file.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            CurrentWindow = new Window();
            CurrentWindow.ExtendsContentIntoTitleBar = true;
            MainPage = new MainPage();
            CurrentWindow.Content = MainPage;

            // Ensure the current window is active
            CurrentWindow.Activate();
        }
    }
}
