using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Microsoft.UI;
using Windows.UI.Text;
using Microsoft.UI.Text;
using ACME.Utils; // For FontWeightValues
using System.Reflection; // For GetProperties
using System.Linq;
using System.Diagnostics;
using DatReaderWriter.Types; // Needed for Position type check
using DatReaderWriter.Enums; // For MagicSchool and SpellCategory enums
using System.Threading.Tasks;

namespace ACME.Renderers
{
    /// <summary>
    /// Responsible for orchestrating the rendering of details for selected items
    /// into a designated StackPanel using specific renderers.
    /// </summary>
    public class DetailRenderer
    {
        private readonly StackPanel _detailPanel;
        private readonly Dictionary<Type, IObjectRenderer> _renderers = new();
        private readonly IObjectRenderer _genericRenderer = new GenericObjectRenderer(); // Fallback renderer
        private readonly Dictionary<string, IObjectRenderer> _contextRenderers = new(); // For context-based dispatch

        public DetailRenderer(StackPanel detailPanel)
        {
            _detailPanel = detailPanel ?? throw new ArgumentNullException(nameof(detailPanel));
            RegisterRenderers();
        }

        /// <summary>
        /// Refreshes the displayed details for the given item
        /// </summary>
        public void RefreshDetails(object item, Dictionary<string, object>? context = null)
        {
            DisplayItemDetails(item, context);
        }

        /// <summary>
        /// Registers specific renderers for known types or context hints.
        /// </summary>
        private void RegisterRenderers()
        { 
            // Register by Type (Example - if we had a specific renderer for SpellSet)
            _renderers.Add(typeof(DatReaderWriter.Types.SpellSet), new SpellSetRenderer());
            
            // Using internal SpellBaseRenderer implementation
            _renderers.Add(typeof(DatReaderWriter.Types.SpellBase), new SpellBaseRenderer());
            
            // Register by Context Hint (ObjectType)
            _contextRenderers.Add("HeritageGroupCG", new HeritageGroupRenderer()); 
            // Add other context-specific renderers here
        }

        /// <summary>
        /// Custom renderer for SpellBase objects with editing functionality
        /// </summary>
        private class SpellBaseRenderer : IObjectRenderer
        {
            private readonly GenericObjectRenderer _genericRenderer = new GenericObjectRenderer();
            
