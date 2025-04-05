using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace ACME.Helpers
{
    /// <summary>
    /// Helper methods for working with Windows
    /// </summary>
    public static class WindowHelper
    {
        // Static reference to the main application window that can be set at app startup
        public static Window? MainWindow { get; set; }

        /// <summary>
        /// Gets the Window that contains the specified element
        /// </summary>
        /// <param name="element">The element to find the Window for</param>
        /// <returns>The Window containing the element, or null if not found</returns>
        public static Window? GetWindowForElement(UIElement element)
        {
            if (element == null)
                return null;

            // In WinUI 3, the simplest way is to use the static reference to MainWindow
            // that should be set at app startup
            return MainWindow;
        }
    }
} 