using System;

namespace ACME.Constants
{
    /// <summary>
    /// Constants for known File IDs or Range Starts for DAT files
    /// </summary>
    public static class DatFileIds
    {
        // Single Files
        public const uint SpellTableId = 0x0E00000E;
        public const uint SkillTableId = 0xE000004;
        public const uint CharGenId = 0xE000002;
        public const uint CombatTableId = 0xE000003;
        public const uint ExperienceTableId = 0xE000018;
        public const uint GameEventTableId = 0xE00001A;
        public const uint SpellComponentsTableId = 0x0E00000F;
        public const uint StringStateTableId = 0x30000000; // Range Start?
        public const uint VitalTableId = 0xE000003; // Same as CombatTable? Check ID
        public const uint WeenieDefaultsId = 0xE000001;
        public const uint ChatPoseTableId = 0x0E000007;
        public const uint BadDataTableId = 0x0E00001A;

        // Range Starts / Collections (Using likely start IDs)
        public const uint ClothingTableId = 0x10000000;
        public const uint GfxObjTableId = 0x01000000;
        public const uint LanguageTableId = 0x31000000;
        public const uint MotionTableId = 0x09000000;
        public const uint PaletteTableId = 0x0F000000;
        public const uint ParticleEmitterTableId = 0x32000000;
        public const uint AnimationHookTableId = 0x04000000; // Guessed start ID
        public const uint ChatEmoteTableId = 0x20000000; // Guessed start ID
        public const uint DungeonTableId = 0x0C000000; // Guessed start ID
        public const uint FileMapId = 0xDA7FF000; // Placeholder ID?
        public const uint GeneratorTableId = 0x24000000; // Guessed start ID
        public const uint MaterialTableId = 0x18000000; // Guessed start ID
        public const uint QualityFilterTableId = 0x16000000; // Guessed start ID
        public const uint RenderMaterialTableId = 0x1A000000; // Guessed start ID
        public const uint SoundTableId = 0x0A000000; // Guessed start ID
        public const uint SurfaceTableId = 0x08000000; // Guessed start ID
        public const uint TextureTableId = 0x05000000; // Guessed start ID
        public const uint UILayoutTableId = 0x26000000; // Guessed start ID
        public const uint AnimationTableId = 0x03000000; // Added

        // Cell Data - Use String Tags as these are direct properties
        public const string EnvCellsCollectionTag = "EnvCells";
        public const string LandBlockInfosCollectionTag = "LandBlockInfos";
        public const string LandBlocksCollectionTag = "LandBlocks";
    }
} 