            public void Render(Panel targetPanel, object data, Dictionary<string, object>? context)
            {
                if (!(data is DatReaderWriter.Types.SpellBase spellBase))
                {
                    // Fallback to generic renderer if not a SpellBase
                    _genericRenderer.Render(targetPanel, data, context);
                    return;
                }
                
                // Create a container for our custom controls
                var controlsPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
                
                // Create Edit button - now we always show "Edit Spell" as we always have write access
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
                
                // Add a status indicator - now we just show that editing is available
                var statusText = new TextBlock
                {
                    Text = "You can edit this spell by clicking the Edit button above.",
                    FontStyle = Windows.UI.Text.FontStyle.Italic,
                    Foreground = new SolidColorBrush(Microsoft.UI.Colors.Green),
                    Margin = new Thickness(0, 0, 0, 10)
                };
                
                controlsPanel.Children.Add(statusText);
                
                // Add separator line
                var separator = new Rectangle
                {
                    Height = 1,
                    Fill = new SolidColorBrush(Microsoft.UI.Colors.LightGray),
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
                
                // Show all components across four columns - REMOVE ALPHABETICAL SORTING
                var componentsToShow = componentLookup.ToList(); // Use original order from lookup
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
                    Debug.WriteLine("=== SPELL BEFORE UPDATE (DetailRenderer) ===");
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
                    if (Enum.TryParse<MagicSchool>(schoolCombo.SelectedItem?.ToString(), out MagicSchool selectedSchool))
                    {
                        spell.School = selectedSchool;
                        Debug.WriteLine($"School parsed successfully: {selectedSchool} ({(int)selectedSchool})");
                    }
                    else
                    {
                        Debug.WriteLine($"Failed to parse School from: {schoolCombo.SelectedItem}");
                    }
                    
                    if (Enum.TryParse<SpellCategory>(categoryCombo.SelectedItem?.ToString(), out SpellCategory selectedCategory))
                    {
                        spell.Category = selectedCategory;
                        Debug.WriteLine($"Category parsed successfully: {selectedCategory} ({(int)selectedCategory})");
                    }
                    else
                    {
                        Debug.WriteLine($"Failed to parse Category from: {categoryCombo.SelectedItem}");
                    }
                    
                    if (Enum.TryParse<SpellType>(metaTypeCombo.SelectedItem?.ToString(), out SpellType selectedType))
                    {
                        spell.MetaSpellType = selectedType;
                        Debug.WriteLine($"MetaSpellType parsed successfully: {selectedType} ({(int)selectedType})");
                    }
                    else
                    {
                        Debug.WriteLine($"Failed to parse MetaSpellType from: {metaTypeCombo.SelectedItem}");
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
                    
                    // === Update components while preserving original order ===
                
                    // 1. Store the original order
                    var originalComponentOrder = spell.Components.ToList(); // Make a copy
                    Debug.WriteLine($"Original Components Order Before Edit: {string.Join(", ", originalComponentOrder)}");

                    // 2. Get the set of currently checked component IDs
                    var checkedComponentIds = new HashSet<uint>();
                    foreach (var checkbox in componentCheckboxes)
                    {
                        if (checkbox.IsChecked == true && checkbox.Tag is uint componentId)
                        {
                            checkedComponentIds.Add(componentId);
                        }
                    }
                    Debug.WriteLine($"Checked Components Before Processing: {string.Join(", ", checkedComponentIds.OrderBy(id => id))}"); // Log checked IDs

                    // 3. Build the new list: Start with original, checked components in their original order
                    var newComponentList = new List<uint>();
                    var originalComponentsStillChecked = new HashSet<uint>(); // Track which originals were kept

                    foreach (var originalId in originalComponentOrder)
                    {
                        if (checkedComponentIds.Contains(originalId))
                        {
                            newComponentList.Add(originalId);
                            originalComponentsStillChecked.Add(originalId); // Keep track
                        }
                    }
                    Debug.WriteLine($"Components After Adding Originals Still Checked: {string.Join(", ", newComponentList)}");

                    // 4. Identify and sort the newly added components (those checked now but NOT in the original list that were kept)
                    var newlyAddedComponents = new List<uint>();
                    foreach(var checkedId in checkedComponentIds)
                    {
                        // If a checked ID wasn't part of the original list that was kept, it's new
                        if (!originalComponentsStillChecked.Contains(checkedId)) 
                        {
                            newlyAddedComponents.Add(checkedId);
                        }
                    }
                    newlyAddedComponents.Sort(); // Sort *only* the new components by ID for consistent appending
                    Debug.WriteLine($"Newly Added Components (Sorted): {string.Join(", ", newlyAddedComponents)}");
                    
                    // 5. Append the sorted new components
                    newComponentList.AddRange(newlyAddedComponents);
                    Debug.WriteLine($"Components After Adding New Ones: {string.Join(", ", newComponentList)}");
                    
                    // 6. Ensure we don't exceed 8 components (truncate from the end if necessary)
                    if (newComponentList.Count > 8)
                    {
                        newComponentList = newComponentList.Take(8).ToList();
                        Debug.WriteLine("Warning: More than 8 components selected. Truncated to the first 8 based on original order + sorted new additions.");
                    }

                    // 7. Assign the final ordered list
                    spell.Components = newComponentList;
                    Debug.WriteLine($"Final Components Order Assigned: {string.Join(", ", spell.Components)}");

                    // Debug log after updating
                    Debug.WriteLine("=== SPELL AFTER UPDATE (DetailRenderer) ===");
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
                                            // Additional Debug: Verify the spell was saved correctly by reading it back
                                            Debug.WriteLine("=== VERIFYING SAVE OPERATION (DetailRenderer) ===");
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
                                                    
                                                    // Compare components in detail (order matters now)
                                                    bool componentCountMatch = savedSpell.Components.Count == spell.Components.Count;
                                                    bool allComponentsMatch = componentCountMatch; // Start assuming true if counts match
                                                    
                                                    // Compare elements directly in order, without sorting
                                                    if (componentCountMatch)
                                                    {
                                                        for (int i = 0; i < spell.Components.Count; i++)
                                                        {
                                                            if (spell.Components[i] != savedSpell.Components[i])
                                                            {
                                                                allComponentsMatch = false;
                                                                Debug.WriteLine($"Component mismatch at index {i}: Expected {spell.Components[i]}, Got {savedSpell.Components[i]}");
                                                                break;
                                                            }
                                                        }
                                                    }
                                                    else
                                                    {
                                                        // Counts don't match, definitely not a match
                                                        allComponentsMatch = false; 
                                                        Debug.WriteLine($"Component count mismatch: Expected {spell.Components.Count}, Got {savedSpell.Components.Count}");
                                                        
                                                        // Show detailed differences using original unsorted lists
                                                        var missingComponents = spell.Components.Except(savedSpell.Components).ToList();
                                                        var extraComponents = savedSpell.Components.Except(spell.Components).ToList();
                                                        
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
                                                    
                                                    Debug.WriteLine($"Verification Results - School Match: {schoolMatch}, Category Match: {categoryMatch}, Type Match: {typeMatch}");
                                                }
                                                else
                                                {
                                                    Debug.WriteLine($"Error: Could not find spell with ID {originalSpellId} in verification read");
                                                }
                                            }
                                            else
                                            {
                                                Debug.WriteLine("Error: Could not read spell table for verification");
                                            }
                                            
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
                }
            }
        }

