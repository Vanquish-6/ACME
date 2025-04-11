using DatReaderWriter;
using DatReaderWriter.DBObjs;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ACME.Renderers
{
    public class SurfaceTextureRenderer : IObjectRenderer
    {
        public void Render(Panel panel, object obj, Dictionary<string, object>? context)
        {
            if (obj is not SurfaceTexture surfaceTexture)
            {
                RendererHelpers.AddErrorMessageToPanel(panel, "Invalid object type passed to SurfaceTextureRenderer.");
                return;
            }

            panel.Children.Clear(); // Clear previous content

            // Basic Info
            RendererHelpers.AddSimplePropertyRow(panel, "ID", $"0x{surfaceTexture.Id:X8}");
            RendererHelpers.AddSimplePropertyRow(panel, "Type", surfaceTexture.Type.ToString());
            RendererHelpers.AddSeparator(panel);
            RendererHelpers.AddSectionHeader(panel, "Referenced Textures");

            if (surfaceTexture.Textures == null || surfaceTexture.Textures.Count == 0)
            {
                RendererHelpers.AddInfoMessageToPanel(panel, "No textures referenced.", Microsoft.UI.Colors.Gray);
                return;
            }

            // Attempt to get the database from the context (check for null context)
            DatDatabase? db = null;
            if (context != null && context.TryGetValue("Database", out var dbObject) && dbObject is DatDatabase database)
            {
                db = database;
            }
            else
            {
                RendererHelpers.AddErrorMessageToPanel(panel, "Database context not found or context is null. Cannot load referenced textures.");
                // Also display the raw IDs as fallback
                foreach (var textureId in surfaceTexture.Textures)
                {
                    RendererHelpers.AddSimplePropertyRow(panel, "Texture ID", $"0x{textureId:X8}");
                }
                return;
            }

            // Get the RenderSurfaceRenderer instance (assuming it's registered and accessible somehow - might need adjustment)
            // This is a simplification; a better approach might involve dependency injection or a service locator.
            var renderSurfaceRenderer = new RenderSurfaceRenderer(); // WARNING: Direct instantiation might not be ideal

            foreach (var textureId in surfaceTexture.Textures)
            {
                if (db.TryReadFile<RenderSurface>(textureId, out var renderSurface))
                {
                    if (renderSurface != null)
                    {
                        // Create a sub-panel for each RenderSurface
                        // Need to use a StackPanel here, as Panel itself doesn't arrange vertically by default.
                        var subPanel = new StackPanel { Margin = new Microsoft.UI.Xaml.Thickness(0, 5, 0, 10) }; 
                        
                        // Render the RenderSurface using its renderer, passing the sub-panel
                        renderSurfaceRenderer.Render(subPanel, renderSurface, context);
                        panel.Children.Add(subPanel); // Add the StackPanel to the main Panel
                    }
                    else
                    {
                        // Instead of a general error, add a placeholder for the specific failed texture
                        var failureText = new TextBlock
                        {
                            Text = $"Loaded RenderSurface (0x{textureId:X8}) was null.",
                            FontStyle = Windows.UI.Text.FontStyle.Italic,
                            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
                            Margin = new Microsoft.UI.Xaml.Thickness(0, 5, 0, 10)
                        };
                        panel.Children.Add(failureText);
                    }
                }
                else
                {
                    // Instead of a general error, add a placeholder for the specific failed texture
                    var failureText = new TextBlock
                    {
                        Text = $"Could not load referenced texture: 0x{textureId:X8}",
                        FontStyle = Windows.UI.Text.FontStyle.Italic,
                        Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
                        Margin = new Microsoft.UI.Xaml.Thickness(0, 5, 0, 10)
                    };
                    panel.Children.Add(failureText);
                }
            }
        }
    }
} 