using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Microsoft.UI;
using Windows.UI.Text;
using Microsoft.UI.Text;
using ACME.Utils; // For FontWeightValues
using System.Reflection; // For GetProperties
using System.Linq;
using System.Diagnostics;
using DatReaderWriter.Types; // Needed for Position type check
using DatReaderWriter.Enums; // For MagicSchool and SpellCategory enums
using System.Threading.Tasks;
using System.Globalization; // For NumberStyles in TryParse
using DatReaderWriter.DBObjs; // Make sure DBObjs namespace is included

namespace ACME.Renderers
{
    /// <summary>
    /// Responsible for orchestrating the rendering of details for selected items
    /// into a designated StackPanel using specific renderers.
    /// </summary>
    public class DetailRenderer
    {
        private readonly StackPanel _detailPanel;
        private readonly Dictionary<Type, IObjectRenderer> _renderers = new();
        private readonly IObjectRenderer _genericRenderer = new GenericObjectRenderer(); // Fallback renderer
        private readonly Dictionary<string, IObjectRenderer> _contextRenderers = new(); // For context-based dispatch

        public DetailRenderer(StackPanel detailPanel)
        {
            _detailPanel = detailPanel ?? throw new ArgumentNullException(nameof(detailPanel));
            RegisterRenderers();
        }

        /// <summary>
        /// Refreshes the displayed details for the given item
        /// </summary>
        public void RefreshDetails(object item, Dictionary<string, object>? context = null)
        {
            DisplayItemDetails(item, context);
        }

        /// <summary>
        /// Registers specific renderers for known types or context hints.
        /// </summary>
        private void RegisterRenderers()
        { 
            // Register by Type (Example - if we had a specific renderer for SpellSet)
            _renderers.Add(typeof(DatReaderWriter.Types.SpellSet), new SpellSetRenderer());
            
            // Using internal SpellBaseRenderer implementation
            _renderers.Add(typeof(DatReaderWriter.Types.SpellBase), new SpellBaseRenderer());
            
            // Register by Context Hint (ObjectType)
            _contextRenderers.Add("HeritageGroupCG", new HeritageGroupRenderer()); 
            
            // Register Animation renderer
            _renderers.Add(typeof(DatReaderWriter.DBObjs.Animation), new AnimationRenderer());
            
            // --- BEGIN NEW RENDERERS ---
            _renderers.Add(typeof(RenderSurface), new RenderSurfaceRenderer());
            _renderers.Add(typeof(SurfaceTexture), new SurfaceTextureRenderer());
            _renderers.Add(typeof(Surface), new SurfaceRenderer());
            // --- END NEW RENDERERS ---
            _renderers.Add(typeof(Palette), new PaletteRenderer());
            _renderers.Add(typeof(PaletteSet), new PaletteSetRenderer());
            
            // Add other context-specific renderers here
        }

