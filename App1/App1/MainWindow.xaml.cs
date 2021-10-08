using Microsoft.UI.Xaml;
using System;
// Needed for WindowId
using Microsoft.UI;
// Needed for AppWindow
using Microsoft.UI.Windowing;
// Needed for XAML hwnd interop
using WinRT.Interop;
using System.Runtime.InteropServices;

namespace App1
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        public readonly AppWindow m_appWindow;

        public MainWindow()
        {
            this.InitializeComponent();
            // Get the AppWindow for our XAML Window
            m_appWindow = GetAppWindowForCurrentWindow();
            if (m_appWindow != null)
            {
                // You now have an AppWindow object and can call its methods to manipulate the window.
                if (AppWindowTitleBar.IsCustomizationSupported())
                {
                }
            }
        }

        private AppWindow GetAppWindowForCurrentWindow()
        {
            IntPtr hWnd = WindowNative.GetWindowHandle(this);
            WindowId myWndId = Win32Interop.GetWindowIdFromWindow(hWnd);
            return AppWindow.GetFromWindowId(myWndId);
        }
    }
}