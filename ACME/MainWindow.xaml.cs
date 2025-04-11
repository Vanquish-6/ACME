using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Text;
using System.Threading.Tasks;
using WinRT.Interop;
using DatReaderWriter;
using ACME.Constants;
using ACME.Managers;
using ACME.Models;
using ACME.Renderers;
using ACME.Utils;
using ACME.Properties;

namespace ACME
{
    /// <summary>
    /// The main application window.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        // --- Managers and Renderer Instances ---
        private readonly DatabaseManager _databaseManager;
        private readonly TreeViewManager _treeViewManager;
        private readonly TreeViewDataLoader _treeViewDataLoader;
        private readonly DetailRenderer _detailRenderer;

        public MainWindow()
        {
            this.InitializeComponent();

            // Set the window icon using our helper class
            IconHelper.SetWindowIcon(this);

            // --- Instantiate Managers and Renderer ---
            _databaseManager = new DatabaseManager();
            _treeViewManager = new TreeViewManager(StructureTreeView); // Pass TreeView control
            _detailRenderer = new DetailRenderer(DetailStackPanel); // Pass Detail panel

            // Create dependencies for TreeViewDataLoader
            var listViewSelectionHandler = new ListViewSelectionHandler(_databaseManager, _detailRenderer, ItemListView);
            var spellFilterManager = new SpellFilterManager();
            var spellLoader = new SpellLoader(spellFilterManager, _detailRenderer);

            _treeViewDataLoader = new TreeViewDataLoader(
                _databaseManager,
                ItemListView,       // Pass ListView control
                _detailRenderer,    // Pass DetailRenderer instance
                listViewSelectionHandler, // Pass handlers
                spellFilterManager,
                spellLoader
            );

            // --- Wire up Events ---
            _databaseManager.DatabasesChanged += DatabaseManager_DatabasesChanged;
            StructureTreeView.SelectionChanged += StructureTreeView_SelectionChanged; // Use TreeViewDataLoader
            _treeViewDataLoader.RelevantDataViewChanged += TreeViewDataLoader_RelevantDataViewChanged; // Handle filter visibility
            // ItemListView.SelectionChanged -= ItemListView_SelectionChanged; // Remove old handler (if it was attached in XAML or code)
            // ItemListView selection is now handled internally by TreeViewDataLoader

            // --- Window Closing Event ---
            this.Closed += MainWindow_Closed; // Ensure cleanup on close

            // --- Initial UI State ---
            UpdateStatusBar();
            _detailRenderer.ClearAndSetMessage("Use File > Open to load a .dat file."); // Initial instruction
        }

        /// <summary>
        /// Handles changes in the loaded databases.
        /// </summary>
        private void DatabaseManager_DatabasesChanged(object? sender, DatabasesChangedEventArgs e)
        {
            _treeViewManager.ClearTreeView();
            foreach (var dbInfo in e.LoadedDatabases)
            {
                 string dbId = _databaseManager.GetDatabaseId(dbInfo); // Get consistent ID
                 if (dbInfo.Type == DatabaseType.Portal && dbInfo.Database is PortalDatabase portalDb)
                 {
                      _treeViewManager.PopulatePortalTreeView(portalDb, dbInfo.FileName, dbId);
                 }
                 else if (dbInfo.Type == DatabaseType.Cell && dbInfo.Database is CellDatabase cellDb)
                 {
                      _treeViewManager.PopulateCellTreeView(cellDb, dbInfo.FileName, dbId);
                 }
            }

            UpdateStatusBar(e.LoadedDatabases);

            // Update detail view based on database changes
            UpdateDetailMessageAfterDbChange(e);

#if DEBUG
            LogLoadedDatabasesForDebug();
#endif
        }

        /// <summary>
        /// Updates the detail message panel based on the database change event.
        /// </summary>
        private void UpdateDetailMessageAfterDbChange(DatabasesChangedEventArgs e)
        {
            ItemListView.ItemsSource = null; // Clear the list view
            if (e.LoadedDatabases.Count == 0)
            {
                _detailRenderer.ClearAndSetMessage("No databases loaded. Use File > Open.");
            }
            else if (e.AddedDatabase != null) // Consider if other cases need messages (e.g., closing last DB)
            {
                _detailRenderer.ClearAndSetMessage("Database list updated. Select an item from the tree.");
            }
            // If a database was removed but others remain, we might not need a specific message,
            // as the user likely initiated the close.
        }