        public void DisplayItemDetails(object? selectedItem, object? displayContext = null)
        {   
            _detailPanel.Children.Clear(); // Clear panel before adding new content

            if (selectedItem == null)
            {
                AddInfoMessage("Select an item to view details.");
                return;
            }

            var contextDict = displayContext as Dictionary<string, object> ?? new Dictionary<string, object>();
            
            // Add a reference to this renderer's refresh method
            if (!contextDict.ContainsKey("RefreshDetailView"))
            {
                contextDict["RefreshDetailView"] = new Action<object>(obj => RefreshDetails(obj, contextDict));
            }
            
            string title = RendererHelpers.DetermineObjectTitle(selectedItem); // Use helper
            AddTitle(title);

            // Create a container panel for the specific renderer's content
            // This allows the main DetailRenderer to manage the title consistently.
            var contentPanel = new StackPanel() { Margin = new Thickness(12, 0, 0, 0) }; // Add padding for content area
            _detailPanel.Children.Add(contentPanel);

            IObjectRenderer? selectedRenderer = null;

            // --- NEW: Explicit handling for Dictionary Entry Anonymous Type ---
            var itemType = selectedItem.GetType();
            var displayTextProp = itemType.GetProperty("DisplayText");
            var valueProp = itemType.GetProperty("Value");

            if (itemType.Name.Contains("AnonymousType") && displayTextProp != null && valueProp != null && displayTextProp.PropertyType == typeof(string))
            {
                string key = displayTextProp.GetValue(selectedItem) as string ?? "(unknown key)";
                object? value = valueProp.GetValue(selectedItem);

                RendererHelpers.AddSimplePropertyRow(contentPanel, "Key", key);
                RendererHelpers.AddSeparator(contentPanel);

                if (value != null)
                {
                    RendererHelpers.AddSectionHeader(contentPanel, "Value");

                    // --- NEW: Directly display if Value is string, otherwise render --- 
                    if (value is string stringValue)
                    {
                         // Use AddSimplePropertyRow or just a TextBlock to display the string value directly
                         RendererHelpers.AddSimplePropertyRow(contentPanel, "", stringValue); // Empty label for direct value display
                    }
                    else 
                    { 
                        // Value is not a string (e.g., ChatEmoteData), find appropriate renderer
                        IObjectRenderer? valueRenderer = null;
                        // 1. Try context-based dispatch for the value
                        if (contextDict?.TryGetValue("ObjectType", out var valueObjTypeHint) == true && 
                            valueObjTypeHint is string valueHint && 
                            _contextRenderers.TryGetValue(valueHint, out valueRenderer))
                        { /* Found renderer */ }
                        // 2. If no context match, try type-based dispatch for the value
                        if (valueRenderer == null)
                        {
                             _renderers.TryGetValue(value.GetType(), out valueRenderer);
                        }
                        // 3. Fallback to generic renderer for the value
                        valueRenderer ??= _genericRenderer;
                        
                        try
                        {
                            Debug.WriteLine($"--- DetailRenderer: About to render VALUE of type: {value?.GetType().FullName ?? "null"} ---"); // Diagnostic message
                            // Render the VALUE using the chosen renderer
                            valueRenderer.Render(contentPanel, value, contextDict);
                        }
                        catch (Exception ex)
                        {
                            RendererHelpers.AddErrorMessageToPanel(contentPanel, $"Error rendering Value details: {ex.Message}");
                            System.Diagnostics.Debug.WriteLine($"Rendering Error (Value): {ex}\nContext: {displayContext}");
                        }
                    }
                    // --- END NEW ---
                }
                else
                {
                     RendererHelpers.AddInfoMessageToPanel(contentPanel, "Value: null", Microsoft.UI.Colors.Gray);
                }
                return; // Handled this specific anonymous type
            }
            // --- END NEW SECTION ---

            // If not the specific anonymous type, proceed with normal renderer selection
            // 1. Try context-based dispatch first
            if (contextDict?.TryGetValue("ObjectType", out var objTypeHint) == true && 
                objTypeHint is string hint && 
                _contextRenderers.TryGetValue(hint, out selectedRenderer))
            {
                 // Found renderer based on context hint
            }
            
            // 2. If no context match, try type-based dispatch
            if (selectedRenderer == null)
            {
                _renderers.TryGetValue(selectedItem.GetType(), out selectedRenderer);
            }

            // 3. Fallback to generic renderer
            selectedRenderer ??= _genericRenderer;

            try
            {
                selectedRenderer.Render(contentPanel, selectedItem, contextDict);
            }
            catch (Exception ex)
            {
                // Add error message within the content panel
                RendererHelpers.AddErrorMessageToPanel(contentPanel, $"Error rendering details: {ex.Message}");
                 // Optionally log the full exception
                 System.Diagnostics.Debug.WriteLine($"Rendering Error: {ex}\nContext: {displayContext}");
            }
        }

        /// <summary>
        /// Clears the detail panel and displays a message.
        /// </summary>
        public void ClearAndSetMessage(string message, bool isError = false)
        { 
            _detailPanel.Children.Clear();
            AddInfoMessage(message, isError ? Microsoft.UI.Colors.OrangeRed : Microsoft.UI.Colors.Gray);
        }

        /// <summary>
        /// Clears the detail panel and adds the standard title and message.
        /// </summary>
        public void ClearAndAddDefaultTitle()
        {
             _detailPanel.Children.Clear();
             AddTitle("Item Details");
             AddInfoMessage("Select an item from the list to view details.", Microsoft.UI.Colors.Gray);
        }

        /// <summary>
        /// Adds a title to the main detail panel.
        /// </summary>
        private void AddTitle(string title)
        {
            // Assume title should always be added at the top, even if content rendering fails
            _detailPanel.Children.Insert(0, new TextBlock()
            {
                Text = title,
                Style = (Style)Application.Current.Resources["TitleTextBlockStyle"],
                Margin = new Thickness(0, 0, 0, 12) // Ensure bottom margin
            });
        }

        /// <summary>
        /// Adds an informational message to the main detail panel.
        /// </summary>
        public void AddInfoMessage(string message, Windows.UI.Color? color = null)
        {
             // Use helper, add to the main panel (_detailPanel)
             RendererHelpers.AddInfoMessageToPanel(_detailPanel, message, color ?? Microsoft.UI.Colors.Gray);
        }
    }
} 