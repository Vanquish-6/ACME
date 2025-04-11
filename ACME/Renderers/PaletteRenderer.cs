using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI;
using DatReaderWriter.DBObjs;
using DatReaderWriter.Types;

namespace ACME.Renderers
{
    public class PaletteRenderer : IObjectRenderer
    {
        public void Render(Panel targetPanel, object data, Dictionary<string, object>? context)
        {
            if (data is not Palette palette)
            {
                RendererHelpers.AddErrorMessageToPanel(targetPanel, "Invalid object type passed to PaletteRenderer.");
                return;
            }

            targetPanel.Children.Clear();

            // Display basic info (ID)
            RendererHelpers.AddSimplePropertyRow(targetPanel, "Id", $"0x{palette.Id:X8}");

            // Add the color preview strip
            RendererHelpers.AddPalettePreview(targetPanel, palette.Colors);

            // Create an expander for the color list
            var expander = RendererHelpers.CreateNestedExpander(targetPanel, $"Colors ({palette.Colors?.Count ?? 0})", palette.Colors);
            if (palette.Colors != null && palette.Colors.Count > 0)
            {
                var contentPanel = expander.Content as Panel;
                if (contentPanel != null)
                {
                    for (int i = 0; i < palette.Colors.Count; i++)
                    {
                        var color = palette.Colors[i];
                        // Use AddSimplePropertyRow for consistent look inside the expander
                        RendererHelpers.AddSimplePropertyRow(contentPanel, $"[{i}]", $"Alpha: {color.Alpha}, Red: {color.Red}, Green: {color.Green}, Blue: {color.Blue}");
                    }
                }
            }
            // Expander is already added to targetPanel by CreateNestedExpander
        }
    }
} 