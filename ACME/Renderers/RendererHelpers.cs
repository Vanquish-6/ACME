using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using ACME.Utils; // For FontWeightValues
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.UI.Text; // For FontWeights
using Microsoft.UI.Xaml.Media.Imaging; // For WriteableBitmap
using Windows.Storage.Streams; // For WriteableBitmap operations
using System.Runtime.InteropServices.WindowsRuntime; // For AsStream / AsBuffer
using Windows.UI; // For Color definition

namespace ACME.Renderers
{
    /// <summary>
    /// Provides static helper methods for rendering UI elements in detail views.
    /// </summary>
    public static class RendererHelpers
    {
        private const int MaxItemsToShow = 50; // Keep relevant constants if needed by helpers

        /// <summary>
        /// Helper method to add a simple label and value row using a two-column Grid.
        /// </summary>
        public static void AddSimplePropertyRow(Panel parentPanel, string label, string value)
        {
             var grid = new Grid();
             grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
             grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
             grid.Margin = new Thickness(0, 3, 0, 3);

             grid.Children.Add(new TextBlock {
                 Text = label,
                 FontWeight = FontWeights.SemiBold,
                 HorizontalAlignment = HorizontalAlignment.Right,
                 VerticalAlignment = VerticalAlignment.Top,
                 Margin = new Thickness(0, 0, 15, 0)
             });
             var valueTextBlock = new TextBlock {
                 Text = value,
                 HorizontalAlignment = HorizontalAlignment.Left,
                 VerticalAlignment = VerticalAlignment.Top,
                 TextWrapping = TextWrapping.Wrap
             };
             Grid.SetColumn(valueTextBlock, 1);
             grid.Children.Add(valueTextBlock);

             parentPanel.Children.Add(grid);
        }

        /// <summary>
        /// Renders a collection of IDs using specific context lookups.
        /// Returns true if handled, false otherwise.
        /// </summary>
        public static bool RenderCollectionWithLookup(Panel panel, IEnumerable enumerable, string lookupKey, Dictionary<string, object>? context, int currentDepth = 0)
        {
            if (context == null || !context.TryGetValue(lookupKey, out var lookupObj))
            {
                Debug.WriteLine($"RenderCollectionWithLookup: Lookup key '{lookupKey}' not found in context.");
                AddErrorMessageToPanel(panel, $"Lookup data '{lookupKey}' not found.");
                return false;
            }

            Dictionary<uint, string>? uintLookup = lookupObj as Dictionary<uint, string>;
            Dictionary<int, string>? intLookup = lookupObj as Dictionary<int, string>;

            if (uintLookup == null && intLookup == null)
            {
                 Debug.WriteLine($"RenderCollectionWithLookup: Lookup key '{lookupKey}' is not a Dictionary<uint, string> or Dictionary<int, string>.");
                 AddErrorMessageToPanel(panel, $"Lookup data '{lookupKey}' is invalid type.");
                 return false; // Indicate not handled
            }

             List<uint> idList;
             try
             {
                 var itemsAsObjects = enumerable.Cast<object>().ToList();
                 if (itemsAsObjects.Count > 0)
                 {
                    var firstItem = itemsAsObjects[0];
                    if (!(firstItem is uint || firstItem is int))
                    {
                         throw new InvalidCastException($"Collection items are not of type uint or int (Actual type: {firstItem?.GetType().Name ?? "null"}).");
                    }
                    if (itemsAsObjects.Any(item => item.GetType() != firstItem?.GetType()))
                    {
                        Debug.WriteLine($"Warning: Collection for lookup '{lookupKey}' contains mixed integer types. Attempting conversion.");
                    }
                 }
                 idList = itemsAsObjects.Select(item => Convert.ToUInt32(item)).ToList();
             }
             catch (Exception ex)
             {
                  Debug.WriteLine($"RenderCollectionWithLookup: Failed to process or convert collection to List<uint>. Member: {lookupKey}, Error: {ex.Message}");
                  AddErrorMessageToPanel(panel, $"Cannot display collection items for '{lookupKey}'. Ensure items are integers.");
                  return false;
             }

             if (idList.Count == 0)
             {
                 panel.Children.Add(new TextBlock { Text = "(empty)", FontStyle = FontStyle.Italic });
                 return true;
             }

             int index = 0;
             var itemsPanel = new StackPanel();
             panel.Children.Add(itemsPanel);

             foreach (uint id in idList)
             {
                 string name = "Unknown";
                 if (uintLookup != null)
                 {
                     name = uintLookup.TryGetValue(id, out var lookedUpName) ? lookedUpName : "Unknown";
                 }
                 else if (intLookup != null)
                 {
                     try 
                     { 
                        int intId = Convert.ToInt32(id);
                        name = intLookup.TryGetValue(intId, out var lookedUpName) ? lookedUpName : "Unknown";
                     }
                     catch (OverflowException) 
                     { 
                        name = "(ID overflow for lookup)"; 
                     } 
                 }
                 itemsPanel.Children.Add(new TextBlock {
                    Text = $"[{index}]: {id} ({name})",
                    Margin = new Thickness(0, 0, 0, 2),
                    TextWrapping = TextWrapping.Wrap
                 });
                 index++;
                 if (index >= MaxItemsToShow)
                 {
                     itemsPanel.Children.Add(new TextBlock { Text = $"... ({idList.Count - index} more items not shown)", FontStyle = FontStyle.Italic, Margin = new Thickness(0, 4, 0, 0) });
                     break;
                 }
             }
             return true;
        }

