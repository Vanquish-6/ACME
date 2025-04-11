using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using DatReaderWriter;
using DatReaderWriter.Options;
using ACME.Constants;
using Microsoft.UI.Xaml;
using WinRT.Interop;
using DatReaderWriter.Lib.IO.BlockAllocators;

namespace ACME.Managers
{
    /// <summary>
    /// Manager class for handling DAT database operations
    /// </summary>
    public class DatabaseManager
    {
        /// <summary>
        /// Collection of loaded databases
        /// </summary>
        private List<DatabaseInfo> _loadedDatabases = new();
        
        /// <summary>
        /// The currently active database
        /// </summary>
        public DatDatabase? CurrentDatabase { get; private set; }
        
        /// <summary>
        /// The type of the currently active database
        /// </summary>
        public DatabaseType CurrentDatabaseType { get; private set; } = DatabaseType.None;
        
        /// <summary>
        /// Get the list of loaded databases
        /// </summary>
        public IReadOnlyList<DatabaseInfo> LoadedDatabases => _loadedDatabases.AsReadOnly();
        
        /// <summary>
        /// Event fired when databases are added or removed
        /// </summary>
        public event EventHandler<DatabasesChangedEventArgs>? DatabasesChanged;
        
        /// <summary>
        /// Attempts to load a database file
        /// </summary>
        /// <param name="file">The StorageFile representing the database</param>
        /// <param name="hwnd">The window handle for initialization</param>
        /// <returns>Tuple containing success flag and error message if any</returns>
        public async Task<(bool Success, string ErrorMessage)> TryLoadDatabaseAsync(StorageFile file, IntPtr hwnd)
        {
            string errorMessage = string.Empty;
            DatDatabase? newDb = null;
            DatabaseType dbType = DatabaseType.None;

            try
            {
                // First, detect preferred database type based on filename convention
                bool isLikelyCell = file.Name.ToLower().Contains("cell");
                DatabaseType primaryAttemptType = isLikelyCell ? DatabaseType.Cell : DatabaseType.Portal;
                DatabaseType secondaryAttemptType = isLikelyCell ? DatabaseType.Portal : DatabaseType.Cell;

                // Attempt 1: Try opening with the preferred type
                newDb = OpenDatabase(file.Path, primaryAttemptType);
                if (newDb != null)
                {
                    dbType = primaryAttemptType;
                    Debug.WriteLine($"Successfully loaded {file.Name} as {dbType} on first attempt.");
                }
                else
                {
                    // Attempt 2: Try opening with the alternate type
                    Debug.WriteLine($"First attempt to load {file.Name} as {primaryAttemptType} failed. Trying {secondaryAttemptType}.");
                    newDb = OpenDatabase(file.Path, secondaryAttemptType);

                    if (newDb != null)
                    {
                        dbType = secondaryAttemptType;
                        Debug.WriteLine($"Successfully loaded {file.Name} as {dbType} on second attempt.");
                    }
                    else
                    {
                        // Both attempts failed
                        errorMessage = $"Failed to load {file.Name} as either {primaryAttemptType} or {secondaryAttemptType} using StreamBlockAllocator.";
                        Debug.WriteLine(errorMessage);
                        return (false, errorMessage); // newDb is null, allocator disposal handled within OpenDatabase
                    }
                }

                // If we reach here, newDb is not null and dbType is set
                var dbInfo = new DatabaseInfo(newDb, dbType, file.Name, file.Path);
                _loadedDatabases.Add(dbInfo);

                // Set as current
                CurrentDatabase = newDb;
                CurrentDatabaseType = dbType;

                // Notify listeners
                DatabasesChanged?.Invoke(this, new DatabasesChangedEventArgs(_loadedDatabases.AsReadOnly(), dbInfo));

                return (true, string.Empty);
            }
            catch (Exception ex) // Catch unexpected errors during the loading process
            {
                errorMessage = $"Unexpected error loading database {file.Name}: {ex.Message}";
                Debug.WriteLine(errorMessage);
                // Ensure disposal if a database was successfully opened but an error occurred before it was fully managed
                (newDb as IDisposable)?.Dispose(); 
                return (false, errorMessage);
            }
        }
        
