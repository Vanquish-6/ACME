using ACME.Constants;
using ACME.Models;
using System;
using System.Linq;

namespace ACME.Helpers
{
    /// <summary>
    /// Static helper methods for parsing DAT file identifiers and related logic.
    /// </summary>
    public static class DatParsingHelpers
    {
        /// <summary>
        /// Checks if a file ID is likely the start of a range-based table.
        /// </summary>
        internal static bool IsRangeTableId(uint fileId)
        {
            // Add known range start IDs here based on corrected names
            return fileId == DatFileIds.ClothingTableId || fileId == DatFileIds.GfxObjId || // Use GfxObjId
                   fileId == DatFileIds.MotionTableId || fileId == DatFileIds.PaletteId || // Use PaletteId
                   fileId == DatFileIds.ParticleEmitterTableId || fileId == DatFileIds.AnimationHookOpId || // Use AnimationHookOpId
                   // fileId == DatFileIds.ChatEmoteTableId || // REMOVED: No dedicated ChatEmoteTableId
                   // fileId == DatFileIds.DungeonTableId || // REMOVED: No DungeonTableId
                   fileId == DatFileIds.GeneratorProfileId || fileId == DatFileIds.MaterialId || // Use GeneratorProfileId, MaterialId
                   fileId == DatFileIds.QualityFilterId || fileId == DatFileIds.RenderMaterialId || // Use QualityFilterId, RenderMaterialId
                   fileId == DatFileIds.SoundTableId || fileId == DatFileIds.SurfaceId || // Use SurfaceId
                   fileId == DatFileIds.SurfaceTextureId || fileId == DatFileIds.UILayoutId || // Use SurfaceTextureId, UILayoutId
                   fileId == DatFileIds.AnimationId || fileId == DatFileIds.LanguageStringDataId || // Use AnimationId, LanguageStringDataId
                   fileId == DatFileIds.StringStateId || fileId == DatFileIds.RenderSurfaceId; // Use StringStateId, RenderSurfaceId
        }

        /// <summary>
        /// Extracts the Database ID string from various identifier types.
        /// </summary>
        internal static string? GetDatabaseIdFromIdentifier(object? identifier)
        {
            return identifier switch
            {
                NodeIdentifier nodeId => nodeId.DatabaseId,
                string strId when strId.Contains('_') => strId.Split(new[] { '_' }, 2).LastOrDefault(),
                string strId => strId,
                _ => null
            };
        }
    }
} 