        /// <summary>
        /// Adds a separator line to the panel.
        /// </summary>
        public static void AddSeparator(Panel panel)
        {
            panel.Children.Add(new Rectangle()
            {
                Fill = new SolidColorBrush(Microsoft.UI.Colors.LightGray),
                Height = 1,
                Margin = new Thickness(0, 8, 0, 8)
            });
        }

        /// <summary>
        /// Adds a section header to the panel.
        /// </summary>
        public static void AddSectionHeader(Panel panel, string text)
        {
            panel.Children.Add(new TextBlock()
            {
                Text = text,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 12, 0, 8) // Increased top margin
            });
        }

        /// <summary>
        /// Adds an informational message to the specified panel.
        /// </summary>
        public static void AddInfoMessageToPanel(Panel panel, string message, Windows.UI.Color color)
        {
            panel.Children.Add(new TextBlock()
            {
                Text = message,
                Foreground = new SolidColorBrush(color),
                Margin = new Thickness(0, 6, 0, 6), // Consistent margin
                TextWrapping = TextWrapping.Wrap
            });
        }

        /// <summary>
        /// Adds an error message to the specified panel.
        /// </summary>
        public static void AddErrorMessageToPanel(Panel panel, string message)
        {
             AddInfoMessageToPanel(panel, message, Microsoft.UI.Colors.Red);
        }

