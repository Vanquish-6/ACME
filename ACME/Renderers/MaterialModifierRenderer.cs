using System;
using System.Collections.Generic;
using System.Diagnostics;
using DatReaderWriter.DBObjs;
using DatReaderWriter.Types;
using DatReaderWriter.Enums;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ACME.Renderers
{
    /// <summary>
    /// Renders details for MaterialModifier objects.
    /// </summary>
    public class MaterialModifierRenderer : IObjectRenderer
    {
        public void Render(Panel targetPanel, object data, Dictionary<string, object>? context)
        {
            if (data is not MaterialModifier matModifier)
            {
                Debug.WriteLine($"MaterialModifierRenderer: Received data is not a MaterialModifier object (Type: {data?.GetType().Name ?? "null"})");
                RendererHelpers.AddErrorMessageToPanel(targetPanel, "Invalid data type for MaterialModifierRenderer.");
                return;
            }

            Debug.WriteLine($"--- MaterialModifierRenderer.Render called for ID: 0x{matModifier.Id:X8} ---");

            var propertiesPanel = new StackPanel() { Margin = new Thickness(0, 0, 0, 20) };

            // Display base properties
            RendererHelpers.AddSimplePropertyRow(propertiesPanel, "Id:", $"0x{matModifier.Id:X8} ({matModifier.Id})");
            RendererHelpers.AddSimplePropertyRow(propertiesPanel, "DBObjType:", matModifier.DBObjType.ToString());

            RendererHelpers.AddSeparator(propertiesPanel);

            // MaterialProperties List (using nested rendering)
            var propertiesExpander = RendererHelpers.CreateNestedExpander(propertiesPanel, "MaterialProperties", matModifier.MaterialProperties);
            if (propertiesExpander.Content is Panel propertiesContentPanel)
            {
                // Since MaterialProperties is a List, render each item within the expander
                if (matModifier.MaterialProperties != null && matModifier.MaterialProperties.Count > 0)
                {
                    int index = 0;
                    foreach (var prop in matModifier.MaterialProperties)
                    {
                        var itemExpander = RendererHelpers.CreateNestedExpander(propertiesContentPanel, $"[{index}]", prop);
                        if (itemExpander.Content is Panel itemPanel)
                        {
                             RendererHelpers.RenderObjectProperties(itemPanel, prop, context);
                        }
                        index++;
                    }
                }
                else
                {
                     RendererHelpers.AddInfoMessageToPanel(propertiesContentPanel, "(empty)", Colors.Gray);
                }
            }

            targetPanel.Children.Add(propertiesPanel);
        }
    }
} 