        /// <summary>
        /// Helper method to open a database of a specific type using StreamBlockAllocator.
        /// </summary>
        /// <param name="filePath">Path to the database file.</param>
        /// <param name="dbTypeToTry">The DatabaseType (Cell or Portal) to attempt opening.</param>
        /// <returns>A DatDatabase instance if successful, otherwise null.</returns>
        private static DatDatabase? OpenDatabase(string filePath, DatabaseType dbTypeToTry)
        {
            Action<DatDatabaseOptions> optionsAction = opt => {
                opt.FilePath = filePath;
                opt.AccessType = DatAccessType.ReadWrite; // Always use ReadWrite
            };
            var dbOptions = new DatDatabaseOptions();
            optionsAction(dbOptions); 
            StreamBlockAllocator? streamAllocator = null; 

            try
            {
                // Explicitly create StreamBlockAllocator
                streamAllocator = new StreamBlockAllocator(dbOptions); 

                DatDatabase newDb = dbTypeToTry switch
                {
                    DatabaseType.Cell => new CellDatabase(optionsAction, streamAllocator),
                    DatabaseType.Portal => new PortalDatabase(optionsAction, streamAllocator),
                    _ => throw new ArgumentOutOfRangeException(nameof(dbTypeToTry), $"Unsupported database type: {dbTypeToTry}")
                };
                
                // Success, ownership of streamAllocator is passed to newDb
                return newDb; 
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to open {Path.GetFileName(filePath)} as {dbTypeToTry} using StreamBlockAllocator: {ex.Message}");
                // If allocator was created but the database constructor failed, dispose the allocator.
                streamAllocator?.Dispose(); 
                return null; // Signal failure
            }
        }
        
        /// <summary>
        /// Shows the file picker to select and load a database file
        /// </summary>
        /// <param name="window">The parent window</param>
        /// <returns>A tuple with success flag and an error message if failed</returns>
        public async Task<(bool Success, string? ErrorMessage)> PickAndLoadDatabaseAsync(Window window)
        {
            var fileOpenPicker = new FileOpenPicker();
            var hwnd = WindowNative.GetWindowHandle(window);
            InitializeWithWindow.Initialize(fileOpenPicker, hwnd);

            fileOpenPicker.FileTypeFilter.Add(".dat");
            fileOpenPicker.ViewMode = PickerViewMode.List;
            fileOpenPicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;

            StorageFile file = await fileOpenPicker.PickSingleFileAsync();
            
            if (file == null)
            {
                // User cancelled
                return (false, null);
            }
            
            var result = await TryLoadDatabaseAsync(file, hwnd);
            return (result.Success, result.Success ? null : result.ErrorMessage);
        }
        
