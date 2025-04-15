using System;
using System.Collections.Generic;
using System.Diagnostics;
using DatReaderWriter.DBObjs;
using DatReaderWriter.Types;
using DatReaderWriter.Enums;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ACME.Renderers
{
    /// <summary>
    /// Renders details for GfxObj objects.
    /// </summary>
    public class GfxObjRenderer : IObjectRenderer
    {
        public void Render(Panel targetPanel, object data, Dictionary<string, object>? context)
        {
            if (data is not GfxObj gfxObj)
            {
                Debug.WriteLine($"GfxObjRenderer: Received data is not a GfxObj (Type: {data?.GetType().Name ?? "null"})");
                RendererHelpers.AddErrorMessageToPanel(targetPanel, "Invalid data type for GfxObjRenderer.");
                return;
            }

            Debug.WriteLine($"--- GfxObjRenderer.Render called for ID: 0x{gfxObj.Id:X8} ---");

            var propertiesPanel = new StackPanel() { Margin = new Thickness(0, 0, 0, 20) };

            // Display base properties
            RendererHelpers.AddSimplePropertyRow(propertiesPanel, "Id:", $"0x{gfxObj.Id:X8} ({gfxObj.Id})");
            RendererHelpers.AddSimplePropertyRow(propertiesPanel, "DBObjType:", gfxObj.DBObjType.ToString());
            RendererHelpers.AddSimplePropertyRow(propertiesPanel, "Flags:", gfxObj.Flags.ToString());
            if (gfxObj.Flags.HasFlag(GfxObjFlags.HasDIDDegrade))
            {
                RendererHelpers.AddSimplePropertyRow(propertiesPanel, "DIDDegrade:", $"0x{gfxObj.DIDDegrade:X8} ({gfxObj.DIDDegrade})");
            }
            RendererHelpers.AddSimplePropertyRow(propertiesPanel, "SortCenter:", gfxObj.SortCenter.ToString());

            RendererHelpers.AddSeparator(propertiesPanel);

            // Surfaces List
            RendererHelpers.RenderSimpleUintList(propertiesPanel, "Surfaces", gfxObj.Surfaces);

            RendererHelpers.AddSeparator(propertiesPanel);

            // Vertex Array (using nested rendering)
            var vertexExpander = RendererHelpers.CreateNestedExpander(propertiesPanel, "VertexArray", gfxObj.VertexArray);
            if (vertexExpander.Content is Panel vertexPanel) {
                 RendererHelpers.RenderObjectProperties(vertexPanel, gfxObj.VertexArray, context); // Render nested properties
            }

            RendererHelpers.AddSeparator(propertiesPanel);

            // Physics Data (Conditional)
            if (gfxObj.Flags.HasFlag(GfxObjFlags.HasPhysics))
            {
                RendererHelpers.AddSectionHeader(propertiesPanel, "Physics Data");
                // Physics Polygons (using nested rendering)
                var physPolyExpander = RendererHelpers.CreateNestedExpander(propertiesPanel, "PhysicsPolygons", gfxObj.PhysicsPolygons);
                if (physPolyExpander.Content is Panel physPolyPanel) {
                    RendererHelpers.RenderObjectProperties(physPolyPanel, gfxObj.PhysicsPolygons, context);
                }
                // Physics BSP (using nested rendering)
                var physBspExpander = RendererHelpers.CreateNestedExpander(propertiesPanel, "PhysicsBSP", gfxObj.PhysicsBSP);
                if (physBspExpander.Content is Panel physBspPanel) {
                    RendererHelpers.RenderObjectProperties(physBspPanel, gfxObj.PhysicsBSP, context, 0, 10);
                }
                RendererHelpers.AddSeparator(propertiesPanel);
            }
            else
            {
                RendererHelpers.AddInfoMessageToPanel(propertiesPanel, "No Physics Data (Flag not set)", Colors.Gray);
            }

            // Drawing Data (Conditional)
            if (gfxObj.Flags.HasFlag(GfxObjFlags.HasDrawing))
            {
                RendererHelpers.AddSectionHeader(propertiesPanel, "Drawing Data");
                 // Polygons (using nested rendering)
                var polyExpander = RendererHelpers.CreateNestedExpander(propertiesPanel, "Polygons", gfxObj.Polygons);
                if (polyExpander.Content is Panel polyPanel) {
                    RendererHelpers.RenderObjectProperties(polyPanel, gfxObj.Polygons, context);
                }
                // Drawing BSP (using nested rendering)
                var drawBspExpander = RendererHelpers.CreateNestedExpander(propertiesPanel, "DrawingBSP", gfxObj.DrawingBSP);
                if (drawBspExpander.Content is Panel drawBspPanel) {
                    RendererHelpers.RenderObjectProperties(drawBspPanel, gfxObj.DrawingBSP, context, 0, 10);
                }
            }
            else
            {
                RendererHelpers.AddInfoMessageToPanel(propertiesPanel, "No Drawing Data (Flag not set)", Colors.Gray);
            }

            targetPanel.Children.Add(propertiesPanel);
        }
    }
} 