        public void DisplayItemDetails(object? selectedItem, object? displayContext = null)
        {   
            _detailPanel.Children.Clear(); // Clear panel before adding new content

            if (selectedItem == null)
            {
                AddInfoMessage("Select an item to view details.");
                return;
            }

            var contextDict = displayContext as Dictionary<string, object> ?? new Dictionary<string, object>();
            
            // Add a reference to this renderer's refresh method
            if (!contextDict.ContainsKey("RefreshDetailView"))
            {
                contextDict["RefreshDetailView"] = new Action<object>(obj => RefreshDetails(obj, contextDict));
            }
            
            string title = RendererHelpers.DetermineObjectTitle(selectedItem); // Use helper
            AddTitle(title);

            // Create a container panel for the specific renderer's content
            // This allows the main DetailRenderer to manage the title consistently.
            var contentPanel = new StackPanel() { Margin = new Thickness(12, 0, 0, 0) }; // Add padding for content area
            _detailPanel.Children.Add(contentPanel);

            IObjectRenderer? selectedRenderer = null;

            // --- NEW: Explicit handling for Dictionary Entry Anonymous Type ---
            var itemType = selectedItem.GetType();
            var displayTextProp = itemType.GetProperty("DisplayText");
            var valueProp = itemType.GetProperty("Value");

            if (itemType.Name.Contains("AnonymousType") && displayTextProp != null && valueProp != null && displayTextProp.PropertyType == typeof(string))
            {
                string key = displayTextProp.GetValue(selectedItem) as string ?? "(unknown key)";
                object? value = valueProp.GetValue(selectedItem);

                RendererHelpers.AddSimplePropertyRow(contentPanel, "Key", key);
                RendererHelpers.AddSeparator(contentPanel);

                if (value != null)
                {
                    RendererHelpers.AddSectionHeader(contentPanel, "Value");

                    // --- NEW: Directly display if Value is string, otherwise render --- 
                    if (value is string stringValue)
                    {
                         // Use AddSimplePropertyRow or just a TextBlock to display the string value directly
                         RendererHelpers.AddSimplePropertyRow(contentPanel, "", stringValue); // Empty label for direct value display
                    }
                    else 
                    { 
                        // Value is not a string (e.g., ChatEmoteData), find appropriate renderer
                        IObjectRenderer? valueRenderer = null;
                        // 1. Try context-based dispatch for the value
                        if (contextDict?.TryGetValue("ObjectType", out var valueObjTypeHint) == true && 
                            valueObjTypeHint is string valueHint && 
                            _contextRenderers.TryGetValue(valueHint, out valueRenderer))
                        { /* Found renderer */ }
                        // 2. If no context match, try type-based dispatch for the value
                        if (valueRenderer == null)
                        {
                             _renderers.TryGetValue(value.GetType(), out valueRenderer);
                        }
                        // 3. Fallback to generic renderer for the value
                        valueRenderer ??= _genericRenderer;
                        
                        try
                        {
                            Debug.WriteLine($"--- DetailRenderer: About to render VALUE of type: {value?.GetType().FullName ?? "null"} ---"); // Diagnostic message
                            // Render the VALUE using the chosen renderer
                            valueRenderer.Render(contentPanel, value, contextDict);
                        }
                        catch (Exception ex)
                        {
                            RendererHelpers.AddErrorMessageToPanel(contentPanel, $"Error rendering Value details: {ex.Message}");
                            System.Diagnostics.Debug.WriteLine($"Rendering Error (Value): {ex}\nContext: {displayContext}");
                        }
                    }
                    // --- END NEW ---
                }
                else
                {
                     RendererHelpers.AddInfoMessageToPanel(contentPanel, "Value: null", Microsoft.UI.Colors.Gray);
                }
                return; // Handled this specific anonymous type
            }
            // --- END NEW SECTION ---

            // If not the specific anonymous type, proceed with normal renderer selection
            // 1. Try context-based dispatch first
            if (contextDict?.TryGetValue("ObjectType", out var objTypeHint) == true && 
                objTypeHint is string hint && 
                _contextRenderers.TryGetValue(hint, out selectedRenderer))
            {
                 // Found renderer based on context hint
            }
            
            // 2. If no context match, try type-based dispatch
            if (selectedRenderer == null)
            {
                _renderers.TryGetValue(selectedItem.GetType(), out selectedRenderer);
            }

            // 3. Fallback to generic renderer
            selectedRenderer ??= _genericRenderer;

            try
            {
                selectedRenderer.Render(contentPanel, selectedItem, contextDict);
            }
            catch (Exception ex)
            {
                // Add error message within the content panel
                RendererHelpers.AddErrorMessageToPanel(contentPanel, $"Error rendering details: {ex.Message}");
                 // Optionally log the full exception
                 System.Diagnostics.Debug.WriteLine($"Rendering Error: {ex}\nContext: {displayContext}");
            }
        }