        /// <summary>
        /// Creates and adds a visual preview of a color palette to the panel.
        /// </summary>
        /// <param name="parentPanel">The panel to add the preview to.</param>
        /// <param name="colors">The list of colors in the palette.</param>
        public static void AddPalettePreview(Panel parentPanel, List<DatReaderWriter.Types.ColorARGB> colors)
        {
            if (colors == null || colors.Count == 0) return;

            const int blockSize = 10; // Pixel size of each color square (Increased)
            const int maxPreviewBlocksPerRow = 64; // Max blocks horizontally (Increased further)
            const int maxPreviewRows = 16; // Max rows vertically (Increased further)
            const int spacing = 0; // Pixel spacing between blocks (Removed)

            int actualBlocks = Math.Min(colors.Count, maxPreviewBlocksPerRow * maxPreviewRows);
            int blocksPerRow = Math.Min(colors.Count, maxPreviewBlocksPerRow);
            // Avoid division by zero if blocksPerRow becomes 0 (e.g., if colors.Count is 0 but actualBlocks > 0 somehow)
            if (blocksPerRow <= 0) blocksPerRow = 1;
            int numRows = (int)Math.Ceiling((double)actualBlocks / blocksPerRow);

            // Adjust width/height calculation for spacing = 0
            int bitmapWidth = blocksPerRow * (blockSize + spacing); 
            int bitmapHeight = numRows * (blockSize + spacing);

            if (bitmapWidth <= 0 || bitmapHeight <= 0) return; // Avoid creating 0-size bitmap

            var writeableBitmap = new WriteableBitmap(bitmapWidth, bitmapHeight);
            byte[] pixelData = new byte[bitmapWidth * bitmapHeight * 4]; // BGRA format

            for (int i = 0; i < actualBlocks; i++)
            {
                // Determine the color to draw
                int colorIndex = (colors.Count <= actualBlocks) ? i : (int)Math.Floor((double)i * colors.Count / actualBlocks);
                colorIndex = Math.Min(colorIndex, colors.Count - 1); // Ensure valid index
                var color = colors[colorIndex];

                // Calculate the top-left corner of the block
                int blockRow = i / blocksPerRow;
                int blockCol = i % blocksPerRow;
                int xOffset = blockCol * (blockSize + spacing);
                int yOffset = blockRow * (blockSize + spacing);

                // Draw the block into the pixel buffer
                for (int y = 0; y < blockSize; y++)
                {
                    for (int x = 0; x < blockSize; x++)
                    {
                        int pixelX = xOffset + x;
                        int pixelY = yOffset + y;
                        int pixelIndex = (pixelY * bitmapWidth + pixelX) * 4;

                        // Ensure we don't write outside the buffer bounds
                        if (pixelIndex >= 0 && pixelIndex + 3 < pixelData.Length)
                        {
                            // Write color in BGRA format
                            pixelData[pixelIndex + 0] = color.Blue;
                            pixelData[pixelIndex + 1] = color.Green;
                            pixelData[pixelIndex + 2] = color.Red;
                            pixelData[pixelIndex + 3] = color.Alpha;
                        }
                    }
                }
            }

            // Copy pixel data to the bitmap
            using (var stream = writeableBitmap.PixelBuffer.AsStream())
            {
                stream.Write(pixelData, 0, pixelData.Length);
            }

            // Create an Image control to display the bitmap
            var imageControl = new Image
            {
                Source = writeableBitmap,
                Stretch = Stretch.None, // Keep original pixel size
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 5, 0, 5)
            };

            parentPanel.Children.Add(imageControl);

