# ACME Editor

ACME Editor is a Windows WinUI 3 application for viewing and editing Asheron's Call .dat files. The application provides a user-friendly interface for exploring and modifying game data.

## Features

- **Database Loading**: Open and load Asheron's Call .dat files (Portal and Cell databases)
- **TreeView Navigation**: Hierarchical exploration of database structure
- **Detail Viewing**: View details of selected items in a dedicated pane
- **Modern UI**: Built with WinUI 3 for a modern Windows look and feel
- **Split-View Interface**: Three-panel layout with navigation tree, item list, and detail view
- **Spell Editing**: View and modify spell properties with changes saved back to .dat files
- **Combat Table Viewing**: 
  - Dynamically lists `CombatTable` objects (range 0x30xxxx).
  - Renders `CombatTable` details, including a nested/collapsible view of `CombatManeuver`s.

## Technical Overview

- **Framework**: WinUI 3 / .NET 8.0 (Windows App SDK)
- **Architecture**: MVVM-inspired separation of concerns with managers and renderers
- **Backend**: Uses DatReaderWriter library for .dat file access

## Project Structure

- **ACME/**: Main application 
  - **Assets/**: Application icons and images
  - **Constants/**: Static values and enumerations
  - **Converters/**: Data conversion for UI binding
  - **Data/**: Data models and repositories
  - **Extractors/**: Logic for extracting data from files
  - **Helpers/**: Utility helper classes
  - **Managers/**: Business logic components
  - **Models/**: Domain models
  - **Renderers/**: UI rendering components
  - **Utils/**: Utility classes

- **DatReaderWriter/**: Library for reading and writing Asheron's Call .dat files

## Current Status

### Implemented
- File opening functionality
- Database structure navigation
- Basic viewing of database contents for many types
- Three-panel UI with resizable panels
- Detail rendering for numerous types (Spells, Textures, Palettes, Animations, etc.)
- Specific viewers/editors:
  - Spell editing and saving back to .dat files
  - Combat Table viewing with nested maneuver details
- UI fixes (Scrollbars, node handling)

### Known Issues / Limitations
- **Unimplemented Nodes**: Loading logic is missing for certain specific File IDs.
- **Performance**: Loading very large collections (like EnvCells) directly into the list view can be slow.
- **Editing**: Currently, only Spells are editable.

### Planned Features
- Editing capabilities for other data types:
  - **Portal Database**:
    - Skill/vital data (SkillTable, VitalTable)
    - Experience tables (ExperienceTable)
    - Spell components (SpellComponentTable)
    - Character generation data (CharGen)
    - Visual elements (Animations, Palettes, Surfaces, Textures)
    - Game systems (MotionTables, Environments, Regions)
    - UI elements (Keymaps, LanguageStrings)
  - **Cell Database**:
    - Landblocks
    - Landblock info
    - Environment cells
- Visual asset viewers (images, 3D models, animations)
- Sound playback for audio assets
- Search functionality
- Export/import capabilities
- Better visualization of game assets
- Help documentation

## Data Types Overview

### Portal Database
The Portal database (client_portal.dat) contains most game data including:

- **SpellTable**: Spells and magic system (currently editable)
- **SpellComponentTable**: Spell components data
- **SkillTable**: Character skills information
- **VitalTable**: Character vital statistics
- **ExperienceTable**: Experience progression data
- **CharGen**: Character generation options
- **Visual Assets**:
  - Animations, GfxObjs, Setups
  - Palettes, PaletteSets
  - Surfaces, SurfaceTextures, RenderSurfaces
  - Clothing, MaterialModifiers, MaterialInstances
  - ParticleEmitters
- **Game Systems**:
  - MotionTables (movement animations)
  - Environments, Regions, Scenes
  - Wave data
  - PhysicsScripts
- **UI Elements**:
  - Keymaps
  - LanguageStrings
  - ChatPoseTable

### Cell Database
The Cell database (client_cell_1.dat) contains world data:

- **LandBlocks**: Terrain data
- **LandBlockInfo**: Object placement and properties
- **EnvCells**: Environment cells (dungeons, interiors)

## Development

### Requirements
- Windows 10/11
- Visual Studio 2022 with the following components:
  - .NET Desktop Development
  - Universal Windows Platform development
  - Windows App SDK

### Building
1. Open the solution in Visual Studio 2022
2. Restore NuGet packages
3. Build the solution

## License

This project is licensed under the MIT License.

## Acknowledgments

- DatReaderWriter library contributors
- Asheron's Call community for documentation and resources 