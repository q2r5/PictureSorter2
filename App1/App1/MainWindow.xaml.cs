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
        // For the simplicity of this code snippet we import the DLL and declare
        // the methods in the MainWindow class here. It is recommended that you
        // break this out into a support class that you use wherever needed instead.
        // See the Windows App SDK windowing sample for more details.
        [DllImport("Microsoft.Internal.FrameworkUdk.dll", EntryPoint = "Windowing_GetWindowHandleFromWindowId", CharSet = CharSet.Unicode)]
        private static extern IntPtr GetWindowHandleFromWindowId(WindowId windowId, out IntPtr result);

        [DllImport("Microsoft.Internal.FrameworkUdk.dll", EntryPoint = "Windowing_GetWindowIdFromWindowHandle", CharSet = CharSet.Unicode)]
        private static extern IntPtr GetWindowIdFromWindowHandle(IntPtr hwnd, out WindowId result);

        public readonly AppWindow m_appWindow;

        public MainWindow()
        {
            this.InitializeComponent();
            // Get the AppWindow for our XAML Window
            m_appWindow = GetAppWindowForCurrentWindow();
            if (m_appWindow != null)
            {
                // You now have an AppWindow object and can call its methods to manipulate the window.
                if (AppWindowTitleBar.IsCustomizationsSupported())
                {
                }
            }
        }

        private AppWindow GetAppWindowForCurrentWindow()
        {
            IntPtr hWnd = WindowNative.GetWindowHandle(this);
            _ = GetWindowIdFromWindowHandle(hWnd, out WindowId myWndId);
            return AppWindow.GetFromWindowId(myWndId);
        }
    }
}