        /// <summary>
        /// Clears the detail panel and displays a message.
        /// </summary>
        public void ClearAndSetMessage(string message, bool isError = false)
        { 
            _detailPanel.Children.Clear();
            AddInfoMessage(message, isError ? Microsoft.UI.Colors.OrangeRed : Microsoft.UI.Colors.Gray);
        }

        /// <summary>
        /// Clears the detail panel and adds the standard title and message.
        /// </summary>
        public void ClearAndAddDefaultTitle()
        {
             _detailPanel.Children.Clear();
             AddTitle("Item Details");
             AddInfoMessage("Select an item from the list to view details.", Microsoft.UI.Colors.Gray);
        }

        /// <summary>
        /// Adds a title to the main detail panel.
        /// </summary>
        private void AddTitle(string title)
        {
            // Assume title should always be added at the top, even if content rendering fails
            _detailPanel.Children.Insert(0, new TextBlock()
            {
                Text = title,
                Style = (Style)Application.Current.Resources["TitleTextBlockStyle"],
                Margin = new Thickness(0, 0, 0, 12) // Ensure bottom margin
            });
        }

        /// <summary>
        /// Adds an informational message to the main detail panel.
        /// </summary>
        public void AddInfoMessage(string message, Windows.UI.Color? color = null)
        {
             // Use helper, add to the main panel (_detailPanel)
             RendererHelpers.AddInfoMessageToPanel(_detailPanel, message, color ?? Microsoft.UI.Colors.Gray);
        }

        // AddErrorMessage is removed as errors related to rendering specific content
        // should be added to the contentPanel by the renderer or the catch block in DisplayItemDetails.
        // General errors (like null selection) use AddInfoMessage.

        // All specific rendering logic (DisplayObjectPropertiesInternal, RenderHeritageGroupDetails, etc.)
        // and most helper methods (AddSeparator, AddSectionHeader, etc.)
        // have been moved to GenericObjectRenderer, HeritageGroupRenderer, or RendererHelpers.
    }
} 