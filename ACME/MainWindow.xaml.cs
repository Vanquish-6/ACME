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
            _treeViewDataLoader = new TreeViewDataLoader(
                _databaseManager,
                ItemListView,       // Pass ListView control
                _detailRenderer     // Pass DetailRenderer instance
            );

            // --- Wire up Events ---
            _databaseManager.DatabasesChanged += DatabaseManager_DatabasesChanged;
            StructureTreeView.SelectionChanged += StructureTreeView_SelectionChanged; // Use TreeViewDataLoader
            // ItemListView.SelectionChanged -= ItemListView_SelectionChanged; // Remove old handler (if it was attached in XAML or code)
            // ItemListView selection is now handled internally by TreeViewDataLoader

            // --- Initial UI State ---
            UpdateStatusBar();
            _detailRenderer.ClearAndSetMessage("Use File > Open to load a .dat file."); // Initial instruction
        }

        /// <summary>
        /// Handles changes in the loaded databases.
        /// </summary>
        private void DatabaseManager_DatabasesChanged(object? sender, DatabasesChangedEventArgs e)
        {
            // Clear and repopulate the TreeView based on the new list of databases
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

            // Update status bar
            UpdateStatusBar(e.LoadedDatabases);

            // Reset list and detail views if databases changed
            ItemListView.ItemsSource = null;
             if (e.LoadedDatabases.Count == 0)
             {
                  _detailRenderer.ClearAndSetMessage("No databases loaded. Use File > Open.");
             }
             else if (e.AddedDatabase != null)
             {
                 // If a specific DB was added/removed, maybe show a generic message
                 _detailRenderer.ClearAndSetMessage("Database list updated. Select an item from the tree.");
             }
             // Else (initial load?), keep the existing message or update as needed

            LogLoadedDatabasesForDebug();
        }

        /// <summary>
        /// Handles the selection change in the TreeView.
        /// Delegates the data loading to the TreeViewDataLoader.
        /// </summary>
        private async void StructureTreeView_SelectionChanged(TreeView sender, TreeViewSelectionChangedEventArgs args)
        {
            if (_treeViewDataLoader != null)
            {
                // Extract the newly selected node from the event args
                TreeViewNode? selectedNode = args.AddedItems.FirstOrDefault() as TreeViewNode; // Get the specific node

                // Pass the selected node directly to the processing method
                await _treeViewDataLoader.ProcessTreeViewSelectionAsync(selectedNode); // Call the updated method
            }
        }

        /// <summary>
        /// Handles the File -> Open menu item click.
        /// </summary>
        private async void OpenMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // Clear previous views immediately for better feedback
            ItemListView.ItemsSource = null;
            _detailRenderer.ClearAndSetMessage("Opening file...");

            var (success, errorMessage) = await _databaseManager.PickAndLoadDatabaseAsync(this);

            if (!success)
            {
                if (!string.IsNullOrEmpty(errorMessage))
                {   // Show error if loading failed
                    _detailRenderer.ClearAndSetMessage($"Error loading file: {errorMessage}", isError: true);
                    Debug.WriteLine($"Error loading file: {errorMessage}");
                }
                else
                {   // User cancelled picker - restore previous message or show default
                    _detailRenderer.ClearAndSetMessage("File open cancelled or no databases loaded.");
                }
            }
            // On success, the DatabasesChanged event handles UI updates.
        }

        /// <summary>
        /// Handles the File -> Close All menu item click.
        /// </summary>
        private void CloseAllMenuItem_Click(object sender, RoutedEventArgs e)
        {
            _databaseManager.CloseAllDatabases();
            // UI updates are handled by the DatabasesChanged event handler
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
                // Concatenate names for status bar, include access type
                LoadedFileStatusText.Text = "Loaded: " + string.Join(" | ", 
                    databases.Select(db => $"{db.FileName} ({db.Type}, {(db.CanWrite ? "Read-Write" : "Read-Only")})"));
            }
        }

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

        // --------------------------------------------------
        // REMOVED METHODS (Moved to Manager/Renderer classes)
        // --------------------------------------------------
        // - AddDatabaseToTree
        // - PopulatePortalTreeView (multiple overloads)
        // - PopulateCellTreeView (multiple overloads)
        // - TryAddNode
        // - TryAddNodeForCollection
        // - IsPredefinedProperty
        // - ProcessTreeViewSelectionAsync
        // - ClearDetailPane
        // - DisplaySingleObject<T>
        // - DisplaySingleObjectHeader
        // - ItemListView_SelectionChanged
        // - DisplayObjectProperties
        // - LogLoadedDatabases (replaced with LogLoadedDatabasesForDebug)
        // --------------------------------------------------

    }
}
