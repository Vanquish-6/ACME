using Microsoft.UI.Xaml;
using System;
using System.IO;
using System.Runtime.InteropServices;
using Windows.ApplicationModel;
using WinRT.Interop;
using System.Diagnostics;

namespace ACME.Properties
{
    internal class IconHelper
    {
        // Define the required Win32 constants and functions
        private const int ICON_SMALL = 0;
        private const int ICON_BIG = 1;
        private const int WM_SETICON = 0x0080;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr LoadImage(IntPtr hinst, string lpszName, uint uType, 
                                               int cxDesired, int cyDesired, uint fuLoad);

        // Constants for LoadImage
        private const uint IMAGE_ICON = 1;
        private const uint LR_LOADFROMFILE = 0x00000010;

        public static void SetWindowIcon(Window window)
        {
            try
            {
                // Get the window handle
                var hwnd = WindowNative.GetWindowHandle(window);

                // Try to find the icon file
                string[] possiblePaths = new[]
                {
                    Path.Combine(AppContext.BaseDirectory, "Assets", "ACMELogo.ico"),
                    Path.Combine(Environment.CurrentDirectory, "Assets", "ACMELogo.ico"),
                };

                foreach (var iconPath in possiblePaths)
                {
                    if (File.Exists(iconPath))
                    {
                        Debug.WriteLine($"Found icon at: {iconPath}");
                        
                        // Load the icon using LoadImage
                        IntPtr hIcon = LoadImage(IntPtr.Zero, iconPath, IMAGE_ICON, 
                                                32, 32, LR_LOADFROMFILE);
                        
                        if (hIcon != IntPtr.Zero)
                        {
                            // Set small icon
                            SendMessage(hwnd, WM_SETICON, (IntPtr)ICON_SMALL, hIcon);
                            
                            // Set big icon
                            SendMessage(hwnd, WM_SETICON, (IntPtr)ICON_BIG, hIcon);
                            
                            Debug.WriteLine("Icon set successfully using Win32 API");
                            return; // Success, exit the method
                        }
                        else
                        {
                            Debug.WriteLine("Failed to load icon");
                        }
                    }
                }
                
                Debug.WriteLine("No suitable icon file found");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error setting window icon: {ex.Message}");
            }
        }
    }
} 