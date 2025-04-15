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
    /// Renders details for Clothing objects.
    /// </summary>
    public class ClothingRenderer : IObjectRenderer
    {
        public void Render(Panel targetPanel, object data, Dictionary<string, object>? context)
        {
            if (data is not Clothing clothing)
            {
                Debug.WriteLine($"ClothingRenderer: Received data is not a Clothing object (Type: {data?.GetType().Name ?? "null"})");
                RendererHelpers.AddErrorMessageToPanel(targetPanel, "Invalid data type for ClothingRenderer.");
                return;
            }

            Debug.WriteLine($"--- ClothingRenderer.Render called for ID: 0x{clothing.Id:X8} ---");

            var propertiesPanel = new StackPanel() { Margin = new Thickness(0, 0, 0, 20) };

            // Display base properties
            RendererHelpers.AddSimplePropertyRow(propertiesPanel, "Id:", $"0x{clothing.Id:X8} ({clothing.Id})");
            RendererHelpers.AddSimplePropertyRow(propertiesPanel, "DBObjType:", clothing.DBObjType.ToString());

            RendererHelpers.AddSeparator(propertiesPanel);

            // ClothingBaseEffects Dictionary (using nested rendering)
            var baseEffectsExpander = RendererHelpers.CreateNestedExpander(propertiesPanel, "ClothingBaseEffects", clothing.ClothingBaseEffects);
            if (baseEffectsExpander.Content is Panel baseEffectsPanel)
            {
                RendererHelpers.RenderObjectProperties(baseEffectsPanel, clothing.ClothingBaseEffects, context);
            }

            RendererHelpers.AddSeparator(propertiesPanel);

            // ClothingSubPalEffects Dictionary (using nested rendering)
            var subPalEffectsExpander = RendererHelpers.CreateNestedExpander(propertiesPanel, "ClothingSubPalEffects", clothing.ClothingSubPalEffects);
            if (subPalEffectsExpander.Content is Panel subPalEffectsPanel)
            {
                RendererHelpers.RenderObjectProperties(subPalEffectsPanel, clothing.ClothingSubPalEffects, context);
            }

            targetPanel.Children.Add(propertiesPanel);
        }
    }
} 