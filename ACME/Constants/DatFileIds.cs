using System;

namespace ACME.Constants
{
    /// <summary>
    /// Constants for known File IDs or Range Starts for DAT files
    /// </summary>
    public static class DatFileIds
    {
        // Single Files / Exact IDs (from dats.xml <type> first/last)
        public const uint WeenieDefaultsId = 0xE000001;
        public const uint CharGenId = 0xE000002; // Contains StartingAreas, HeritageGroups, etc.
        public const uint CombatTableId = 0xE000003;
        // public const uint VitalTableId = 0xE000003; // REMOVED: No VitalTable type; Vitals are in ExperienceTable. CombatTable uses this ID.
        public const uint SkillTableId = 0xE000004;
        public const uint ChatPoseTableId = 0x0E000007; // Contains ChatPoses, ChatEmotes
        public const uint SpellTableId = 0x0E00000E;
        public const uint SpellComponentsTableId = 0x0E00000F;
        public const uint ExperienceTableId = 0xE000018;
        public const uint TabooTableId = 0xE000019; // Added from dats.xml
        public const uint GameEventTableId = 0xE00001A;
        // public const uint BadDataTableId = 0x0E00001A; // REMOVED: Incorrect, GameEventTable uses this ID.

        // Range Starts / Collections (from dats.xml <type> first)
        public const uint GfxObjId = 0x01000000; // GfxObj range start
        public const uint SetupId = 0x02000000; // Setup range start (Added)
        public const uint AnimationId = 0x03000000; // Animation range start
        public const uint AnimationHookOpId = 0x1D000000; // AnimationHookOp range start (Corrected ID, was AnimationHookTableId 0x04000000)
        public const uint SurfaceTextureId = 0x05000000; // SurfaceTexture range start (Renamed from TextureTableId)
        public const uint RenderSurfaceId = 0x06000000; // RenderSurface range start (Added)
        public const uint SurfaceId = 0x08000000; // Surface range start (Renamed from SurfaceTableId)
        public const uint MotionTableId = 0x09000000; // MotionTable range start
        public const uint SoundTableId = 0x0A000000; // SoundTable range start
        public const uint SoundResourceId = 0x0D000000; // SoundResource range start (Added)
        public const uint PaletteId = 0x04000000; // Palette range start (Corrected ID, was 0x0F000000)
        public const uint PaletteSetId = 0x0F000000; // PaletteSet range start (Added)
        public const uint ClothingTableId = 0x10000000; // ClothingTable range start
        public const uint DegradeInfoId = 0x11000000; // DegradeInfo range start (Added from dats.xml)
        public const uint SceneId = 0x12000000;       // Scene range start (Added from dats.xml)
        public const uint RegionId = 0x13000000; // Region range start (Added)
        public const uint KeymapId = 0x14000000; // Keymap range start (Added)
        public const uint RenderTextureId = 0x15000000; // RenderTexture range start (Added)
        public const uint QualityFilterId = 0x16000000; // QualityFilter range start (Renamed from QualityFilterTableId)
        public const uint RenderMaterialId = 0x1A000000; // RenderMaterial range start (Corrected ID from 0x16000000 based on dats.xml)
        public const uint FontId = 0x17000000; // Font range start (Added)
        public const uint MaterialId = 0x18000000; // Material range start (Renamed from MaterialTableId)
        public const uint PhysicsScriptTableId = 0x19000000; // PhysicsScriptTable range start (Added)
        public const uint ParticleEmitterTableId = 0x32000000; // ParticleEmitterTable range start
        public const uint GeneratorProfileId = 0x24000000; // GeneratorProfile range start (Renamed from GeneratorTableId)

        // Local Dat specific types (from dats.xml <dat name="local">)
        public const uint UILayoutId = 0x21000000; // UILayout range start (Corrected ID, was 0x26000000, Renamed from UILayoutTableId)
        public const uint StringTableId = 0x23000000; // StringTable range start (Added, Note: LanguageStringData uses this range in portal.dat)
        public const uint FontLocalId = 0x40001000; // FontLocal range start (Added)
        public const uint StringStateId = 0x41000000; // StringState range start (Corrected ID, was 0x30000000, Renamed from StringStateTableId)

        // Portal Dat specific LanguageStrings (overlaps with local StringTable range)
        public const uint LanguageStringDataId = 0x23000000; // LanguageStringData range start (Corrected ID, was LanguageTableId 0x31000000)

        // Cell Data - Use String Tags as these are direct properties in CellDatabase
        public const string EnvCellsCollectionTag = "EnvCells";
        public const string LandBlockInfosCollectionTag = "LandBlockInfos";
        public const string LandBlocksCollectionTag = "LandBlocks";
    }
} 