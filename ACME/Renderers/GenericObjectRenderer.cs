using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using ACME.Utils; // FontWeightValues
using DatReaderWriter.Types; // Position
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.UI.Text; // FontStyle
using DatReaderWriter.DBObjs; // <<< Added for Palette type check

namespace ACME.Renderers
{
    /// <summary>
    /// Renders details for generic objects by reflecting their properties and fields.
    /// </summary>
    public class GenericObjectRenderer : IObjectRenderer
    {
        private const int MaxItemsToShow = 50; // Consider moving to RendererHelpers or config
        private const int MaxRecursionDepth = 5;

        // Constants for Member Names
        private const string IdPropertyName = "Id";
        private const string NamePropertyName = "Name";
        private const string HeaderFlagsPropertyName = "HeaderFlags";
        private const string DbObjTypePropertyName = "DBObjType";
        private const string DataCategoryPropertyName = "DataCategory";
        private const string ComponentsPropertyName = "Components";
        private const string PrimaryStartAreasPropertyName = "PrimaryStartAreas";
        private const string SecondaryStartAreasPropertyName = "SecondaryStartAreas";
        private const string SpellsPropertyName = "Spells";
        private const string KeyPropertyName = "Key";
        private const string ValuePropertyName = "Value";
        private const string FramePropertyName = "Frame"; // Added for RenderPosition

        public void Render(Panel targetPanel, object data, Dictionary<string, object>? context)
        {
            Debug.WriteLine($"--- GenericObjectRenderer.Render called with data type: {data?.GetType().FullName ?? "null"} ---"); // Diagnostic message
            
            // The targetPanel here is expected to be the container *within* the main DetailPanel
            // where the specific object's details should be rendered.
            // Title is handled by the main DetailRenderer.

            var propertiesPanel = new StackPanel() { Margin = new Thickness(0, 0, 0, 20) }; // Use 0 left margin as DetailRenderer adds padding

            // --- SPECIAL: KeyValuePair<string, string> for Chat Poses ---
            if (data is KeyValuePair<string, string> stringPair)
            {
                RendererHelpers.AddSimplePropertyRow(propertiesPanel, "Pose Name:", stringPair.Key);
                RendererHelpers.AddSimplePropertyRow(propertiesPanel, "Action:", stringPair.Value);
            }
            // --- END SPECIAL ---
            // --- NEW: Check if data is a dictionary first ---
            else if (data is IDictionary dictionary)
            {
                RenderDictionary(propertiesPanel, dictionary, context, 0); // Start rendering the dictionary
            }
            // --- NEW: Check if data is a general collection ---
            else if (data is IEnumerable enumerable && !(data is string)) // Handle non-dictionary, non-string collections
            {
                 RenderCollection(propertiesPanel, enumerable, context, 0); // Render the collection's items
            }
            // --- ELSE: Handle as a regular object ---
            else
            {
                // Get members using the helper
                var allMembers = RendererHelpers.GetObjectMembers(data);

                // --- Display Id and Name first ---
                var idMember = allMembers.FirstOrDefault(m => string.Equals(m.Name, IdPropertyName, StringComparison.OrdinalIgnoreCase));
                var nameMember = allMembers.FirstOrDefault(m => string.Equals(m.Name, NamePropertyName, StringComparison.OrdinalIgnoreCase));

                bool displayedHeader = false;
                if (idMember != null)
                {
                    DisplaySingleMember(propertiesPanel, idMember, context, 0);
                    displayedHeader = true;
                }
                if (nameMember != null)
                {
                    DisplaySingleMember(propertiesPanel, nameMember, context, 0);
                    displayedHeader = true;
                }

                if (displayedHeader)
                {
                    RendererHelpers.AddSeparator(propertiesPanel);
                }
                // --- END SECTION ---

                // Get remaining members, filter out Id/Name, sort alphabetically
                var remainingMembers = allMembers
                    .Where(m => !(string.Equals(m.Name, IdPropertyName, StringComparison.OrdinalIgnoreCase) ||
                                   string.Equals(m.Name, NamePropertyName, StringComparison.OrdinalIgnoreCase)))
                    .OrderBy(m => m.Name);

                foreach (var member in remainingMembers)
                {
                    // Skip common base members from DBObj unless they are Id/Name (already handled or skipped)
                    var declaringTypeName = member.DeclaringType?.Name ?? string.Empty;
                    if (declaringTypeName == "DBObj" && (member.Name == HeaderFlagsPropertyName || member.Name == DbObjTypePropertyName || member.Name == DataCategoryPropertyName))
                    {
                        continue;
                    }
                    DisplaySingleMember(propertiesPanel, member, context, 0);
                }

                // Add a final separator for visual spacing if there were remaining members
                if (remainingMembers.Any())
                {
                    RendererHelpers.AddSeparator(propertiesPanel);
                }
            }
            // --- END Object Handling ---

            targetPanel.Children.Add(propertiesPanel); // Add the content to the passed-in panel
        }

