using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI;
using DatReaderWriter.DBObjs;
using DatReaderWriter.Types;
using DatReaderWriter; // For DatDatabase

namespace ACME.Renderers
{
    public class PaletteSetRenderer : IObjectRenderer
    {
        public void Render(Panel targetPanel, object data, Dictionary<string, object>? context)
        {
            if (data is not PaletteSet paletteSet)
            {
                RendererHelpers.AddErrorMessageToPanel(targetPanel, "Invalid object type passed to PaletteSetRenderer.");
                return;
            }

            targetPanel.Children.Clear();

            // --- Basic Info ---
            RendererHelpers.AddSimplePropertyRow(targetPanel, "Id", $"0x{paletteSet.Id:X8}");
            // You could add other PaletteSet specific properties here if needed.
            RendererHelpers.AddSeparator(targetPanel);

            // --- Combine Palettes & Show Preview ---
            // --- Display Individual Palette Previews ---
            DatDatabase? db = null;
            if (context?.TryGetValue("Database", out var dbObject) == true && dbObject is DatDatabase database)
            {
                db = database;
            }
            else
            {
                RendererHelpers.AddErrorMessageToPanel(targetPanel, "Database context not found. Cannot load constituent palettes.");
                return;
            }

            // List<ColorARGB> combinedColors = new List<ColorARGB>(); // REMOVED: Don't combine
            List<uint> failedPaletteLoads = new List<uint>();
            bool previewsShown = false;

            if (paletteSet.Palettes != null)
            {
                RendererHelpers.AddSectionHeader(targetPanel, "Individual Palette Previews"); // New Section Header

                foreach (uint paletteId in paletteSet.Palettes)
                {
                    if (db.TryReadFile<Palette>(paletteId, out var loadedPalette) && loadedPalette?.Colors != null)
                    {
                        // Add a label for this specific palette preview
                        RendererHelpers.AddSimplePropertyRow(targetPanel, "Palette:", $"0x{paletteId:X8}");
                        // combinedColors.AddRange(loadedPalette.Colors); // REMOVED
                        RendererHelpers.AddPalettePreview(targetPanel, loadedPalette.Colors); // Add preview for *this* palette
                        RendererHelpers.AddSeparator(targetPanel); // Add separator after each preview
                        previewsShown = true;
                    }
                    else
                    {
                        failedPaletteLoads.Add(paletteId);
                        // Optionally add a message here if a specific palette fails to load
                        RendererHelpers.AddErrorMessageToPanel(targetPanel, $"Failed to load Palette 0x{paletteId:X8} for preview.");
                    }
                }
            }

            // RendererHelpers.AddSectionHeader(targetPanel, "Combined Palette Preview"); // REMOVED
            // if (combinedColors.Count > 0) // REMOVED
            // {
            //     RendererHelpers.AddPalettePreview(targetPanel, combinedColors); // REMOVED
            // }
            // else
            if (!previewsShown && (paletteSet.Palettes == null || paletteSet.Palettes.Count == 0))
            {
                // Display message only if no previews were shown at all
                RendererHelpers.AddInfoMessageToPanel(targetPanel, "No constituent palettes defined or loaded.", Colors.Gray);
            }

            // --- Show Constituent Palettes ---
            var expander = RendererHelpers.CreateNestedExpander(targetPanel, $"Constituent Palette IDs ({paletteSet.Palettes?.Count ?? 0})", paletteSet.Palettes);
            if (paletteSet.Palettes != null && paletteSet.Palettes.Count > 0)
            {
                var contentPanel = expander.Content as Panel;
                if (contentPanel != null)
                {
                    for (int i = 0; i < paletteSet.Palettes.Count; i++)
                    {
                        uint pId = paletteSet.Palettes[i];
                        string status = failedPaletteLoads.Contains(pId) ? " (Load Failed)" : "";
                        // Just list the IDs for now
                        RendererHelpers.AddSimplePropertyRow(contentPanel, $"[{i}]", $"0x{pId:X8}{status}");
                    }
                }
            }
            // Expander is added by CreateNestedExpander
        }
    }
} 