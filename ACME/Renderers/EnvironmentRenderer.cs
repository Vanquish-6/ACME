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
    /// Renders details for Environment objects.
    /// </summary>
    public class EnvironmentRenderer : IObjectRenderer
    {
        public void Render(Panel targetPanel, object data, Dictionary<string, object>? context)
        {
            if (data is not DatReaderWriter.DBObjs.Environment env)
            {
                Debug.WriteLine($"EnvironmentRenderer: Received data is not an Environment object (Type: {data?.GetType().Name ?? "null"})");
                RendererHelpers.AddErrorMessageToPanel(targetPanel, "Invalid data type for EnvironmentRenderer.");
                return;
            }

            Debug.WriteLine($"--- EnvironmentRenderer.Render called for ID: 0x{env.Id:X8} ---");

            var propertiesPanel = new StackPanel() { Margin = new Thickness(0, 0, 0, 20) };

            // Display base properties
            RendererHelpers.AddSimplePropertyRow(propertiesPanel, "Id:", $"0x{env.Id:X8} ({env.Id})");
            RendererHelpers.AddSimplePropertyRow(propertiesPanel, "DBObjType:", env.DBObjType.ToString());

            RendererHelpers.AddSeparator(propertiesPanel);

            // Cells Dictionary (using nested rendering)
            var cellsExpander = RendererHelpers.CreateNestedExpander(propertiesPanel, "Cells", env.Cells);
            if (cellsExpander.Content is Panel cellsPanel)
            {
                RendererHelpers.RenderObjectProperties(cellsPanel, env.Cells, context);
            }

            targetPanel.Children.Add(propertiesPanel);
        }
    }
} 