        // --- Internal Rendering Logic (Moved from DetailRenderer, now private within GenericObjectRenderer) ---

        /// <summary>
        /// Helper method to display a single member (property or field) in a Grid row.
        /// </summary>
        private void DisplaySingleMember(StackPanel panel, dynamic member, Dictionary<string, object>? displayContext, int currentDepth)
        {
            string memberName = "Unknown Member"; // Default name in case of early error
            try
            {
                memberName = member.Name; // Get name early for error message
                var memberGrid = new Grid();
                memberGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });
                memberGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
                memberGrid.Margin = new Thickness(0, 3, 0, 3);

                var nameTextBlock = new TextBlock()
                {
                    Text = member.Name,
                    FontWeight = FontWeightValues.SemiBold,
                    VerticalAlignment = VerticalAlignment.Top,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(0, 0, 15, 0)
                };
                Grid.SetColumn(nameTextBlock, 0);
                memberGrid.Children.Add(nameTextBlock);

                var valuePanel = new StackPanel()
                {
                    VerticalAlignment = VerticalAlignment.Top,
                    HorizontalAlignment = HorizontalAlignment.Left
                };
                Grid.SetColumn(valuePanel, 1);
                memberGrid.Children.Add(valuePanel);

                AddPropertyValue(valuePanel, member.Value, memberName, displayContext, currentDepth);

