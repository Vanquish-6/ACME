using ACME.Managers;
using ACME.Models;
using ACME.Utils;
using DatReaderWriter.DBObjs;
using DatReaderWriter.Enums;
using DatReaderWriter;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Microsoft.UI;
using System.Collections.Generic;
using System.Linq;

namespace ACME.Renderers
{
    public class SurfaceRenderer : IObjectRenderer
    {
        public void Render(Panel targetPanel, object data, Dictionary<string, object>? context)
        {
            targetPanel.Children.Clear(); // Clear previous content

            if (data is not Surface surface)
            {
                RendererHelpers.AddErrorMessageToPanel(targetPanel, "Invalid object type passed to SurfaceRenderer.");
                return;
            }

            DatDatabase? db = null;
            if (context?.TryGetValue("Database", out var dbObject) == true && dbObject is DatDatabase database)
            {
                db = database;
            }
            else
            {
                RendererHelpers.AddErrorMessageToPanel(targetPanel, "Database context not found.");
                return;
            }

            RendererHelpers.AddSimplePropertyRow(targetPanel, "ID", $"0x{surface.Id:X8} (Note: Surface IDs are not stored in the DAT)");
            RendererHelpers.AddSimplePropertyRow(targetPanel, "Type", surface.Type.ToString());
            RendererHelpers.AddSimplePropertyRow(targetPanel, "Translucency", surface.Translucency.ToString());
            RendererHelpers.AddSimplePropertyRow(targetPanel, "Luminosity", surface.Luminosity.ToString());
            RendererHelpers.AddSimplePropertyRow(targetPanel, "Diffuse", surface.Diffuse.ToString());

            if (surface.Type.HasFlag(SurfaceType.Base1Image) || surface.Type.HasFlag(SurfaceType.Base1ClipMap))
            {
                RendererHelpers.AddSimplePropertyRow(targetPanel, "Orig Texture ID", $"0x{surface.OrigTextureId:X8}");
                RendererHelpers.AddSimplePropertyRow(targetPanel, "Orig Palette ID", $"0x{surface.OrigPaletteId:X8}");

                if (db.TryReadFile<SurfaceTexture>(surface.OrigTextureId, out var surfaceTexture) && surfaceTexture != null)
                {
                    if (surfaceTexture.Textures == null || surfaceTexture.Textures.Count == 0)
                    {
                        RendererHelpers.AddSimplePropertyRow(targetPanel, "Textures", "None referenced in SurfaceTexture");
                    }
                    else
                    {
                        RendererHelpers.AddSectionHeader(targetPanel, "Referenced Textures");

                        var textureContainer = new StackPanel { Orientation = Orientation.Vertical, Spacing = 5 };
                        targetPanel.Children.Add(textureContainer);

                        var renderSurfaceRenderer = new RenderSurfaceRenderer();

                        foreach (var textureId in surfaceTexture.Textures)
                        {
                            if (db.TryReadFile<RenderSurface>(textureId, out var renderSurface) && renderSurface != null)
                            {
                                var subPanelContainer = new Border
                                {
                                    BorderBrush = new SolidColorBrush(Colors.Gray),
                                    BorderThickness = new Microsoft.UI.Xaml.Thickness(1),
                                    Padding = new Microsoft.UI.Xaml.Thickness(5),
                                    Margin = new Microsoft.UI.Xaml.Thickness(0, 5, 0, 0)
                                };
                                var subPanel = new StackPanel();
                                subPanelContainer.Child = subPanel;
                                
                                textureContainer.Children.Add(subPanelContainer);

                                renderSurfaceRenderer.Render(subPanel, renderSurface, context);
                            }
                            else
                            {
                                RendererHelpers.AddErrorMessageToPanel(textureContainer, $"Could not load RenderSurface: 0x{textureId:X8}");
                            }
                        }
                    }
                }
                else
                {
                    RendererHelpers.AddErrorMessageToPanel(targetPanel, $"Could not load referenced SurfaceTexture: 0x{surface.OrigTextureId:X8}");
                }
            }
            else if (surface.ColorValue != null) // Likely Base1Solid or similar
            {
                RendererHelpers.AddSimplePropertyRow(targetPanel, "Color (ARGB)", $"#{surface.ColorValue.Alpha:X2}{surface.ColorValue.Red:X2}{surface.ColorValue.Green:X2}{surface.ColorValue.Blue:X2}");

                RendererHelpers.AddSectionHeader(targetPanel, "Color Preview");

                var color = ColorHelper.FromArgb(surface.ColorValue.Alpha, surface.ColorValue.Red, surface.ColorValue.Green, surface.ColorValue.Blue);

                var colorBox = new Border()
                {
                    Width = 50, // Example size
                    Height = 50, // Example size
                    Background = new SolidColorBrush(color),
                    BorderBrush = new SolidColorBrush(Colors.DarkGray),
                    BorderThickness = new Microsoft.UI.Xaml.Thickness(1),
                    Margin = new Microsoft.UI.Xaml.Thickness(0, 5, 0, 0)
                };
                targetPanel.Children.Add(colorBox);
            }
            else
            {
                RendererHelpers.AddSimplePropertyRow(targetPanel, "Data", "Surface type does not directly contain viewable image or color data.");
            }
        }
    }
} 