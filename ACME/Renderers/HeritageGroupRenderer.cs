using ACME.Utils; // FontWeightValues
using DatReaderWriter.Types;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media; // Added for SolidColorBrush
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Windows.UI.Text; // FontWeights

namespace ACME.Renderers
{
    /// <summary>
    /// Renders details for HeritageGroupCG objects.
    /// </summary>
    public class HeritageGroupRenderer : IObjectRenderer
    {
        private const int MaxItemsToShow = 50; // Consider moving to RendererHelpers or config if shared more widely

        public void Render(Panel targetPanel, object data, Dictionary<string, object>? context)
        {
            if (data is not HeritageGroupCG hg)
            {
                RendererHelpers.AddErrorMessageToPanel(targetPanel, "Invalid data type. Expected HeritageGroupCG.");
                return;
            }

            // Clear previous content? Assume DetailRenderer handles clearing.
            // Add title? Assume DetailRenderer handles the main title.

            var mainPanel = new StackPanel() { Margin = new Thickness(12, 0, 0, 20) }; // Top-level container for this renderer's output

            // --- Simple Properties (Reordered) ---
            RendererHelpers.AddSimplePropertyRow(mainPanel, "Icon Id:", hg.IconId.ToString("X8")); // Display hex
            RendererHelpers.AddSimplePropertyRow(mainPanel, "Setup Id:", hg.SetupId.ToString());
            RendererHelpers.AddSimplePropertyRow(mainPanel, "Environment Setup Id:", hg.EnvironmentSetupId.ToString());
            RendererHelpers.AddSimplePropertyRow(mainPanel, "Attribute Credits:", hg.AttributeCredits.ToString());
            RendererHelpers.AddSimplePropertyRow(mainPanel, "Skill Credits:", hg.SkillCredits.ToString());
            RendererHelpers.AddSeparator(mainPanel);

            // --- Start Areas ---
            RendererHelpers.AddSectionHeader(mainPanel, "Primary Start Areas");
            var primaryAreasPanel = new StackPanel { Margin = new Thickness(20, 0, 0, 0) }; // Indent
            RendererHelpers.RenderCollectionWithLookup(primaryAreasPanel, hg.PrimaryStartAreas, "StartAreaLookup", context);
            mainPanel.Children.Add(primaryAreasPanel);
            RendererHelpers.AddSeparator(mainPanel);

            RendererHelpers.AddSectionHeader(mainPanel, "Secondary Start Areas");
            var secondaryAreasPanel = new StackPanel { Margin = new Thickness(20, 0, 0, 0) }; // Indent
            RendererHelpers.RenderCollectionWithLookup(secondaryAreasPanel, hg.SecondaryStartAreas, "StartAreaLookup", context);
            mainPanel.Children.Add(secondaryAreasPanel);
            RendererHelpers.AddSeparator(mainPanel);

            // --- Expanders for Collections/Dictionaries ---

            // Skills Expander (Moved down)
            var skillsExpander = new Expander
            {
                Header = $"Skills ({(hg.Skills?.Count ?? 0)})",
                Margin = new Thickness(0, 5, 0, 5),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            var skillsContentPanel = new StackPanel { Margin = new Thickness(20, 0, 0, 0) };
            RenderHeritageSkills(skillsContentPanel, hg.Skills, context); // Call internal helper
            skillsExpander.Content = skillsContentPanel;
            mainPanel.Children.Add(skillsExpander);

            // Templates Expander (Moved down)
            var templatesExpander = new Expander
            {
                Header = $"Templates ({(hg.Templates?.Count ?? 0)})",
                Margin = new Thickness(0, 5, 0, 5),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            var templatesContentPanel = new StackPanel { Margin = new Thickness(20, 0, 0, 0) };
            RenderHeritageTemplates(templatesContentPanel, hg.Templates, context); // Call internal helper
            templatesExpander.Content = templatesContentPanel;
            mainPanel.Children.Add(templatesExpander);

            // Genders Expander (Moved down)
            var gendersExpander = new Expander
            {
                Header = $"Genders ({(hg.Genders?.Count ?? 0)})",
                Margin = new Thickness(0, 5, 0, 5),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            var gendersContentPanel = new StackPanel { Margin = new Thickness(20, 0, 0, 0) };
            RenderHeritageGenders(gendersContentPanel, hg.Genders, context); // Call internal helper
            gendersExpander.Content = gendersContentPanel;
            mainPanel.Children.Add(gendersExpander);

            targetPanel.Children.Add(mainPanel); // Add the constructed panel to the target
        }


        // --- Private Helper Methods specific to HeritageGroup Rendering ---
        // These were moved from DetailRenderer but are specific to HeritageGroupCG,
        // so they stay within this class instead of RendererHelpers.

        /// <summary>
        /// Renders the Genders dictionary (Key: uint, Value: SexCG) for a HeritageGroup.
        /// </summary>
        private void RenderHeritageGenders(StackPanel parentPanel, IDictionary? gendersDict, Dictionary<string, object>? context)
        {
            if (gendersDict == null || gendersDict.Count == 0)
            {
                RendererHelpers.AddInfoMessageToPanel(parentPanel, "No genders defined.", Colors.Gray);
                return;
            }

            int displayedCount = 0;
            foreach (DictionaryEntry entry in gendersDict)
            {
                if (displayedCount >= MaxItemsToShow)
                {
                    RendererHelpers.AddInfoMessageToPanel(parentPanel, $"...and {gendersDict.Count - MaxItemsToShow} more.", Colors.Gray);
                    break;
                }

                if (entry.Value is SexCG genderObj)
                {
                    // Create an Expander for the entire gender entry
                    var genderExpander = new Expander
                    {
                        Margin = new Thickness(0, 2, 0, 2),
                        HorizontalAlignment = HorizontalAlignment.Stretch
                    };

                    // Header: ID and Name
                    var genderHeaderPanel = new StackPanel { Orientation = Orientation.Horizontal };
                    genderHeaderPanel.Children.Add(new TextBlock { Text = $"ID: {entry.Key}", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 10, 0) });
                    genderHeaderPanel.Children.Add(new TextBlock { Text = genderObj.Name ?? "(No Name)" });
                    genderExpander.Header = genderHeaderPanel;

                    // Content: Panel for all gender details
                    var genderContentPanel = new StackPanel { Margin = new Thickness(30, 5, 0, 5) }; // Indent content

                    // --- Simple Properties of SexCG ---
                    RendererHelpers.AddSimplePropertyRow(genderContentPanel, "Scale:", genderObj.Scale.ToString());
                    RendererHelpers.AddSimplePropertyRow(genderContentPanel, "Setup Id:", genderObj.SetupId.ToString("X8")); // Hex likely useful
                    RendererHelpers.AddSimplePropertyRow(genderContentPanel, "Sound Table:", genderObj.SoundTable.ToString("X8"));
                    RendererHelpers.AddSimplePropertyRow(genderContentPanel, "Icon Id:", genderObj.IconId.ToString("X8"));
                    RendererHelpers.AddSimplePropertyRow(genderContentPanel, "Base Palette:", genderObj.BasePalette.ToString("X8"));
                    RendererHelpers.AddSimplePropertyRow(genderContentPanel, "Skin PalSet:", genderObj.SkinPalSet.ToString("X8"));
                    RendererHelpers.AddSimplePropertyRow(genderContentPanel, "Physics Table:", genderObj.PhysicsTable.ToString("X8"));
                    RendererHelpers.AddSimplePropertyRow(genderContentPanel, "Motion Table:", genderObj.MotionTable.ToString("X8"));
                    RendererHelpers.AddSimplePropertyRow(genderContentPanel, "Combat Table:", genderObj.CombatTable.ToString("X8"));
                    RendererHelpers.AddSeparator(genderContentPanel);

                    // --- BaseObjDesc Expander ---
                    var objDescExpander = RendererHelpers.CreateNestedExpander(genderContentPanel, "Base Object Descriptor", genderObj.BaseObjDesc);
                    if (genderObj.BaseObjDesc != null)
                    {
                        RendererHelpers.RenderObjectProperties(objDescExpander.Content as Panel, genderObj.BaseObjDesc, context);
                    }

                    // --- Expanders for Lists ---
                    RenderUintListExpander(genderContentPanel, "Hair Colors", genderObj.HairColors);
                    RenderComplexListExpander<HairStyleCG>(genderContentPanel, "Hair Styles", genderObj.HairStyles, context);
                    RenderUintListExpander(genderContentPanel, "Eye Colors", genderObj.EyeColors);
                    RenderComplexListExpander<EyeStripCG>(genderContentPanel, "Eye Strips", genderObj.EyeStrips, context);
                    RenderComplexListExpander<FaceStripCG>(genderContentPanel, "Nose Strips", genderObj.NoseStrips, context);
                    RenderComplexListExpander<FaceStripCG>(genderContentPanel, "Mouth Strips", genderObj.MouthStrips, context);
                    RenderComplexListExpander<GearCG>(genderContentPanel, "Headgears", genderObj.Headgears, context);
                    RenderComplexListExpander<GearCG>(genderContentPanel, "Shirts", genderObj.Shirts, context);
                    RenderComplexListExpander<GearCG>(genderContentPanel, "Pants", genderObj.Pants, context);
                    RenderComplexListExpander<GearCG>(genderContentPanel, "Footwear", genderObj.Footwear, context);
                    RenderUintListExpander(genderContentPanel, "Clothing Colors", genderObj.ClothingColors);

                    // Assign the content panel to the gender expander
                    genderExpander.Content = genderContentPanel;

                    // Add the gender expander to the parent panel
                    parentPanel.Children.Add(genderExpander);
                }
                else
                {
                    // Handle cases where the value might not be SexCG
                    // Display the error within an expander for consistency
                     var errorExpander = new Expander
                    {
                        Header = new TextBlock { Text = $"ID: {entry.Key} - Error", Foreground = new SolidColorBrush(Colors.OrangeRed) },
                        Content = new TextBlock { Text = $"Invalid data type. Expected SexCG but got {entry.Value?.GetType().Name ?? "null"}.", Margin = new Thickness(30, 5, 0, 5) },
                        Margin = new Thickness(0, 2, 0, 2),
                        HorizontalAlignment = HorizontalAlignment.Stretch
                    };
                    parentPanel.Children.Add(errorExpander);
                }
                displayedCount++;
            }
             // Add a small bottom margin for spacing within the expander
            parentPanel.Margin = new Thickness(parentPanel.Margin.Left, parentPanel.Margin.Top, parentPanel.Margin.Right, 5);
        }

        // --- Helper method to render List<uint> in an expander ---
        private void RenderUintListExpander(Panel parentPanel, string title, List<uint> list)
        {
            var expander = RendererHelpers.CreateNestedExpander(parentPanel, title, list);
            if (list != null && list.Count > 0)
            {
                var contentPanel = expander.Content as Panel;
                for (int i = 0; i < list.Count; i++)
                {
                     if (i >= MaxItemsToShow) { RendererHelpers.AddInfoMessageToPanel(contentPanel, $"...and {list.Count - MaxItemsToShow} more.", Colors.Gray); break; }
                    // Display as hex, common for IDs/Palettes
                    RendererHelpers.AddSimplePropertyRow(contentPanel, $"[{i}]", list[i].ToString("X8"));
                }
            }
        }

        // --- Helper method to render List<T> where T is complex, in an expander ---
        private void RenderComplexListExpander<T>(Panel parentPanel, string title, List<T> list, Dictionary<string, object>? context) where T : class
        {
            var expander = RendererHelpers.CreateNestedExpander(parentPanel, title, list);
            if (list != null && list.Count > 0)
            {
                var contentPanel = expander.Content as Panel;
                for (int i = 0; i < list.Count; i++)
                {
                     if (i >= MaxItemsToShow) { RendererHelpers.AddInfoMessageToPanel(contentPanel, $"...and {list.Count - MaxItemsToShow} more.", Colors.Gray); break; }

                    var item = list[i];
                    var itemPanel = new StackPanel { Margin = new Thickness(0, 3, 0, 3)};
                    itemPanel.Children.Add(new TextBlock { Text = $"[{i}]", FontWeight = FontWeights.SemiBold });
                    var itemDetailsPanel = new StackPanel { Margin = new Thickness(15, 0, 0, 0)};
                    // Use generic renderer for the item's properties
                    RendererHelpers.RenderObjectProperties(itemDetailsPanel, item, context);
                    itemPanel.Children.Add(itemDetailsPanel);
                    contentPanel.Children.Add(itemPanel);
                }
            }
        }

        /// <summary>
        /// Renders the Skills collection for a HeritageGroup.
        /// Assumes items have Id, NormalCost, PrimaryCost properties (uses dynamic access).
        /// </summary>
        private void RenderHeritageSkills(StackPanel parentPanel, IEnumerable? skillsCollection, Dictionary<string, object>? context)
        {
            if (skillsCollection == null)
            {
                RendererHelpers.AddInfoMessageToPanel(parentPanel, "Skills collection is null.", Colors.Gray);
                return;
            }

            var skillsList = skillsCollection.Cast<object>().ToList(); // Materialize for counting and indexing

            if (skillsList.Count == 0)
            {
                RendererHelpers.AddInfoMessageToPanel(parentPanel, "No skills defined.", Colors.Gray);
                return;
            }

            int displayedCount = 0;
            for (int i = 0; i < skillsList.Count; i++)
            {
                if (displayedCount >= MaxItemsToShow)
                {
                    RendererHelpers.AddInfoMessageToPanel(parentPanel, $"...and {skillsList.Count - MaxItemsToShow} more.", Colors.Gray);
                    break;
                }

                dynamic skillItem = skillsList[i];

                // Create an Expander for each skill
                var skillExpander = new Expander
                {
                    Margin = new Thickness(0, 2, 0, 2), // Reduced vertical margin
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };

                var headerPanel = new StackPanel { Orientation = Orientation.Horizontal }; // Panel for header content
                var detailsPanel = new StackPanel { Margin = new Thickness(30, 0, 0, 0) }; // Indent details further for expander content

                try
                {
                    // Attempt to access properties dynamically
                    object? idValue = null;
                    int normalCost = 0;
                    int primaryCost = 0;
                    string idDisplay = "(Unknown ID)";
                    string normalCostDisplay = "(Unknown)";
                    string primaryCostDisplay = "(Unknown)";

                    try { idValue = skillItem.Id; idDisplay = idValue?.ToString() ?? "null"; } catch { /* ignore */ }
                    try { normalCost = skillItem.NormalCost; normalCostDisplay = normalCost.ToString(); } catch { /* ignore */ }
                    try { primaryCost = skillItem.PrimaryCost; primaryCostDisplay = primaryCost.ToString(); } catch { /* ignore */ }

                    // Try to lookup Skill Name if context provides it
                    if (idValue != null && context?.TryGetValue("SkillLookup", out var sl) == true && sl is Dictionary<uint, string> skillLookup && idValue is Enum)
                    {
                        try
                        {
                            uint skillIdUint = Convert.ToUInt32(idValue); // SkillId is likely an enum based on uint
                            if(skillLookup.TryGetValue(skillIdUint, out var skillName))
                            {
                                idDisplay = $"{idDisplay} ({skillName})";
                            }
                        }
                        catch { /* Conversion or lookup failed, keep original display */ }
                    }

                    // Build Header
                    headerPanel.Children.Add(new TextBlock { Text = $"[{i}]", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 10, 0) }); // Index
                    headerPanel.Children.Add(new TextBlock { Text = $"Id: {idDisplay}" }); // Skill ID and Name

                    // Add properties to detailsPanel using AddSimplePropertyRow for alignment
                    // RendererHelpers.AddSimplePropertyRow(detailsPanel, "Id:", idDisplay); // Moved to header
                    RendererHelpers.AddSimplePropertyRow(detailsPanel, "Normal Cost:", normalCostDisplay);
                    RendererHelpers.AddSimplePropertyRow(detailsPanel, "Primary Cost:", primaryCostDisplay);

                }
                catch (Exception ex)
                {
                    // Catch potential errors during dynamic access
                    // Add error to header if details can't be populated
                    headerPanel.Children.Add(new TextBlock { Text = $"Error: {ex.Message}", Foreground = new SolidColorBrush(Colors.OrangeRed), Margin=new Thickness(10,0,0,0) });
                    // Optionally add to details panel too if needed
                    // RendererHelpers.AddErrorMessageToPanel(detailsPanel, $"Error accessing skill properties: {ex.Message}");
                }

                skillExpander.Header = headerPanel;
                skillExpander.Content = detailsPanel;

                parentPanel.Children.Add(skillExpander); // Add the expander
                displayedCount++;
            }
            // Add a small bottom margin for spacing within the expander
            parentPanel.Margin = new Thickness(parentPanel.Margin.Left, parentPanel.Margin.Top, parentPanel.Margin.Right, 5);
        }

        /// <summary>
        /// Renders the Templates collection (IEnumerable<TemplateCG>) for a HeritageGroup.
        /// </summary>
        private void RenderHeritageTemplates(StackPanel parentPanel, IEnumerable? templatesCollection, Dictionary<string, object>? context)
        {
            if (templatesCollection == null)
            {
                RendererHelpers.AddInfoMessageToPanel(parentPanel, "Templates collection is null.", Colors.Gray);
                return;
            }

            var templatesList = templatesCollection.Cast<object>().ToList(); // Materialize for counting and indexing

            if (templatesList.Count == 0)
            {
                RendererHelpers.AddInfoMessageToPanel(parentPanel, "No templates defined.", Colors.Gray);
                return;
            }

            int displayedCount = 0;
            for (int i = 0; i < templatesList.Count; i++)
            {
                if (displayedCount >= MaxItemsToShow)
                {
                    RendererHelpers.AddInfoMessageToPanel(parentPanel, $"...and {templatesList.Count - MaxItemsToShow} more.", Colors.Gray);
                    break;
                }

                if (templatesList[i] is TemplateCG template)
                {
                    // Create an Expander for each template
                    var templateExpander = new Expander
                    {
                        Margin = new Thickness(0, 2, 0, 2), // Reduced vertical margin
                        HorizontalAlignment = HorizontalAlignment.Stretch
                    };

                    var headerPanel = new StackPanel { Orientation = Orientation.Horizontal }; // Panel for header content
                    var detailsPanel = new StackPanel { Margin = new Thickness(30, 0, 0, 0) }; // Indent details further for expander content

                    // Build Header
                    headerPanel.Children.Add(new TextBlock { Text = $"[{i}]", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 10, 0) }); // Index
                    headerPanel.Children.Add(new TextBlock { Text = template.Name ?? "(No Name)" }); // Template Name

                    // Add properties to detailsPanel using AddSimplePropertyRow
                    RendererHelpers.AddSimplePropertyRow(detailsPanel, "Icon Id:", template.IconId.ToString("X8")); // Display hex
                    RendererHelpers.AddSimplePropertyRow(detailsPanel, "Strength:", template.Strength.ToString());
                    RendererHelpers.AddSimplePropertyRow(detailsPanel, "Endurance:", template.Endurance.ToString());
                    RendererHelpers.AddSimplePropertyRow(detailsPanel, "Coordination:", template.Coordination.ToString());
                    RendererHelpers.AddSimplePropertyRow(detailsPanel, "Quickness:", template.Quickness.ToString());
                    RendererHelpers.AddSimplePropertyRow(detailsPanel, "Focus:", template.Focus.ToString());
                    RendererHelpers.AddSimplePropertyRow(detailsPanel, "Self:", template.Self.ToString());

                    templateExpander.Header = headerPanel;
                    templateExpander.Content = detailsPanel;

                    parentPanel.Children.Add(templateExpander); // Add the expander
                }
                else
                {
                     // Handle unexpected item type if necessary
                    // Consider adding this error within an expander as well, or directly to parentPanel
                     RendererHelpers.AddErrorMessageToPanel(parentPanel, $"Item at index {i} is not a TemplateCG (Type: {templatesList[i]?.GetType().Name ?? "null"}).");
                }

                displayedCount++;
            }
            // Add a small bottom margin for spacing within the expander
            parentPanel.Margin = new Thickness(parentPanel.Margin.Left, parentPanel.Margin.Top, parentPanel.Margin.Right, 5);
        }
    }
} 