                panel.Children.Add(memberGrid);
            }
            catch (TargetInvocationException tie) // Specific exception for issues within the invoked member
            {
                 Debug.WriteLine($"GenericObjectRenderer: TargetInvocationException rendering member '{memberName}': {tie.InnerException?.Message ?? tie.Message}");
                 RendererHelpers.AddErrorMessageToPanel(panel, $"Error accessing member '{memberName}': {tie.InnerException?.Message ?? tie.Message}");
            }
            catch (Exception ex) // General fallback
            {
                Debug.WriteLine($"GenericObjectRenderer: Exception rendering member '{memberName}': {ex.Message}");
                RendererHelpers.AddErrorMessageToPanel(panel, $"Error reading member '{memberName}': {ex.Message}");
            }
        }

        /// <summary>
        /// Adds a property value to the specified panel with appropriate formatting.
        /// </summary>
        private void AddPropertyValue(Panel panel, object? value, string memberName, Dictionary<string, object>? displayContext, int currentDepth)
        {
            if (currentDepth > MaxRecursionDepth)
            {
                 panel.Children.Add(new TextBlock { Text = "{ Max Recursion Depth Reached }", FontStyle = FontStyle.Italic, Foreground=new SolidColorBrush(Colors.OrangeRed) });
                 return;
            }

            if (value == null)
            {
                panel.Children.Add(new TextBlock { Text = "null", FontStyle = FontStyle.Italic, Foreground = new SolidColorBrush(Microsoft.UI.Colors.Gray) });
                return;
            }

            var valueType = value.GetType();

            // --- Simple Types ---
            if (valueType.IsPrimitive || value is string || value is DateTime || value is decimal || value is Enum)
            {
                panel.Children.Add(new TextBlock { Text = value.ToString(), TextWrapping = TextWrapping.Wrap, MaxWidth = 450 });
                return;
            }

            // --- Specific Type Handling: Position ---
            if (value is Position pos)
            {
                RenderPosition(panel, pos);
                return;
            }

            // --- NEW: KeyValuePair Handling ---
            if (valueType.IsGenericType && valueType.GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
            {
                RenderKeyValuePair(panel, value, valueType, displayContext, currentDepth); 
                return;
            }

            // --- Collection with Lookup Handling (Leverage Helper) ---
            // Although this is the generic renderer, a collection of IDs might still benefit from lookup
            bool collectionHandledByLookup = false;
            // Make sure it's not a dictionary before attempting collection lookup
            if (value is IEnumerable enumerableValue && valueType.IsGenericType && !(value is IDictionary))
            {
                var elementType = valueType.GetGenericArguments().FirstOrDefault();
                // Check if the element type is uint or int
                if (elementType == typeof(uint) || elementType == typeof(int))
                {
                    string potentialLookupKey = memberName switch {
                        ComponentsPropertyName => "ComponentLookup",
                        PrimaryStartAreasPropertyName => "StartAreaLookup",
                        SecondaryStartAreasPropertyName => "StartAreaLookup",
                        SpellsPropertyName => "SpellLookup",
                        _ => memberName + "Lookup" // Generic fallback
                    };

                    // Use RendererHelpers.RenderCollectionWithLookup
                    collectionHandledByLookup = RendererHelpers.RenderCollectionWithLookup(panel, enumerableValue, potentialLookupKey, displayContext, currentDepth);
                    if (!collectionHandledByLookup && (memberName == ComponentsPropertyName || memberName == SpellsPropertyName || memberName == PrimaryStartAreasPropertyName || memberName == SecondaryStartAreasPropertyName))
                    {
                         Debug.WriteLine($"GenericRenderer: No specific lookup key '{potentialLookupKey}' found in context for collection '{memberName}'. Standard rendering will apply.");
                    }
                }
            }

            if (collectionHandledByLookup)
            {
                 return; // Handled by lookup
            }
            // --- END Collection with Lookup ---

            // --- Specific Type/Name Handling: GfxObj.DrawingBSP ---
            if (valueType.Name == "DrawingBSP" || memberName == "DrawingBSP") // Check type name or member name
            {
                panel.Children.Add(new TextBlock { Text = "{ BSP Tree (Display Limited) }", FontStyle = FontStyle.Italic, Foreground = new SolidColorBrush(Colors.Gray) });
                return; // Stop rendering this branch further
            }

            // --- General IEnumerable Handling (BEFORE Complex Object) ---
            if (value is IEnumerable generalEnumerable && !(value is string) && !(value is IDictionary)) 
            {
                 RenderCollection(panel, generalEnumerable, displayContext, currentDepth);
                 return;
            }

            // --- Dictionary Handling ---
            if (value is IDictionary dictionary)
            {
                RenderDictionary(panel, dictionary, displayContext, currentDepth); // Pass depth
                return;
            }

            // --- Standard Collection Handling ---
            if (value is IEnumerable enumerable && !(value is string))
            {
                 RenderCollection(panel, enumerable, displayContext, currentDepth); // Pass depth
                 return;
            }

            // --- Nested Object Handling ---
            if (!valueType.IsValueType && valueType != typeof(string) && currentDepth < MaxRecursionDepth)
            {
                RenderComplexObjectMembers(panel, value, displayContext, currentDepth + 1); // Recurse
                return;
            }

            // --- Complex Object Fallback (Recursion) ---
            if (!valueType.IsPrimitive && valueType != typeof(string))
            {
                RenderComplexObjectMembers(panel, value, displayContext, currentDepth + 1); // Recurse
                return;
            }

            // --- Fallback for other types ---
            panel.Children.Add(new TextBlock { Text = value.ToString(), TextWrapping = TextWrapping.Wrap, MaxWidth = 450 });
        }

        /// <summary>
        /// Renders a DatReaderWriter.Types.Position.
        /// </summary>
        private void RenderPosition(Panel panel, Position pos)
        {
            var posPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 4) };
            posPanel.Children.Add(new TextBlock { Text = $"CellId: 0x{pos.CellId:X8}" });
            if (pos.Frame != null)
            {
                posPanel.Children.Add(new TextBlock { Text = $"Origin: X={pos.Frame.Origin.X:F2}, Y={pos.Frame.Origin.Y:F2}, Z={pos.Frame.Origin.Z:F2}" });
            }
            else
            {
                posPanel.Children.Add(new TextBlock { Text = "Frame: null", FontStyle = FontStyle.Italic });
            }
            panel.Children.Add(posPanel);
        }

        /// <summary>
        /// Renders the details of a DatReaderWriter.Types.Frame (Origin and Orientation).
        /// </summary>
        private void RenderFrameDetails(Panel panel, DatReaderWriter.Types.Frame frame)
        {
            if (frame != null)
            {
                panel.Children.Add(new TextBlock { Text = $"Origin: X={frame.Origin.X:F2}, Y={frame.Origin.Y:F2}, Z={frame.Origin.Z:F2}", TextWrapping = TextWrapping.Wrap });
                // Assuming Orientation is Quaternion, format appropriately
                panel.Children.Add(new TextBlock { Text = $"Orientation: W={frame.Orientation.W:F4}, X={frame.Orientation.X:F4}, Y={frame.Orientation.Y:F4}, Z={frame.Orientation.Z:F4}", TextWrapping = TextWrapping.Wrap });
            }
            else
            {
                panel.Children.Add(new TextBlock { Text = "Frame: null", FontStyle = FontStyle.Italic });
            }
        }

        /// <summary>
        /// Renders a KeyValuePair<,>.
        /// </summary>
        private void RenderKeyValuePair(Panel panel, object kvpObject, Type kvpType, Dictionary<string, object>? context, int currentDepth)
        {
            if (currentDepth > MaxRecursionDepth)
            {
                panel.Children.Add(new TextBlock { Text = "{ Max Recursion Depth Reached }", FontStyle = FontStyle.Italic, Foreground = new SolidColorBrush(Colors.OrangeRed) });
                return;
            }

            // Get Key and Value using reflection (since we don't know the generic types)
            var keyProp = kvpType.GetProperty(KeyPropertyName);
            var valueProp = kvpType.GetProperty(ValuePropertyName);

            object? key = keyProp?.GetValue(kvpObject);
            object? value = valueProp?.GetValue(kvpObject);

            var keyPanel = new StackPanel { Orientation = Orientation.Horizontal };
            keyPanel.Children.Add(new TextBlock { Text = "Key:", FontWeight = FontWeightValues.SemiBold, Margin = new Thickness(0, 0, 8, 0) });
            AddPropertyValue(keyPanel, key, KeyPropertyName, context, currentDepth + 1); // Recurse for key
            panel.Children.Add(keyPanel);

            var valuePanel = new StackPanel { Margin = new Thickness(12, 4, 0, 0) }; 
            valuePanel.Children.Add(new TextBlock { Text = "Value:", FontWeight = FontWeightValues.SemiBold, Margin = new Thickness(0, 0, 8, 4) });
            AddPropertyValue(valuePanel, value, ValuePropertyName, context, currentDepth + 1); // Recurse for value
            panel.Children.Add(valuePanel);
        }

        /// <summary>
        /// Renders an IDictionary recursively.
        /// </summary>
        private void RenderDictionary(Panel panel, IDictionary dictionary, Dictionary<string, object>? context, int currentDepth)
        {
            if (currentDepth > MaxRecursionDepth)
            {
                panel.Children.Add(new TextBlock { Text = "{ Max Recursion Depth Reached }", FontStyle = FontStyle.Italic, Foreground = new SolidColorBrush(Colors.OrangeRed) });
                return;
            }

            panel.Children.Add(new TextBlock { Text = $"Dictionary ({dictionary.Count} entries)", FontWeight = FontWeightValues.Normal, FontStyle = FontStyle.Italic, Margin = new Thickness(0, 0, 0, 4) });

            if (dictionary.Count == 0)
            {
                panel.Children.Add(new TextBlock { Text = "(empty)", FontStyle = FontStyle.Italic, Margin = new Thickness(12, 0, 0, 0) });
                return;
            }

            var itemsPanel = new StackPanel { Margin = new Thickness(12, 0, 0, 0) };
            panel.Children.Add(itemsPanel);

            int index = 0;
            foreach (DictionaryEntry entry in dictionary)
            {
                 var entryPanel = new StackPanel { Margin = new Thickness(0, 6, 0, 6) };

                 var keyPanel = new StackPanel { Orientation = Orientation.Horizontal };
                 keyPanel.Children.Add(new TextBlock { Text = "Key:", FontWeight = FontWeightValues.SemiBold, Margin = new Thickness(0, 0, 8, 0) });
                 AddPropertyValue(keyPanel, entry.Key, KeyPropertyName, context, currentDepth + 1);
                 entryPanel.Children.Add(keyPanel);

                 var valuePanel = new StackPanel { Margin = new Thickness(12, 4, 0, 0) };
                 AddPropertyValue(valuePanel, entry.Value, ValuePropertyName, context, currentDepth + 1);
                 entryPanel.Children.Add(valuePanel);

                 itemsPanel.Children.Add(entryPanel);

                 if (index < dictionary.Count -1 && index < MaxItemsToShow -1)
                 {
                     itemsPanel.Children.Add(new Rectangle { Height = 1, Fill = new SolidColorBrush(Microsoft.UI.Colors.Gray) { Opacity = 0.3 }, Margin = new Thickness(0, 6, 0, 6) });
                 }

                 index++;
                 if (index >= MaxItemsToShow)
                 {
                      itemsPanel.Children.Add(new TextBlock { Text = $"... ({dictionary.Count - index} more entries not shown)", FontStyle = FontStyle.Italic, Margin = new Thickness(0, 8, 0, 0) });
                      break;
                 }
            }
        }

        /// <summary>
        /// Renders a generic collection (IEnumerable) into the panel.
        /// </summary>
        private void RenderCollection(Panel panel, IEnumerable enumerable, Dictionary<string, object>? context, int currentDepth)
        {
            if (currentDepth > MaxRecursionDepth)
            {
                panel.Children.Add(new TextBlock { Text = "{ Max Recursion Depth Reached }", FontStyle = FontStyle.Italic, Foreground = new SolidColorBrush(Colors.OrangeRed) });
                return;
            }

            int count = 0;
            var itemsPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 5) }; // Panel for all items

            try
            {
                int index = 0;
                foreach (var item in enumerable)
                {
                    // --- START MODIFICATION: Handle Position items with Expander ---
                    if (item is Position pos) // Check if the item is a Position
                    {
                        var expander = new Expander
                        {
                            // Use index and CellId for the header
                            Header = new TextBlock { Text = $"[{index}] CellId: 0x{pos.CellId:X8}", FontWeight = FontWeights.SemiBold },
                            Margin = new Thickness(0, 2, 0, 2) // Add some vertical spacing between items
                        };
                        // Create a panel for the expander's content and indent it
                        var contentPanel = new StackPanel { Margin = new Thickness(20, 0, 0, 0) };
                        RenderFrameDetails(contentPanel, pos.Frame); // Use helper to render frame details
                        expander.Content = contentPanel;
                        itemsPanel.Children.Add(expander); // Add the expander to the list
                    }
                    else // Original logic for other item types
                    {
                        var itemPanel = new StackPanel { Margin = new Thickness(0, 2, 0, 2) }; // Panel for a single item's details
                        // Recursively call AddPropertyValue to render the item's details
                        // Pass a generic memberName like "Item N" as context for the nested call
                        AddPropertyValue(itemPanel, item, $"Item {index}", context, currentDepth + 1);
                        itemsPanel.Children.Add(itemPanel); // Add the rendered item panel
                    }
                    // --- END MODIFICATION ---

                    count++;
                    index++; // Increment index regardless of item type
                }
            }
            catch (Exception ex)
            {
                RendererHelpers.AddErrorMessageToPanel(itemsPanel, $"Error iterating collection: {ex.Message}");
            }

            if (count == 0)
            {
                itemsPanel.Children.Add(new TextBlock { Text = "{ Empty Collection }", FontStyle = FontStyle.Italic, Foreground = new SolidColorBrush(Colors.Gray) });
            }

            panel.Children.Add(itemsPanel); // Add the panel containing all rendered items/expanders
        }

        /// <summary>
        /// Renders nested complex objects recursively.
        /// </summary>
        private void RenderComplexObjectMembers(Panel panel, object obj, Dictionary<string, object>? context, int currentDepth)
        {
             // Use helper to get members
             var members = RendererHelpers.GetObjectMembers(obj);
             if (!members.Any())
             {
                RendererHelpers.AddInfoMessageToPanel(panel, "(No public properties or fields found)", Colors.Gray);
                return;
             }

             var nestedPropertiesPanel = new StackPanel { Margin = new Thickness(12, 0, 0, 0) }; // Indent nested members
             foreach (var member in members)
             {
                  // Skip common base members from DBObj in nested views too?
                 var declaringTypeName = member.DeclaringType?.Name ?? string.Empty;
                 if (declaringTypeName == "DBObj" && 
                     (member.Name == HeaderFlagsPropertyName || member.Name == DbObjTypePropertyName || member.Name == DataCategoryPropertyName || member.Name == IdPropertyName || member.Name == NamePropertyName))
                 {
                     continue; // Skip base props and Id/Name in nested view
                 }
                  DisplaySingleMember(nestedPropertiesPanel, member, context, currentDepth); // Pass currentDepth (recursion increment handled in AddPropertyValue)
             }
             panel.Children.Add(nestedPropertiesPanel);
        }
    }
} 