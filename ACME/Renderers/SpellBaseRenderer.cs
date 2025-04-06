using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Microsoft.UI;
using Windows.UI.Text;
using Windows.UI; // For Color class
using ACME.Managers;
using DatReaderWriter.Types;
using DatReaderWriter.Enums;
using System.Threading.Tasks;
using System.Diagnostics;

namespace ACME.Renderers
{
    /// <summary>
    /// Custom renderer for SpellBase objects with editing functionality
    /// </summary>
    public class SpellBaseRenderer : IObjectRenderer
    {
        private readonly GenericObjectRenderer _genericRenderer = new GenericObjectRenderer();
        
        public void Render(Panel targetPanel, object data, Dictionary<string, object>? context)
        {
            if (!(data is SpellBase spellBase))
            {
                // Fallback to generic renderer if not a SpellBase
                _genericRenderer.Render(targetPanel, data, context);
                return;
            }
            
            // Create a container for our custom controls
            var controlsPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
            
            // Create Edit button - now we always show "Edit Spell" as all databases have write access
            var editButton = new Button
            {
                Content = "Edit Spell",
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 5, 0, 10)
            };
            
            // Add button click handler
            editButton.Click += async (sender, args) =>
            {
                // Show edit dialog with the spell data
                await ShowSpellEditDialog(spellBase, targetPanel.XamlRoot, context);
            };
            
            controlsPanel.Children.Add(editButton);
            
            // Add a status indicator
            var statusText = new TextBlock
            {
                Text = "You can edit this spell by clicking the Edit button above.",
                FontStyle = FontStyle.Italic,
                Foreground = new SolidColorBrush(Colors.Green),
                Margin = new Thickness(0, 0, 0, 10)
            };
            
            controlsPanel.Children.Add(statusText);
            
            // Add separator line
            var separator = new Rectangle
            {
                Height = 1,
                Fill = new SolidColorBrush(Colors.LightGray),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 5, 0, 10)
            };
            
            controlsPanel.Children.Add(separator);
            
            // Add the controls panel to the target panel
            targetPanel.Children.Add(controlsPanel);
            
