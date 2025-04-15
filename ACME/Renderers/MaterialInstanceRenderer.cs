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
    /// Renders details for MaterialInstance objects.
    /// </summary>
    public class MaterialInstanceRenderer : IObjectRenderer
    {
        public void Render(Panel targetPanel, object data, Dictionary<string, object>? context)
        {
            if (data is not MaterialInstance matInstance)
            {
                Debug.WriteLine($"MaterialInstanceRenderer: Received data is not a MaterialInstance object (Type: {data?.GetType().Name ?? "null"})");
                RendererHelpers.AddErrorMessageToPanel(targetPanel, "Invalid data type for MaterialInstanceRenderer.");
                return;
            }

            Debug.WriteLine($"--- MaterialInstanceRenderer.Render called for ID: 0x{matInstance.Id:X8} ---");

            var propertiesPanel = new StackPanel() { Margin = new Thickness(0, 0, 0, 20) };

            // Display base properties
            RendererHelpers.AddSimplePropertyRow(propertiesPanel, "Id:", $"0x{matInstance.Id:X8} ({matInstance.Id})");
            RendererHelpers.AddSimplePropertyRow(propertiesPanel, "DBObjType:", matInstance.DBObjType.ToString());
            RendererHelpers.AddSimplePropertyRow(propertiesPanel, "MaterialId:", $"0x{matInstance.MaterialId:X8}");
            RendererHelpers.AddSimplePropertyRow(propertiesPanel, "MaterialType:", $"0x{matInstance.MaterialType:X8}");
            RendererHelpers.AddSimplePropertyRow(propertiesPanel, "AllowStencilShadows:", matInstance.AllowStencilShadows.ToString());
            RendererHelpers.AddSimplePropertyRow(propertiesPanel, "WantDiscardGeometry:", matInstance.WantDiscardGeometry.ToString());

            RendererHelpers.AddSeparator(propertiesPanel);

            // ModifierRefs List
            RendererHelpers.RenderSimpleUintList(propertiesPanel, "ModifierRefs", matInstance.ModifierRefs);
            
            targetPanel.Children.Add(propertiesPanel);
        }
    }
} 