            // Optionally add a note if not all colors are shown
            if (colors.Count > actualBlocks)
            {
                // AddInfoMessageToPanel(parentPanel, $"(Previewing {actualBlocks} of {colors.Count} colors)", Microsoft.UI.Colors.DarkGray);
            }
        }

        /// <summary>
        /// Determines a suitable title for an object based on its type and properties.
        /// </summary>
        public static string DetermineObjectTitle(object obj)
        {
            var type = obj.GetType();
            string typeName = type.Name;

            if (obj is KeyValuePair<string, string> stringPair)
            {
                return $"Chat Pose: {stringPair.Key}";
            }

            if (obj is KeyValuePair<object, object> kvp)
            {
                 return $"Entry: {kvp.Key}";
            }

            // --- NEW: Handle Wave (Sound) type specifically ---
            if (obj is DatReaderWriter.DBObjs.Wave waveObj)
            {
                return $"Sound: ID {waveObj.Id:X8}";
            }
            // --- END NEW ---

            if (type.Name.Contains("AnonymousType"))
            {
                 PropertyInfo? displayProp = type.GetProperty("DisplayText");
                 if (displayProp != null)
                 {
                      return displayProp.GetValue(obj)?.ToString() ?? typeName;
                 }
            }

            PropertyInfo? idProp = type.GetProperty("Id") ?? type.GetProperty("ID");
            PropertyInfo? nameProp = type.GetProperty("Name");

            try
            {
                if (idProp != null && nameProp != null)
                {
                    var id = idProp.GetValue(obj);
                    var name = nameProp.GetValue(obj);
                    return $"{typeName}: {name} ({id})";
                }
                else if (nameProp != null)
                {
                    var name = nameProp.GetValue(obj);
                    return $"{typeName}: {name}";
                }
                else if (idProp != null)
                {
                    var id = idProp.GetValue(obj);
                    return $"{typeName}: ID {id}";
                }
            }
            catch (Exception ex)
            {
                 Debug.WriteLine($"Error determining object title: {ex.Message}");
            }

            return typeName;
        }

        /// <summary>
        /// Gets public fields and readable public properties of an object.
        /// </summary>
        public static List<dynamic> GetObjectMembers(object obj)
        {
             var properties = obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                              // Filter out indexer properties (which require parameters)
                              .Where(p => p.CanRead && p.GetIndexParameters().Length == 0) 
                              .Select(p => new { Name = p.Name, Value = p.GetValue(obj), MemberInfo = (MemberInfo)p, DeclaringType = p.DeclaringType }); // Add DeclaringType
             var fields = obj.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance)
                           .Select(f => new { Name = f.Name, Value = f.GetValue(obj), MemberInfo = (MemberInfo)f, DeclaringType = f.DeclaringType }); // Add DeclaringType
            // Combine members
            return properties.Concat(fields).OrderBy(m => m.Name).ToList<dynamic>();
        }

        /// <summary>
        /// Creates a standard nested Expander, adds it to the parent, and returns it.
        /// The Expander's Content is set to a new StackPanel for the caller to populate.
        /// </summary>
        public static Expander CreateNestedExpander(Panel parentPanel, string title, object? dataItem)
        {
            string headerText = title;
            int count = 0;
            bool hasCount = false;

            if (dataItem is ICollection collection && collection.Count > 0)
            {
                count = collection.Count;
                hasCount = true;
            }
            else if (dataItem is IDictionary dictionary && dictionary.Count > 0)
            {
                count = dictionary.Count;
                hasCount = true;
            }
            // Add more collection types if needed

            if (hasCount)
            {
                headerText = $"{title} ({count})";
            }

            var expander = new Expander
            {
                Header = headerText,
                Margin = new Thickness(0, 3, 0, 3),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                // Set Content to a StackPanel for the caller
                Content = new StackPanel { Margin = new Thickness(20, 0, 0, 0) } // Standard indent for nested content
            };

            // Only add the expander if there's something to show (data is not null)
            // Or if it's an empty collection (header will indicate 0)
            if (dataItem != null || hasCount) 
            {
                 parentPanel.Children.Add(expander);
            }
            else
            {
                 // Optionally add a simple message if dataItem is null and not a collection
                 // AddInfoMessageToPanel(parentPanel, $"{title}: (null)", Colors.DarkGray);
                 // Or just don't add anything.
            }

            return expander;
        }

        /// <summary>
        /// Renders the public properties of an object into a target panel using reflection.
        /// Creates nested expanders for complex types or collections.
        /// </summary>
        public static void RenderObjectProperties(Panel targetPanel, object? obj, Dictionary<string, object>? context, int currentDepth = 0, int maxDepth = 5)
        {
            if (obj == null)
            {
                AddInfoMessageToPanel(targetPanel, "(null)", Colors.Gray);
                return;
            }

            if (currentDepth > maxDepth)
            {
                AddInfoMessageToPanel(targetPanel, "(Max recursion depth reached)", Colors.Orange);
                return;
            }

            var type = obj.GetType();

            // Basic Types Check (handle directly, don't reflect)
            if (type.IsPrimitive || type == typeof(string) || type == typeof(decimal) || type.IsEnum)
            {
                AddSimplePropertyRow(targetPanel, "Value", obj.ToString() ?? "null");
                return;
            }

            // Use GetObjectMembers (assuming it returns PropertyInfo/FieldInfo)
            // For simplicity here, let's use standard GetProperties directly.
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanRead).ToList();
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance).ToList();

            if (!properties.Any() && !fields.Any())
            {
                 AddInfoMessageToPanel(targetPanel, "(No public properties or fields)", Colors.Gray);
                 return;
            }

            foreach (var prop in properties)
            {
                try
                {
                    // Skip indexer properties
                    if (prop.GetIndexParameters().Length > 0) continue;

                    object? value = prop.GetValue(obj);
                    RenderMember(targetPanel, prop.Name, prop.PropertyType, value, context, currentDepth);
                }
                catch (Exception ex)
                {
                    AddErrorMessageToPanel(targetPanel, $"Error reading property {prop.Name}: {ex.Message}");
                    Debug.WriteLine($"Reflection Error reading property {type.Name}.{prop.Name}: {ex}");
                }
            }

            foreach (var field in fields)
            {
                 try
                 {
                    object? value = field.GetValue(obj);
                    RenderMember(targetPanel, field.Name, field.FieldType, value, context, currentDepth);
                 }
                 catch (Exception ex)
                 {
                    AddErrorMessageToPanel(targetPanel, $"Error reading field {field.Name}: {ex.Message}");
                    Debug.WriteLine($"Reflection Error reading field {type.Name}.{field.Name}: {ex}");
                 }
            }
        }

        /// <summary>
        /// Internal helper to render a single member (property or field).
        /// </summary>
        private static void RenderMember(Panel targetPanel, string memberName, Type memberType, object? value, Dictionary<string, object>? context, int currentDepth, int maxDepth = 5) 
        {
            if (memberType == typeof(string) || memberType.IsPrimitive || memberType.IsEnum || memberType == typeof(decimal))
            {
                string displayValue = value?.ToString() ?? "null";
                // Special formatting for uint IDs likely common
                if (value is uint uintVal && (memberName.EndsWith("Id") || memberName.EndsWith("ID")))
                {
                     displayValue = $"0x{uintVal:X8}";
                }
                AddSimplePropertyRow(targetPanel, memberName + ":", displayValue);
            }
            else if (value is IEnumerable enumerable && memberType != typeof(string)) // Handle collections (excluding strings)
            {
                 var collectionExpander = CreateNestedExpander(targetPanel, memberName, value as ICollection ?? value as IDictionary ?? value);
                 var contentPanel = collectionExpander.Content as Panel;
                 if (contentPanel != null && value != null)
                 {
                     int index = 0;
                     foreach (var item in enumerable)
                     {
                        if (index >= MaxItemsToShow) { AddInfoMessageToPanel(contentPanel, $"... ({GetCollectionCount(enumerable) - index} more)", Colors.Gray); break; }
                        // Render each item - potentially recursively
                        var itemExpander = CreateNestedExpander(contentPanel, $"[{index}]", item);
                        RenderObjectProperties(itemExpander.Content as Panel, item, context, currentDepth + 1, maxDepth);
                        index++;
                     }
                     if(index == 0)
                     {
                        AddInfoMessageToPanel(contentPanel, "(empty)", Colors.Gray);
                     }
                 }
            }
            else if (value != null && memberType.IsClass) // Handle nested complex objects
            {
                var nestedExpander = CreateNestedExpander(targetPanel, memberName, value);
                RenderObjectProperties(nestedExpander.Content as Panel, value, context, currentDepth + 1, maxDepth);
            }
            else // Handle null complex objects or other unhandled types
            {
                 AddSimplePropertyRow(targetPanel, memberName + ":", value?.ToString() ?? "null");
            }
        }

        /// <summary>
        /// Helper to get count from IEnumerable if possible, otherwise returns MaxItemsToShow+1 to avoid incorrect messages.
        /// </summary>
        private static int GetCollectionCount(IEnumerable enumerable)
        {
             if (enumerable is ICollection collection) return collection.Count;
             if (enumerable is IDictionary dictionary) return dictionary.Count;
             // Cannot efficiently get count for generic IEnumerable without iterating
             return MaxItemsToShow + 1; // Signal that we don't have an exact count
        }
    }
} 