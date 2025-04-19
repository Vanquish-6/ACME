using Microsoft.UI.Xaml.Controls;
using DatReaderWriter.DBObjs;
using DatReaderWriter.Types;
using System.Collections.Generic;
using ACME.Renderers; // Correct namespace
using Microsoft.UI.Text; // For FontWeights
using Microsoft.UI.Xaml; // For Thickness

namespace ACME.Renderers
{
    /// <summary>
    /// Renders CombatTable objects.
    /// </summary>
    public class CombatTableRenderer : IObjectRenderer
    {
        /// <inheritdoc />
        public void Render(Panel targetPanel, object data, Dictionary<string, object>? context)
        {
            if (data is not CombatTable combatTable)
            {
                // Use existing helper for error messages
                RendererHelpers.AddErrorMessageToPanel(targetPanel, "Error: Invalid data type for CombatTableRenderer.");
                return;
            }

            targetPanel.Children.Clear(); // Clear previous content

            // Add a header for the combat table itself using a TextBlock
            targetPanel.Children.Add(new TextBlock {
                Text = $"Combat Table Details (ID: {combatTable.Id:X8})", 
                Style = (Style)Application.Current.Resources["TitleTextBlockStyle"], // Assuming a Title style exists
                Margin = new Thickness(0, 0, 0, 12)
            });

            // Add a header for the maneuvers list using AddSectionHeader
            RendererHelpers.AddSectionHeader(targetPanel, $"Combat Maneuvers ({combatTable.CombatManeuvers.Count})");

            if (combatTable.CombatManeuvers.Count == 0)
            {
                // Use a simple TextBlock for info messages if AddInfoMessageToPanel isn't suitable here
                targetPanel.Children.Add(new TextBlock { 
                    Text = "No combat maneuvers found.", 
                    FontStyle = Windows.UI.Text.FontStyle.Italic, 
                    Margin = new Thickness(0, 6, 0, 6) 
                });
                return;
            }

            // Render each maneuver within an Expander
            int index = 0;
            foreach (var maneuver in combatTable.CombatManeuvers)
            {
                var expander = new Expander
                {
                    // Header: Simple text showing index and style
                    Header = new TextBlock 
                    { 
                        Text = $"Maneuver {index + 1}: {maneuver.Style}",
                        FontWeight = FontWeights.SemiBold
                    },
                    IsExpanded = false, // Start collapsed
                    Margin = new Thickness(0, 5, 0, 5)
                };

                // Content: Panel with detailed properties
                var maneuverPanel = new StackPanel { Spacing = 5, Margin = new Thickness(20, 10, 0, 10) }; // Indent content
                
                // Use AddSimplePropertyRow for maneuver properties inside the panel
                RendererHelpers.AddSimplePropertyRow(maneuverPanel, "Style:", maneuver.Style.ToString());
                RendererHelpers.AddSimplePropertyRow(maneuverPanel, "Attack Height:", maneuver.AttackHeight.ToString());
                RendererHelpers.AddSimplePropertyRow(maneuverPanel, "Attack Type:", maneuver.AttackType.ToString());
                RendererHelpers.AddSimplePropertyRow(maneuverPanel, "Min Skill Level:", maneuver.MinSkillLevel.ToString());
                RendererHelpers.AddSimplePropertyRow(maneuverPanel, "Motion:", maneuver.Motion.ToString());

                expander.Content = maneuverPanel;
                targetPanel.Children.Add(expander);
                index++;
            }
        }
    }
} 