        /// <summary>
        /// Handles the selection change in the TreeView.
        /// Delegates the data loading to the TreeViewDataLoader.
        /// </summary>
        private async void StructureTreeView_SelectionChanged(TreeView sender, TreeViewSelectionChangedEventArgs args)
        {
            try
            {
                if (_treeViewDataLoader != null)
                {
                    TreeViewNode? selectedNode = args.AddedItems.FirstOrDefault() as TreeViewNode; // Get the specific node
                    await _treeViewDataLoader.ProcessTreeViewSelectionAsync(selectedNode); // Call the updated method
                }
            }
            catch (Exception ex)
            {
                // Log the exception (using Debug.WriteLine for simplicity, consider a proper logging framework)
                Debug.WriteLine($"Error in StructureTreeView_SelectionChanged: {ex.Message}");
                Debug.WriteLine(ex.StackTrace);
                // Optionally, inform the user
                _detailRenderer?.ClearAndSetMessage($"An error occurred processing the selection: {ex.Message}", isError: true);
            }
        }

        /// <summary>
        /// Handles the File -> Open menu item click.
        /// </summary>
        private async void OpenMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ItemListView.ItemsSource = null;
                _detailRenderer.ClearAndSetMessage("Opening file...");

                var (success, errorMessage) = await _databaseManager.PickAndLoadDatabaseAsync(this);

                if (!success)
                {
                    if (!string.IsNullOrEmpty(errorMessage))
                    {
                        _detailRenderer.ClearAndSetMessage($"Error loading file: {errorMessage}", isError: true);
                        Debug.WriteLine($"Error loading file: {errorMessage}");
                    }
                    else
                    {
                        _detailRenderer.ClearAndSetMessage("File open cancelled or no databases loaded.");
                    }
                }
            }
            catch (Exception ex)
            {
                // Log the exception
                Debug.WriteLine($"Error in OpenMenuItem_Click: {ex.Message}");
                Debug.WriteLine(ex.StackTrace);
                // Inform the user
                _detailRenderer?.ClearAndSetMessage($"An unexpected error occurred while opening the file: {ex.Message}", isError: true);
            }
        }

        /// <summary>
        /// Handles the File -> Close All menu item click.
        /// </summary>
        private void CloseAllMenuItem_Click(object sender, RoutedEventArgs e)
        {
            _databaseManager.CloseAllDatabases();
        }

        /// <summary>
        /// Updates the status bar text based on loaded databases.
        /// </summary>
        private void UpdateStatusBar(IReadOnlyList<DatabaseInfo>? loadedDbs = null)
        {
            var databases = loadedDbs ?? _databaseManager.LoadedDatabases;
            if (databases.Count == 0)
            {
                LoadedFileStatusText.Text = "No databases loaded";
            }
            else
            {
                LoadedFileStatusText.Text = "Loaded: " + string.Join(" | ", 
                    databases.Select(db => $"{db.FileName} ({db.Type}, {(db.CanWrite ? "Read-Write" : "Read-Only")})"));
            }
        }

#if DEBUG
        /// <summary>
        /// Helper to log database state for debugging.
        /// </summary>
        private void LogLoadedDatabasesForDebug()
        {
            Debug.WriteLine("==== MainWindow: Loaded Databases Report ====");
            if (_databaseManager.LoadedDatabases.Count == 0)
            {
                Debug.WriteLine("  No databases loaded.");
                return;
            }
            foreach (var dbInfo in _databaseManager.LoadedDatabases)
            {
                string dbId = _databaseManager.GetDatabaseId(dbInfo);
                Debug.WriteLine($"  - {dbInfo.FileName} (Type: {dbInfo.Type}, ID: {dbId})");
            }
             Debug.WriteLine($"  Current DB Set in Manager: {(_databaseManager.CurrentDatabase != null)}");
        }
#endif

        /// <summary>
        /// Handles the window closing event to ensure database resources are released.
        /// </summary>
        private void MainWindow_Closed(object sender, WindowEventArgs args)
        {
            _databaseManager?.CloseAllDatabases(); // Dispose all open databases
            Debug.WriteLine("MainWindow_Closed: Called CloseAllDatabases.");
        }

        /// <summary>
        /// Event handler for the spell name filter TextBox.
        /// Triggers filtering in the TreeViewDataLoader.
        /// </summary>
        private void SpellNameFilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_treeViewDataLoader != null && sender is TextBox filterBox)
            {
                _treeViewDataLoader.ApplySpellFilter(filterBox.Text);
            }
        }

        /// <summary>
        /// Shows or hides the spell filter panel based on the data context.
        /// </summary>
        private void TreeViewDataLoader_RelevantDataViewChanged(object? sender, RelevantDataViewChangedEventArgs e)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                SpellFilterPanel.Visibility = e.IsSpellViewRelevant ? Visibility.Visible : Visibility.Collapsed;
                if (!e.IsSpellViewRelevant)
                {
                    SpellNameFilterTextBox.Text = string.Empty;
                }
            });
        }
    }
}