        /// <summary>
        /// Close and dispose a specific database
        /// </summary>
        /// <param name="databaseId">The ID of the database to close</param>
        /// <returns>True if found and closed, false otherwise</returns>
        public bool CloseDatabase(string databaseId)
        {
            var db = _loadedDatabases.FirstOrDefault(db => GetDatabaseId(db) == databaseId);
            
            if (db != null)
            {
                db.Database.Dispose();
                _loadedDatabases.Remove(db);
                
                // If we removed the current database, update current
                if (CurrentDatabase == db.Database)
                {
                    CurrentDatabase = _loadedDatabases.FirstOrDefault()?.Database;
                    CurrentDatabaseType = _loadedDatabases.FirstOrDefault()?.Type ?? DatabaseType.None;
                }
                
                // Notify listeners
                DatabasesChanged?.Invoke(this, new DatabasesChangedEventArgs(_loadedDatabases.AsReadOnly(), null));
                
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// Close all open databases
        /// </summary>
        public void CloseAllDatabases()
        {
            foreach (var db in _loadedDatabases)
            {
                db.Database?.Dispose();
            }
            
            _loadedDatabases.Clear();
            CurrentDatabase = null;
            CurrentDatabaseType = DatabaseType.None;
            
            // Notify listeners
            DatabasesChanged?.Invoke(this, new DatabasesChangedEventArgs(_loadedDatabases.AsReadOnly(), null));
        }
        
        /// <summary>
        /// Find a database by its ID
        /// </summary>
        /// <param name="databaseId">The database ID to look for</param>
        /// <returns>The database info if found, null otherwise</returns>
        public DatabaseInfo? FindDatabase(string databaseId)
        {
            return _loadedDatabases.FirstOrDefault(db => GetDatabaseId(db) == databaseId);
        }
        
        /// <summary>
        /// Sets a specific database as the current active database
        /// </summary>
        /// <param name="databaseId">The ID of the database to set as current</param>
        /// <returns>True if successful, false if database not found</returns>
        public bool SetCurrentDatabase(string databaseId)
        {
            var db = FindDatabase(databaseId);
            
            if (db != null)
            {
                CurrentDatabase = db.Database;
                CurrentDatabaseType = db.Type;
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// Get a unique database ID for a database info object
        /// </summary>
        public string GetDatabaseId(DatabaseInfo dbInfo)
        {
            // Generate a unique ID based on the database file name and index
            // This is used to identify the database in the TreeView
            if (dbInfo == null) return string.Empty;
            
            int index = _loadedDatabases.IndexOf(dbInfo);
            if (index >= 0)
            {
                string accessType = dbInfo.CanWrite ? "RW" : "RO"; // RW = ReadWrite, RO = ReadOnly
                return $"{dbInfo.FileName}_{index}_{accessType}";
            }
            
            return $"{dbInfo.FileName}_unknown";
        }
        
        /// <summary>
        /// Find a database by its ID
        /// </summary>
        public DatabaseInfo? FindDatabaseById(string databaseId)
        {
            if (string.IsNullOrEmpty(databaseId)) return null;
            
            return _loadedDatabases.FirstOrDefault(db => GetDatabaseId(db) == databaseId);
        }
        
        /// <summary>
        /// Log information about loaded databases to the debug output
        /// </summary>
        public void LogLoadedDatabases()
        {
            Debug.WriteLine($"==== LOADED DATABASES ({_loadedDatabases.Count}) ====");
            
            if (_loadedDatabases.Count == 0)
            {
                Debug.WriteLine("No databases loaded");
                return;
            }
            
            for (int i = 0; i < _loadedDatabases.Count; i++)
            {
                var db = _loadedDatabases[i];
                string dbId = $"DB_{i}_{db.FileName}";
                Debug.WriteLine($"Database {i}: {db.FileName}, Type: {db.Type}, ID: {dbId}");
            }
            
            Debug.WriteLine($"Current database: {(CurrentDatabase != null ? "Set" : "Not set")}, Type: {CurrentDatabaseType}");
        }
        
        /// <summary>
        /// Determines if the currently active database has write access
        /// </summary>
        /// <returns>True if the database can be written to, false otherwise</returns>
        public bool CanWriteToCurrentDatabase()
        {
            if (CurrentDatabase?.BlockAllocator != null)
            {
                return CurrentDatabase.BlockAllocator.CanWrite;
            }
            return false;
        }
        
        /// <summary>
        /// Gets the access type of the currently active database
        /// </summary>
        /// <returns>The DatAccessType (Read or ReadWrite)</returns>
        public DatAccessType GetCurrentDatabaseAccessType()
        {
            return CanWriteToCurrentDatabase() ? DatAccessType.ReadWrite : DatAccessType.Read;
        }
    }
    
    /// <summary>
    /// Event arguments for database changes
    /// </summary>
    public class DatabasesChangedEventArgs : EventArgs
    {
        /// <summary>
        /// All currently loaded databases
        /// </summary>
        public IReadOnlyList<DatabaseInfo> LoadedDatabases { get; }
        
        /// <summary>
        /// The database that was just added, or null if databases were removed
        /// </summary>
        public DatabaseInfo? AddedDatabase { get; }
        
        public DatabasesChangedEventArgs(IReadOnlyList<DatabaseInfo> databases, DatabaseInfo? addedDatabase)
        {
            LoadedDatabases = databases;
            AddedDatabase = addedDatabase;
        }
    }
    
    /// <summary>
    /// Information about a loaded database
    /// </summary>
    public class DatabaseInfo
    {
        /// <summary>
        /// The database instance
        /// </summary>
        public DatDatabase Database { get; }
        
        /// <summary>
        /// The type of database
        /// </summary>
        public DatabaseType Type { get; }
        
        /// <summary>
        /// The filename of the database
        /// </summary>
        public string FileName { get; }
        
        /// <summary>
        /// The file path of the database
        /// </summary>
        public string FilePath { get; }
        
        /// <summary>
        /// Whether the database has write access
        /// </summary>
        public bool CanWrite => Database?.BlockAllocator?.CanWrite ?? false;
        
        public DatabaseInfo(DatDatabase database, DatabaseType type, string fileName, string filePath)
        {
            Database = database;
            Type = type;
            FileName = fileName;
            FilePath = filePath;
        }
    }
} 