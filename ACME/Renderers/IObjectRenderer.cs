using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;

namespace ACME.Renderers
{
    /// <summary>
    /// Interface for classes responsible for rendering specific object types 
    /// into a UI panel.
    /// </summary>
    public interface IObjectRenderer
    {
        /// <summary>
        /// Renders the details of the provided data object into the target panel.
        /// </summary>
        /// <param name="targetPanel">The panel where the details should be rendered.</param>
        /// <param name="data">The object whose details are to be rendered.</param>
        /// <param name="context">Optional context dictionary containing lookup data or display hints.</param>
        void Render(Panel targetPanel, object data, Dictionary<string, object>? context);
    }
} 