            // Use the generic renderer to display the rest of the properties
            _genericRenderer.Render(targetPanel, data, context);
        }
        
        /// <summary>
        /// Shows a dialog for editing spell properties
        /// </summary>
        private async Task ShowSpellEditDialog(SpellBase spell, XamlRoot xamlRoot, Dictionary<string, object>? context = null)
        {
            // Store the original spell's ID
            uint originalSpellId = 0;
            
            // Find the spell ID before making any changes
            if (context != null && context.TryGetValue("DatabaseManager", out var dbMgrObj) && 
                dbMgrObj is Managers.DatabaseManager dbMgr && dbMgr.CurrentDatabase != null)
            {
                var db = dbMgr.CurrentDatabase;
                if (db.TryReadFile<DatReaderWriter.DBObjs.SpellTable>(ACME.Constants.DatFileIds.SpellTableId, out var spellTable))
                {
                    // First try direct reference equality
                    foreach (var entry in spellTable.Spells)
                    {
                        // Check if the spell instance is the same memory reference
                        // This is crucial because the spell object from the list view is the actual dictionary value
                        if (object.ReferenceEquals(entry.Value, spell))
                        {
                            originalSpellId = entry.Key;
                            Debug.WriteLine($"Found spell with ID: {originalSpellId} using reference equality");
                            break;
                        }
                    }
                    
                    // If we didn't find it by reference, try to get it from context
                    if (originalSpellId == 0 && context.TryGetValue("SelectedItemId", out var selectedItemIdObj) && 
                        selectedItemIdObj is uint spellId && spellTable.Spells.ContainsKey(spellId))
                    {
                        originalSpellId = spellId;
                        Debug.WriteLine($"Found spell with ID: {originalSpellId} from SelectedItemId context");
                    }
                    
                    // Last resort - try to find by name and ID
                    if (originalSpellId == 0 && !string.IsNullOrEmpty(spell.Name))
                    {
                        // Try to find by name and key properties
                        foreach (var entry in spellTable.Spells)
                        {
                            if (entry.Value.Name == spell.Name && 
                                entry.Value.Icon == spell.Icon && 
                                entry.Value.School == spell.School)
                            {
                                originalSpellId = entry.Key;
                                Debug.WriteLine($"Found spell with ID: {originalSpellId} using property matching");
                                break;
                            }
                        }
                    }
                }
            }

            // Create a scrollable container for all properties
            var scrollViewer = new ScrollViewer
            {
                HorizontalScrollMode = ScrollMode.Disabled,
                VerticalScrollMode = ScrollMode.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(10),
                MaxHeight = 900 // Further increased for more visible content
            };
            
            var mainPanel = new StackPanel { Spacing = 20, Padding = new Thickness(10) };
            scrollViewer.Content = mainPanel;
            
            // ===== SPELL IDENTIFICATION SECTION =====
            var idHeader = new TextBlock
            {
                Text = "SPELL IDENTIFICATION",
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                FontSize = 16,
                Margin = new Thickness(0, 0, 0, 10)
            };
            mainPanel.Children.Add(idHeader);
            
            // Name field
            var nameBox = new TextBox { 
                Header = "Spell Name",
                Text = spell.Name,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 0, 0, 10)
            };
            mainPanel.Children.Add(nameBox);
            
            // Description field
            var descBox = new TextBox { 
                Header = "Description",
                Text = spell.Description,
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true,
                MinHeight = 80,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            mainPanel.Children.Add(descBox);
            
            // Add separator
            mainPanel.Children.Add(new Rectangle { 
                Height = 1, 
                Fill = new SolidColorBrush(Microsoft.UI.Colors.LightGray),
                HorizontalAlignment = HorizontalAlignment.Stretch 
            });
            
            // ===== CLASSIFICATION SECTION =====
            var classHeader = new TextBlock
            {
                Text = "CLASSIFICATION",
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                FontSize = 16,
                Margin = new Thickness(0, 0, 0, 10)
            };
            mainPanel.Children.Add(classHeader);
            
            // School selection - using simpler approach
            var schoolCombo = new ComboBox { 
                Header = "Magic School",
                Width = 350,
                Margin = new Thickness(0, 0, 0, 10)
            };
            
            // Add all enum values and find the current one
            int schoolIndex = 0;
            int currentSchoolIndex = 0;
            
            foreach (MagicSchool schoolValue in Enum.GetValues(typeof(MagicSchool)))
            {
                schoolCombo.Items.Add(schoolValue.ToString());
                if (schoolValue == spell.School)
                {
                    currentSchoolIndex = schoolIndex;
                }
                schoolIndex++;
            }
            
            // Set the selected index
            if (schoolCombo.Items.Count > 0)
            {
                schoolCombo.SelectedIndex = currentSchoolIndex;
            }
            mainPanel.Children.Add(schoolCombo);
            
            // Category selection - using simpler approach
            var categoryCombo = new ComboBox { 
                Header = "Spell Category",
                Width = 350,
                Margin = new Thickness(0, 0, 0, 10)
            };
            
            // Add all enum values and find the current one
            int categoryIndex = 0;
            int currentCategoryIndex = 0;
            
            foreach (SpellCategory categoryValue in Enum.GetValues(typeof(SpellCategory)))
            {
                categoryCombo.Items.Add(categoryValue.ToString());
                if (categoryValue == spell.Category)
                {
                    currentCategoryIndex = categoryIndex;
                }
                categoryIndex++;
            }
            
            // Set the selected index
            if (categoryCombo.Items.Count > 0)
            {
                categoryCombo.SelectedIndex = currentCategoryIndex;
            }
            mainPanel.Children.Add(categoryCombo);
            
            // Meta spell type - using simpler approach
            var metaTypeCombo = new ComboBox { 
                Header = "Meta Spell Type",
                Width = 350,
                Margin = new Thickness(0, 0, 0, 10)
            };
            
            // Add all enum values and find the current one
            int typeIndex = 0;
            int currentTypeIndex = 0;
            
            foreach (SpellType typeValue in Enum.GetValues(typeof(SpellType)))
            {
                metaTypeCombo.Items.Add(typeValue.ToString());
                if (typeValue == spell.MetaSpellType)
                {
                    currentTypeIndex = typeIndex;
                }
                typeIndex++;
            }
            
            // Set the selected index
            if (metaTypeCombo.Items.Count > 0)
            {
                metaTypeCombo.SelectedIndex = currentTypeIndex;
            }
            mainPanel.Children.Add(metaTypeCombo);
            
            var metaIdBox = new NumberBox { 
                Header = "Meta Spell ID",
                Value = spell.MetaSpellId,
                SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
                Width = 350,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, 0, 10)
            };
            mainPanel.Children.Add(metaIdBox);
            
            // Add separator
            mainPanel.Children.Add(new Rectangle { 
                Height = 1, 
                Fill = new SolidColorBrush(Microsoft.UI.Colors.LightGray),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 10, 0, 0)
            });
            
            // ===== VISUAL PROPERTIES SECTION =====
            var visualHeader = new TextBlock
            {
                Text = "VISUAL PROPERTIES",
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                FontSize = 16,
                Margin = new Thickness(0, 0, 0, 10)
            };
            mainPanel.Children.Add(visualHeader);
            
            // Icon and Display Order
            var visualRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 20 };
            mainPanel.Children.Add(visualRow);
            
            var iconBox = new NumberBox { 
                Header = "Icon ID",
                Value = spell.Icon,
                SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
                Width = 150
            };
            visualRow.Children.Add(iconBox);
            
            var displayOrderBox = new NumberBox { 
                Header = "Display Order",
                Value = spell.DisplayOrder,
                SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
                Width = 150
            };
            visualRow.Children.Add(displayOrderBox);
            
            // Visual effects row - split into two rows to prevent cutoff
            var vfxRow1 = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 30, Margin = new Thickness(0, 10, 0, 0) };
            mainPanel.Children.Add(vfxRow1);
            
            var casterEffectBox = new NumberBox { 
                Header = "Caster Effect",
                Value = spell.CasterEffect,
                SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
                Width = 200
            };
            vfxRow1.Children.Add(casterEffectBox);
            
            var targetEffectBox = new NumberBox { 
                Header = "Target Effect",
                Value = spell.TargetEffect,
                SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
                Width = 200
            };
            vfxRow1.Children.Add(targetEffectBox);
            
            // Second visual effects row
            var vfxRow2 = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 30, Margin = new Thickness(0, 10, 0, 0) };
            mainPanel.Children.Add(vfxRow2);
            
            var fizzleEffectBox = new NumberBox { 
                Header = "Fizzle Effect",
                Value = spell.FizzleEffect,
                SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
                Width = 200
            };
            vfxRow2.Children.Add(fizzleEffectBox);
            
            // Add separator
            mainPanel.Children.Add(new Rectangle { 
                Height = 1, 
                Fill = new SolidColorBrush(Microsoft.UI.Colors.LightGray),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 10, 0, 0)
            });
            
            // ===== SPELL METRICS SECTION =====
            var metricsHeader = new TextBlock
            {
                Text = "SPELL METRICS",
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                FontSize = 16,
                Margin = new Thickness(0, 0, 0, 10)
            };
            mainPanel.Children.Add(metricsHeader);
            
            // Energy metrics row - split into two rows for better visibility
            var energyRow1 = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 30 }; 
            mainPanel.Children.Add(energyRow1);
            
            var manaBox = new NumberBox { 
                Header = "Base Mana",
                Value = spell.BaseMana,
                SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
                Width = 200
            };
            energyRow1.Children.Add(manaBox);
            
            var manaMod = new NumberBox { 
                Header = "Mana Modifier",
                Value = spell.ManaMod,
                SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
                Width = 200
            };
            energyRow1.Children.Add(manaMod);
            
            // Second energy row
            var energyRow2 = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 30, Margin = new Thickness(0, 10, 0, 0) };
            mainPanel.Children.Add(energyRow2);
            
            var powerBox = new NumberBox { 
                Header = "Power",
                Value = spell.Power,
                SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
                Width = 200
            };
            energyRow2.Children.Add(powerBox);
            
            var economyModBox = new NumberBox { 
                Header = "Economy Modifier",
                Value = spell.SpellEconomyMod,
                SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
                Width = 200
            };
            energyRow2.Children.Add(economyModBox);
            
            // Range metrics row - split into two rows to prevent cutoff
            var rangeRow1 = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 30, Margin = new Thickness(0, 10, 0, 0) };
            mainPanel.Children.Add(rangeRow1);
            
            var rangeConstBox = new NumberBox { 
                Header = "Base Range Constant",
                Value = spell.BaseRangeConstant,
                SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
                Width = 200
            };
            rangeRow1.Children.Add(rangeConstBox);
            
            var rangeModBox = new NumberBox { 
                Header = "Base Range Modifier",
                Value = spell.BaseRangeMod,
                SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
                Width = 200
            };
            rangeRow1.Children.Add(rangeModBox);
            
            // Second range row
            var rangeRow2 = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 30, Margin = new Thickness(0, 10, 0, 0) };
            mainPanel.Children.Add(rangeRow2);
            
            var nonComponentTargetBox = new NumberBox { 
                Header = "Non-Component Target Type",
                Value = spell.NonComponentTargetType,
                SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
                Width = 200
            };
            rangeRow2.Children.Add(nonComponentTargetBox);
            
            // Additional metrics - split into multiple rows for clarity
            var extraRow1 = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 30, Margin = new Thickness(0, 10, 0, 0) };
            mainPanel.Children.Add(extraRow1);
            
            var bitfieldBox = new NumberBox { 
                Header = "Bitfield",
                Value = spell.Bitfield,
                SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
                Width = 200
            };
            extraRow1.Children.Add(bitfieldBox);
            
            var formulaVersionBox = new NumberBox { 
                Header = "Formula Version",
                Value = spell.FormulaVersion,
                SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
                Width = 200
            };
            extraRow1.Children.Add(formulaVersionBox);
            
            // Second extra row
            var extraRow2 = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 30, Margin = new Thickness(0, 10, 0, 0) };
            mainPanel.Children.Add(extraRow2);
            
            var componentLossBox = new NumberBox { 
                Header = "Component Loss",
                Value = spell.ComponentLoss,
                SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
                Maximum = 1,
                SmallChange = 0.01,
                LargeChange = 0.1,
                Width = 200
            };
            extraRow2.Children.Add(componentLossBox);
            
            // Add separator
            mainPanel.Children.Add(new Rectangle { 
                Height = 1, 
                Fill = new SolidColorBrush(Microsoft.UI.Colors.LightGray),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 10, 0, 0)
            });
            
            // ===== DURATION & RECOVERY SECTION =====
            var durationHeader = new TextBlock
            {
                Text = "DURATION & RECOVERY",
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                FontSize = 16,
                Margin = new Thickness(0, 0, 0, 10)
            };
            mainPanel.Children.Add(durationHeader);
            
            // Duration row - split into two rows to prevent cutoff
            var durationRow1 = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 30 };
            mainPanel.Children.Add(durationRow1);
            
            var durationBox = new NumberBox { 
                Header = "Duration (seconds)",
                Value = spell.Duration,
                SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
                Width = 200
            };
            durationRow1.Children.Add(durationBox);
            
            var degradeModBox = new NumberBox { 
                Header = "Degrade Modifier",
                Value = spell.DegradeModifier,
                SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
                Width = 200
            };
            durationRow1.Children.Add(degradeModBox);
            
            // Second duration row
            var durationRow2 = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 30, Margin = new Thickness(0, 10, 0, 0) };
            mainPanel.Children.Add(durationRow2);
            
            var degradeLimitBox = new NumberBox { 
                Header = "Degrade Limit",
                Value = spell.DegradeLimit,
                SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
                Width = 200
            };
            durationRow2.Children.Add(degradeLimitBox);
            
            // Recovery row - split into two rows to prevent cutoff
            var recoveryRow1 = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 30, Margin = new Thickness(0, 10, 0, 0) };
            mainPanel.Children.Add(recoveryRow1);
            
            var recoveryIntervalBox = new NumberBox { 
                Header = "Recovery Interval",
                Value = spell.RecoveryInterval,
                SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
                Width = 200
            };
            recoveryRow1.Children.Add(recoveryIntervalBox);
            
            var recoveryAmountBox = new NumberBox { 
                Header = "Recovery Amount",
                Value = spell.RecoveryAmount,
                SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
                Width = 200
            };
            recoveryRow1.Children.Add(recoveryAmountBox);
            
            // Second recovery row
            var recoveryRow2 = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 30, Margin = new Thickness(0, 10, 0, 0) };
            mainPanel.Children.Add(recoveryRow2);
            
            var portalLifetimeBox = new NumberBox { 
                Header = "Portal Lifetime (seconds)",
                Value = spell.PortalLifetime,
                SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
                Width = 200
            };
            recoveryRow2.Children.Add(portalLifetimeBox);
            
            // Add separator
            mainPanel.Children.Add(new Rectangle { 
                Height = 1, 
                Fill = new SolidColorBrush(Microsoft.UI.Colors.LightGray),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 10, 0, 0)
            });
            
            // ===== COMPONENTS SECTION =====
            var componentsHeader = new TextBlock
            {
                Text = "SPELL COMPONENTS",
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                FontSize = 16,
                Margin = new Thickness(0, 0, 0, 5)
            };
            mainPanel.Children.Add(componentsHeader);
            
            // Components description
            var componentsLimitText = new TextBlock
            {
                Text = "Select up to 8 components for this spell. These determine the spell's effects and properties.",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10) // Increased margin
            };
            mainPanel.Children.Add(componentsLimitText);
            
            // Current selection summary
            var selectionGrid = new Grid { Margin = new Thickness(0, 0, 0, 15) }; // Increased margin
            selectionGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            selectionGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            mainPanel.Children.Add(selectionGrid);
            
            // Selected components display
            var selectionPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };
            Grid.SetColumn(selectionPanel, 0);
            selectionGrid.Children.Add(selectionPanel);
            
            var selectionLabel = new TextBlock
            {
                Text = "Selected:",
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            };
            selectionPanel.Children.Add(selectionLabel);
            
            var selectedComponentsText = new TextBlock
            {
                Text = spell.Components.Count > 0 
                    ? string.Join(", ", spell.Components) 
                    : "None",
                VerticalAlignment = VerticalAlignment.Center
            };
            selectionPanel.Children.Add(selectedComponentsText);
            
            // Count display
            var countText = new TextBlock
            {
                Text = $"{spell.Components.Count}/8 selected",
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            Grid.SetColumn(countText, 1);
            selectionGrid.Children.Add(countText);
            
            // Search box
            var searchBox = new TextBox
            {
                PlaceholderText = "Search components by name or ID...",
                Margin = new Thickness(0, 0, 0, 15) // Increased margin
            };
            mainPanel.Children.Add(searchBox);
            
            // Components selection area
            var componentSelectionPanel = new Border
            {
                BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.LightGray),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(15), // Increased padding
                CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(10, 0, 0, 0))
            };
            mainPanel.Children.Add(componentSelectionPanel);
            
            // Scrollable components area
            var componentScroller = new ScrollViewer
            {
                HorizontalScrollMode = ScrollMode.Disabled,
                VerticalScrollMode = ScrollMode.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                MaxHeight = 350 // Increased height for components area
            };
            componentSelectionPanel.Child = componentScroller;
            
            // Grid for multi-column layout
            var componentGrid = new Grid();
            // Define 4 columns instead of 3 for better distribution of components
            for (int i = 0; i < 4; i++)
            {
                componentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }
            componentScroller.Content = componentGrid;
            
            // Create containers for each column
            var column1 = new StackPanel { Margin = new Thickness(5) };
            var column2 = new StackPanel { Margin = new Thickness(5) };
            var column3 = new StackPanel { Margin = new Thickness(5) };
            var column4 = new StackPanel { Margin = new Thickness(5) };
            
            Grid.SetColumn(column1, 0);
            Grid.SetColumn(column2, 1);
            Grid.SetColumn(column3, 2);
            Grid.SetColumn(column4, 3);
            
            componentGrid.Children.Add(column1);
            componentGrid.Children.Add(column2);
            componentGrid.Children.Add(column3);
            componentGrid.Children.Add(column4);
            
            // Get component names from context
            Dictionary<uint, string> componentLookup = new Dictionary<uint, string>();
            if (context != null && context.TryGetValue("ComponentLookup", out var lookupObj) && 
                lookupObj is Dictionary<uint, string> lookupDict)
            {
                componentLookup = lookupDict;
            }
            
            // Create a list to track selected components
            List<CheckBox> componentCheckboxes = new List<CheckBox>();
            HashSet<uint> selectedComponents = new HashSet<uint>(spell.Components);
            
            // Show all components across three columns
            var componentsToShow = componentLookup.OrderBy(c => c.Value).ToList(); // Sort by name
            var totalComponents = componentsToShow.Count;
            
            // Initial component distribution
            for (int i = 0; i < totalComponents; i++)
            {
                var component = componentsToShow[i];
                var checkbox = new CheckBox
                {
                    Content = $"{component.Key}: {component.Value}",
                    IsChecked = selectedComponents.Contains(component.Key),
                    Tag = component.Key,
                    Margin = new Thickness(0, 0, 0, 5)
                };
                
                // Limit selections to 8 components
                checkbox.Checked += (s, e) => 
                {
                    var checkedCount = componentCheckboxes.Count(cb => cb.IsChecked == true);
                    if (checkedCount > 8)
                    {
                        checkbox.IsChecked = false;
                    }
                    
                    // Update selection summary
                    UpdateSelectionSummary();
                };
                
                checkbox.Unchecked += (s, e) => UpdateSelectionSummary();
                
                // Add to the appropriate column - distribute evenly across 4 columns
                if (i % 4 == 0)
                    column1.Children.Add(checkbox);
                else if (i % 4 == 1)
                    column2.Children.Add(checkbox);
                else if (i % 4 == 2)
                    column3.Children.Add(checkbox);
                else
                    column4.Children.Add(checkbox);
                
                componentCheckboxes.Add(checkbox);
            }
            
            // Show a message if no component lookup is available
            if (componentLookup.Count == 0)
            {
                componentSelectionPanel.Child = new TextBlock
                {
                    Text = "Component names not available. Current component IDs: " + 
                           (spell.Components.Count > 0 ? string.Join(", ", spell.Components) : "None"),
                    TextWrapping = TextWrapping.Wrap,
                    Padding = new Thickness(10)
                };
            }
            
            // Helper method to update the selection summary
            void UpdateSelectionSummary()
            {
                var selectedIds = componentCheckboxes
                    .Where(cb => cb.IsChecked == true)
                    .Select(cb => (uint)cb.Tag)
                    .ToList();
                
                // Update the text displays
                selectedComponentsText.Text = selectedIds.Count > 0 
                    ? string.Join(", ", selectedIds) 
                    : "None";
                
                countText.Text = $"{selectedIds.Count}/8 selected";
                
                // Update text color based on count
                if (selectedIds.Count > 8)
                {
                    countText.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Red);
                }
                else if (selectedIds.Count == 8)
                {
                    countText.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Orange);
                }
                else
                {
                    countText.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Green);
                }
            }
            
            // Add search functionality for components
            searchBox.TextChanged += (s, e) =>
            {
                var searchTerm = searchBox.Text.ToLowerInvariant();
                
                // Clear all columns
                column1.Children.Clear();
                column2.Children.Clear();
                column3.Children.Clear();
                column4.Children.Clear();
                
                // Filter components
                var filteredComponents = componentsToShow;
                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    filteredComponents = componentsToShow
                        .Where(c => 
                            c.Value.ToLowerInvariant().Contains(searchTerm) || 
                            c.Key.ToString().Contains(searchTerm))
                        .ToList();
                }
                
                // Redistribute filtered components across 4 columns
                for (int i = 0; i < filteredComponents.Count; i++)
                {
                    var component = filteredComponents[i];
                    var existingCheckbox = componentCheckboxes.FirstOrDefault(cb => (uint)cb.Tag == component.Key);
                    
                    if (existingCheckbox != null)
                    {
                        // Remove from current parent before adding to new column
                        if (existingCheckbox.Parent is Panel parent)
                        {
                            parent.Children.Remove(existingCheckbox);
                        }
                        
                        // Add to the appropriate column
                        if (i % 4 == 0)
                            column1.Children.Add(existingCheckbox);
                        else if (i % 4 == 1)
                            column2.Children.Add(existingCheckbox);
                        else if (i % 4 == 2)
                            column3.Children.Add(existingCheckbox);
                        else
                            column4.Children.Add(existingCheckbox);
                    }
                }
            };
            
            // Create dialog
            var dialog = new ContentDialog
            {
                Title = "Edit Spell",
                Content = scrollViewer,
                PrimaryButtonText = "Save",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = xamlRoot
            };
            
            // Match the dialog size to content
            dialog.MinWidth = 1500;
            dialog.MinHeight = 900;
            
            // Show dialog and handle result
            var result = await dialog.ShowAsync();
            
            if (result == ContentDialogResult.Primary)
            {
                // Debug log before updating
                Debug.WriteLine("=== SPELL BEFORE UPDATE ===");
                Debug.WriteLine($"Name: {spell.Name}");
                Debug.WriteLine($"Description: {spell.Description}");
                Debug.WriteLine($"School: {spell.School} ({(int)spell.School})");
                Debug.WriteLine($"Category: {spell.Category} ({(int)spell.Category})");
                Debug.WriteLine($"MetaSpellType: {spell.MetaSpellType} ({(int)spell.MetaSpellType})");
                Debug.WriteLine($"MetaSpellId: {spell.MetaSpellId}");
                Debug.WriteLine($"Bitfield: {spell.Bitfield}");
                Debug.WriteLine($"Components: {string.Join(", ", spell.Components)}");
                Debug.WriteLine($"BaseMana: {spell.BaseMana}");
                Debug.WriteLine($"BaseRangeConstant: {spell.BaseRangeConstant}");
                Debug.WriteLine($"BaseRangeMod: {spell.BaseRangeMod}");
                Debug.WriteLine($"Power: {spell.Power}");
                Debug.WriteLine($"SpellEconomyMod: {spell.SpellEconomyMod}");
                Debug.WriteLine($"FormulaVersion: {spell.FormulaVersion}");
                Debug.WriteLine($"ComponentLoss: {spell.ComponentLoss}");
                Debug.WriteLine($"Duration: {spell.Duration}");
                Debug.WriteLine($"DegradeModifier: {spell.DegradeModifier}");
                Debug.WriteLine($"DegradeLimit: {spell.DegradeLimit}");
                Debug.WriteLine($"PortalLifetime: {spell.PortalLifetime}");
                Debug.WriteLine($"CasterEffect: {spell.CasterEffect}");
                Debug.WriteLine($"TargetEffect: {spell.TargetEffect}");
                Debug.WriteLine($"FizzleEffect: {spell.FizzleEffect}");
                Debug.WriteLine($"RecoveryInterval: {spell.RecoveryInterval}");
                Debug.WriteLine($"RecoveryAmount: {spell.RecoveryAmount}");
                Debug.WriteLine($"DisplayOrder: {spell.DisplayOrder}");
                Debug.WriteLine($"NonComponentTargetType: {spell.NonComponentTargetType}");
                Debug.WriteLine($"ManaMod: {spell.ManaMod}");
                
                // Track the original name to detect changes
                string originalName = spell.Name;
                
                // Update basic properties
                spell.Name = nameBox.Text;
                spell.Description = descBox.Text;
                
                // Check if name has changed and warn about encryption impacts
                if (originalName != spell.Name)
                {
                    Debug.WriteLine("!!!! WARNING: SPELL NAME CHANGED !!!!");
                    Debug.WriteLine("This will change the component encryption key and may affect game functionality.");
                    Debug.WriteLine($"Old name: '{originalName}', New name: '{spell.Name}'");
                    
                    // Get information about the spell from DatReaderWriter
                    var spellType = typeof(DatReaderWriter.Types.SpellBase);
                    var methodInfo = spellType.GetMethod("GetHashKey", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    
                    if (methodInfo != null)
                    {
                        try {
                            // Create a temporary spell with old name to get old hash
                            var tempSpell = new DatReaderWriter.Types.SpellBase { Name = originalName, Description = spell.Description };
                            uint oldHash = (uint)methodInfo.Invoke(tempSpell, null);
                            
                            // Get hash with new name
                            uint newHash = (uint)methodInfo.Invoke(spell, null);
                            
                            Debug.WriteLine($"Encryption key changed: Old hash: 0x{oldHash:X8}, New hash: 0x{newHash:X8}");
                            Debug.WriteLine("This may affect how components are stored and retrieved.");
                        }
                        catch (Exception ex) {
                            Debug.WriteLine($"Could not check hash keys: {ex.Message}");
                        }
                    }
                }
                
                // Parse enum values from strings
                if (Enum.TryParse<MagicSchool>(schoolCombo.SelectedItem.ToString(), out MagicSchool selectedSchool))
                {
                    spell.School = selectedSchool;
                }
                
                if (Enum.TryParse<SpellCategory>(categoryCombo.SelectedItem.ToString(), out SpellCategory selectedCategory))
                {
                    spell.Category = selectedCategory;
                }
                
                if (Enum.TryParse<SpellType>(metaTypeCombo.SelectedItem.ToString(), out SpellType selectedType))
                {
                    spell.MetaSpellType = selectedType;
                }
                
                spell.Icon = (uint)iconBox.Value;
                spell.BaseMana = (uint)manaBox.Value;
                spell.BaseRangeConstant = (float)rangeConstBox.Value;
                spell.BaseRangeMod = (float)rangeModBox.Value;
                spell.Power = (uint)powerBox.Value;
                spell.Bitfield = (uint)bitfieldBox.Value;
                spell.DisplayOrder = (uint)displayOrderBox.Value;
                spell.SpellEconomyMod = (float)economyModBox.Value;
                spell.FormulaVersion = (uint)formulaVersionBox.Value;
                spell.ComponentLoss = (float)componentLossBox.Value;
                spell.ManaMod = (uint)manaMod.Value;
                
                // Update meta properties
                spell.MetaSpellId = (uint)metaIdBox.Value;
                
                // Update visual effects
                spell.CasterEffect = (uint)casterEffectBox.Value;
                spell.TargetEffect = (uint)targetEffectBox.Value;
                spell.FizzleEffect = (uint)fizzleEffectBox.Value;
                
                // Update duration and recovery
                spell.Duration = durationBox.Value;
                spell.DegradeModifier = (float)degradeModBox.Value;
                spell.DegradeLimit = (float)degradeLimitBox.Value;
                spell.RecoveryInterval = recoveryIntervalBox.Value;
                spell.RecoveryAmount = (float)recoveryAmountBox.Value;
                spell.PortalLifetime = portalLifetimeBox.Value;
                spell.NonComponentTargetType = (uint)nonComponentTargetBox.Value;
                
                // Update components from checkboxes
                List<uint> selectedComponentIdsRaw = new List<uint>();
                foreach (var checkbox in componentCheckboxes)
                {
                    if (checkbox.IsChecked == true && checkbox.Tag is uint componentId)
                    {
                        selectedComponentIdsRaw.Add(componentId);
                    }
                }
                
                // Ensure we don't exceed 8 components initially
                if (selectedComponentIdsRaw.Count > 8)
                {
                    selectedComponentIdsRaw = selectedComponentIdsRaw.Take(8).ToList();
                }

                // --- Sort components by type before assigning ---
                List<uint> sortedComponentIds = selectedComponentIdsRaw; // Default to raw if sorting fails
                DatReaderWriter.DBObjs.SpellComponentTable? componentTable = null;

                // Ensure DatabaseManager context exists
                Managers.DatabaseManager? dbManagerForSort = null;
                if (context != null && context.TryGetValue("DatabaseManager", out var dbManagerObjForSort) &&
                    dbManagerObjForSort is Managers.DatabaseManager managerInstance && managerInstance.CurrentDatabase != null)
                {
                    dbManagerForSort = managerInstance;
                    dbManagerForSort.CurrentDatabase.TryReadFile<DatReaderWriter.DBObjs.SpellComponentTable>(ACME.Constants.DatFileIds.SpellComponentsTableId, out componentTable);
                }

                if (componentTable != null)
                {
                    var componentsToSort = new List<(uint Id, DatReaderWriter.Enums.ComponentType Type)>();
                    foreach (var id in selectedComponentIdsRaw)
                    {
                        if (componentTable.Components.TryGetValue(id, out var compBase))
                        {
                            componentsToSort.Add((id, compBase.Type));
                        }
                        else
                        {
                             Debug.WriteLine($"Warning: Component ID {id} not found in SpellComponentTable during sorting. Adding as Undef.");
                             componentsToSort.Add((id, DatReaderWriter.Enums.ComponentType.Undef)); // Add with default type if lookup fails
                        }
                    }

                    // Sort based on the numeric value of ComponentType
                    componentsToSort.Sort((a, b) => ((uint)a.Type).CompareTo((uint)b.Type));

                    // Extract the sorted IDs
                    sortedComponentIds = componentsToSort.Select(c => c.Id).ToList();
                    Debug.WriteLine($"Sorted Components (Renderer): {string.Join(", ", sortedComponentIds)}"); 
                }
                else
                {
                    Debug.WriteLine("Error: Could not load SpellComponentTable to sort components. Saving in selection order.");
                    // Optionally show an error dialog here
                }
                // --- End sorting logic ---
                
                spell.Components = sortedComponentIds; // Assign the sorted list
                
                // Save changes to the database file
                try
                {
                    // Get the database manager from context if available
                    if (context != null && context.TryGetValue("DatabaseManager", out var dbManagerObj) && 
                        dbManagerObj is Managers.DatabaseManager dbManager)
                    {
                        // Get the current database
                        var db = dbManager.CurrentDatabase;
                        if (db != null)
                        {
                            // Get the SpellTable from the database
                            if (db.TryReadFile<DatReaderWriter.DBObjs.SpellTable>(ACME.Constants.DatFileIds.SpellTableId, out var spellTable) && spellTable != null)
                            {
                                // Use the originally captured spell ID
                                if (originalSpellId != 0 && spellTable.Spells.ContainsKey(originalSpellId))
                                {
                                    // Update the spell in the table using the original ID
                                    spellTable.Spells[originalSpellId] = spell;
                                    
                                    // Save the updated SpellTable back to the database
                                    bool success = db.TryWriteFile(spellTable);
                                    
                                    if (success)
                                    {
                                        // Show confirmation
                                        var confirmDialog = new ContentDialog
                                        {
                                            Title = "Spell Updated",
                                            Content = "The spell has been updated successfully and saved to the database.",
                                            CloseButtonText = "OK",
                                            XamlRoot = xamlRoot
                                        };
                                        
                                        await confirmDialog.ShowAsync();
                                        
                                        // Refresh the view to show updated data
                                        if (context != null)
                                        {
                                            // Try to re-render this object by notifying appropriate renderer
                                            if (context.TryGetValue("RefreshDetailView", out var refreshAction) && 
                                                refreshAction is Action<object> refresh)
                                            {
                                                refresh(spell);
                                            }
                                            // If the main window has a refresh method, call it
                                            else if (context.TryGetValue("RefreshView", out var refreshViewAction) && 
                                                     refreshViewAction is Action refreshView)
                                            {
                                                refreshView();
                                            }
                                        }
                                        
                                        // Verify the reloaded spell has the same core values as the original
                                        Debug.WriteLine("=== VERIFYING SAVE OPERATION ===");
                                        if (db.TryReadFile<DatReaderWriter.DBObjs.SpellTable>(ACME.Constants.DatFileIds.SpellTableId, out var verifyTable))
                                        {
                                            if (verifyTable.Spells.TryGetValue(originalSpellId, out var savedSpell))
                                            {
                                                // Check if the saved spell has the updated values
                                                Debug.WriteLine("Successfully read back saved spell:");
                                                Debug.WriteLine($"Name: {savedSpell.Name}");
                                                Debug.WriteLine($"Description: {savedSpell.Description}");
                                                Debug.WriteLine($"School: {savedSpell.School} ({(int)savedSpell.School})");
                                                Debug.WriteLine($"Category: {savedSpell.Category} ({(int)savedSpell.Category})");
                                                Debug.WriteLine($"MetaSpellType: {savedSpell.MetaSpellType} ({(int)savedSpell.MetaSpellType})");
                                                Debug.WriteLine($"MetaSpellId: {savedSpell.MetaSpellId}");
                                                Debug.WriteLine($"Bitfield: {savedSpell.Bitfield}");
                                                Debug.WriteLine($"Components: {string.Join(", ", savedSpell.Components)}");
                                                Debug.WriteLine($"BaseMana: {savedSpell.BaseMana}");
                                                Debug.WriteLine($"BaseRangeConstant: {savedSpell.BaseRangeConstant}");
                                                Debug.WriteLine($"BaseRangeMod: {savedSpell.BaseRangeMod}");
                                                Debug.WriteLine($"Power: {savedSpell.Power}");
                                                Debug.WriteLine($"SpellEconomyMod: {savedSpell.SpellEconomyMod}");
                                                Debug.WriteLine($"FormulaVersion: {savedSpell.FormulaVersion}");
                                                Debug.WriteLine($"ComponentLoss: {savedSpell.ComponentLoss}");
                                                Debug.WriteLine($"Duration: {savedSpell.Duration}");
                                                Debug.WriteLine($"DegradeModifier: {savedSpell.DegradeModifier}");
                                                Debug.WriteLine($"DegradeLimit: {savedSpell.DegradeLimit}");
                                                Debug.WriteLine($"PortalLifetime: {savedSpell.PortalLifetime}");
                                                Debug.WriteLine($"CasterEffect: {savedSpell.CasterEffect}");
                                                Debug.WriteLine($"TargetEffect: {savedSpell.TargetEffect}");
                                                Debug.WriteLine($"FizzleEffect: {savedSpell.FizzleEffect}");
                                                Debug.WriteLine($"RecoveryInterval: {savedSpell.RecoveryInterval}");
                                                Debug.WriteLine($"RecoveryAmount: {savedSpell.RecoveryAmount}");
                                                Debug.WriteLine($"DisplayOrder: {savedSpell.DisplayOrder}");
                                                Debug.WriteLine($"NonComponentTargetType: {savedSpell.NonComponentTargetType}");
                                                Debug.WriteLine($"ManaMod: {savedSpell.ManaMod}");
                                                
                                                bool schoolMatch = savedSpell.School == spell.School;
                                                bool categoryMatch = savedSpell.Category == spell.Category;
                                                bool typeMatch = savedSpell.MetaSpellType == spell.MetaSpellType;
                                                
                                                // Compare components in detail
                                                bool componentCountMatch = savedSpell.Components.Count == spell.Components.Count;
                                                bool allComponentsMatch = true;
                                                
                                                // Sort both lists to ensure consistent comparison
                                                var originalComponents = spell.Components.OrderBy(c => c).ToList();
                                                var savedComponents = savedSpell.Components.OrderBy(c => c).ToList();
                                                
                                                if (componentCountMatch)
                                                {
                                                    for (int i = 0; i < originalComponents.Count; i++)
                                                    {
                                                        if (originalComponents[i] != savedComponents[i])
                                                        {
                                                            allComponentsMatch = false;
                                                            Debug.WriteLine($"Component mismatch at index {i}: Expected {originalComponents[i]}, Got {savedComponents[i]}");
                                                            break;
                                                        }
                                                    }
                                                }
                                                else
                                                {
                                                    allComponentsMatch = false;
                                                    Debug.WriteLine($"Component count mismatch: Expected {spell.Components.Count}, Got {savedSpell.Components.Count}");
                                                    
                                                    // Show detailed differences
                                                    var missingComponents = originalComponents.Except(savedComponents).ToList();
                                                    var extraComponents = savedComponents.Except(originalComponents).ToList();
                                                    
                                                    if (missingComponents.Any())
                                                        Debug.WriteLine($"Missing components: {string.Join(", ", missingComponents)}");
                                                    
                                                    if (extraComponents.Any())
                                                        Debug.WriteLine($"Extra components: {string.Join(", ", extraComponents)}");
                                                }
                                                
                                                // Verify numeric fields
                                                bool durationMatch = savedSpell.Duration == spell.Duration;
                                                bool degradeModMatch = savedSpell.DegradeModifier == spell.DegradeModifier;
                                                bool degradeLimitMatch = savedSpell.DegradeLimit == spell.DegradeLimit;
                                                bool rangeModMatch = savedSpell.BaseRangeMod == spell.BaseRangeMod;
                                                bool recoveryAmountMatch = savedSpell.RecoveryAmount == spell.RecoveryAmount;
                                                bool bitfieldMatch = savedSpell.Bitfield == spell.Bitfield;
                                                
                                                Debug.WriteLine($"Verification Results:");
                                                Debug.WriteLine($"- Core Values: School Match: {schoolMatch}, Category Match: {categoryMatch}, Type Match: {typeMatch}");
                                                Debug.WriteLine($"- Components: Count Match: {componentCountMatch}, All Match: {allComponentsMatch}");
                                                Debug.WriteLine($"- Numeric Values: Duration: {durationMatch}, DegradeMod: {degradeModMatch}, DegradeLimit: {degradeLimitMatch}");
                                                Debug.WriteLine($"- More Values: RangeMod: {rangeModMatch}, RecoveryAmount: {recoveryAmountMatch}, Bitfield: {bitfieldMatch}");
                                            }
                                        }
                                    }
                                    else
                                    {
                                        // Show error if saving failed
                                        var errorDialog = new ContentDialog
                                        {
                                            Title = "Error Saving Changes",
                                            Content = "The spell was updated in memory but couldn't be saved to the database file.",
                                            CloseButtonText = "OK",
                                            XamlRoot = xamlRoot
                                        };
                                        
                                        await errorDialog.ShowAsync();
                                    }
                                }
                                else
                                {
                                    // Couldn't find the spell in the table
                                    var errorDialog = new ContentDialog
                                    {
                                        Title = "Error Saving Changes",
                                        Content = $"The spell couldn't be found in the spell table (ID: {originalSpellId}). Changes were made in memory only.",
                                        CloseButtonText = "OK",
                                        XamlRoot = xamlRoot
                                    };
                                    
                                    Debug.WriteLine($"Error saving spell: Could not find spell with ID {originalSpellId} in SpellTable. Dictionary contains {spellTable.Spells.Count} spells.");
                                    Debug.WriteLine($"Spell properties - Name: {spell.Name}, School: {spell.School}, Category: {spell.Category}");
                                    
                                    await errorDialog.ShowAsync();
                                }
                            }
                            else
                            {
                                // Couldn't load the spell table
                                var errorDialog = new ContentDialog
                                {
                                    Title = "Error Saving Changes",
                                    Content = "The spell table couldn't be loaded from the database. Changes were made in memory only.",
                                    CloseButtonText = "OK",
                                    XamlRoot = xamlRoot
                                };
                                
                                await errorDialog.ShowAsync();
                            }
                        }
                        else
                        {
                            // No active database
                            var warningDialog = new ContentDialog
                            {
                                Title = "Warning",
                                Content = "Spell was updated in memory but changes may not be saved permanently because no active database was found.",
                                CloseButtonText = "OK",
                                XamlRoot = xamlRoot
                            };
                            
                            await warningDialog.ShowAsync();
                        }
                    }
                    else
                    {
                        // Database manager not found in context, show warning
                        var warningDialog = new ContentDialog
                        {
                            Title = "Warning",
                            Content = "Spell was updated in memory but changes may not be saved permanently because the database manager couldn't be accessed.",
                            CloseButtonText = "OK",
                            XamlRoot = xamlRoot
                        };
                        
                        await warningDialog.ShowAsync();
                    }
                }
                catch (Exception ex)
                {
                    // Show error if saving failed
                    var errorDialog = new ContentDialog
                    {
                        Title = "Error Saving Changes",
                        Content = $"The spell was updated but there was an error saving changes to the database: {ex.Message}",
                        CloseButtonText = "OK",
                        XamlRoot = xamlRoot
                    };
                    
                    await errorDialog.ShowAsync();
                }

                // Debug log after updating
                Debug.WriteLine("=== SPELL AFTER UPDATE ===");
                Debug.WriteLine($"Name: {spell.Name}");
                Debug.WriteLine($"Description: {spell.Description}");
                Debug.WriteLine($"School: {spell.School} ({(int)spell.School})");
                Debug.WriteLine($"Category: {spell.Category} ({(int)spell.Category})");
                Debug.WriteLine($"MetaSpellType: {spell.MetaSpellType} ({(int)spell.MetaSpellType})");
                Debug.WriteLine($"MetaSpellId: {spell.MetaSpellId}");
                Debug.WriteLine($"Bitfield: {spell.Bitfield}");
                Debug.WriteLine($"Components: {string.Join(", ", spell.Components)}");
                Debug.WriteLine($"BaseMana: {spell.BaseMana}");
                Debug.WriteLine($"BaseRangeConstant: {spell.BaseRangeConstant}");
                Debug.WriteLine($"BaseRangeMod: {spell.BaseRangeMod}");
                Debug.WriteLine($"Power: {spell.Power}");
                Debug.WriteLine($"SpellEconomyMod: {spell.SpellEconomyMod}");
                Debug.WriteLine($"FormulaVersion: {spell.FormulaVersion}");
                Debug.WriteLine($"ComponentLoss: {spell.ComponentLoss}");
                Debug.WriteLine($"Duration: {spell.Duration}");
                Debug.WriteLine($"DegradeModifier: {spell.DegradeModifier}");
                Debug.WriteLine($"DegradeLimit: {spell.DegradeLimit}");
                Debug.WriteLine($"PortalLifetime: {spell.PortalLifetime}");
                Debug.WriteLine($"CasterEffect: {spell.CasterEffect}");
                Debug.WriteLine($"TargetEffect: {spell.TargetEffect}");
                Debug.WriteLine($"FizzleEffect: {spell.FizzleEffect}");
                Debug.WriteLine($"RecoveryInterval: {spell.RecoveryInterval}");
                Debug.WriteLine($"RecoveryAmount: {spell.RecoveryAmount}");
                Debug.WriteLine($"DisplayOrder: {spell.DisplayOrder}");
                Debug.WriteLine($"NonComponentTargetType: {spell.NonComponentTargetType}");
                Debug.WriteLine($"ManaMod: {spell.ManaMod}");
            }
        }
    }
} 