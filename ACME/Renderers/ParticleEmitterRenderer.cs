using System;
using System.Collections.Generic;
using System.Diagnostics;
using DatReaderWriter.DBObjs;
using DatReaderWriter.Types;
using DatReaderWriter.Enums;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ACME.Renderers
{
    /// <summary>
    /// Renders details for ParticleEmitter objects.
    /// </summary>
    public class ParticleEmitterRenderer : IObjectRenderer
    {
        public void Render(Panel targetPanel, object data, Dictionary<string, object>? context)
        {
            if (data is not ParticleEmitter emitter)
            {
                Debug.WriteLine($"ParticleEmitterRenderer: Received data is not a ParticleEmitter object (Type: {data?.GetType().Name ?? "null"})");
                RendererHelpers.AddErrorMessageToPanel(targetPanel, "Invalid data type for ParticleEmitterRenderer.");
                return;
            }

            Debug.WriteLine($"--- ParticleEmitterRenderer.Render called for ID: 0x{emitter.Id:X8} ---");

            var propertiesPanel = new StackPanel() { Margin = new Thickness(0, 0, 0, 20) };

            // Display base properties
            RendererHelpers.AddSimplePropertyRow(propertiesPanel, "Id:", $"0x{emitter.Id:X8} ({emitter.Id})");
            RendererHelpers.AddSimplePropertyRow(propertiesPanel, "DBObjType:", emitter.DBObjType.ToString());
            
            RendererHelpers.AddSeparator(propertiesPanel);
            
            // Display ParticleEmitter specific properties
            RendererHelpers.AddSimplePropertyRow(propertiesPanel, "Unknown:", emitter.Unknown.ToString());
            RendererHelpers.AddSimplePropertyRow(propertiesPanel, "EmitterType:", emitter.EmitterType.ToString());
            RendererHelpers.AddSimplePropertyRow(propertiesPanel, "ParticleType:", emitter.ParticleType.ToString());
            RendererHelpers.AddSimplePropertyRow(propertiesPanel, "GfxObjId:", $"0x{emitter.GfxObjId:X8}");
            RendererHelpers.AddSimplePropertyRow(propertiesPanel, "HwGfxObjId:", $"0x{emitter.HwGfxObjId:X8}");
            RendererHelpers.AddSimplePropertyRow(propertiesPanel, "Birthrate:", emitter.Birthrate.ToString());
            RendererHelpers.AddSimplePropertyRow(propertiesPanel, "MaxParticles:", emitter.MaxParticles.ToString());
            RendererHelpers.AddSimplePropertyRow(propertiesPanel, "InitialParticles:", emitter.InitialParticles.ToString());
            RendererHelpers.AddSimplePropertyRow(propertiesPanel, "TotalParticles:", emitter.TotalParticles.ToString());
            RendererHelpers.AddSimplePropertyRow(propertiesPanel, "TotalSeconds:", emitter.TotalSeconds.ToString());
            RendererHelpers.AddSimplePropertyRow(propertiesPanel, "Lifespan:", emitter.Lifespan.ToString());
            RendererHelpers.AddSimplePropertyRow(propertiesPanel, "LifespanRand:", emitter.LifespanRand.ToString());
            RendererHelpers.AddSimplePropertyRow(propertiesPanel, "OffsetDir:", emitter.OffsetDir.ToString());
            RendererHelpers.AddSimplePropertyRow(propertiesPanel, "MinOffset:", emitter.MinOffset.ToString());
            RendererHelpers.AddSimplePropertyRow(propertiesPanel, "MaxOffset:", emitter.MaxOffset.ToString());
            RendererHelpers.AddSimplePropertyRow(propertiesPanel, "A:", emitter.A.ToString());
            RendererHelpers.AddSimplePropertyRow(propertiesPanel, "MinA:", emitter.MinA.ToString());
            RendererHelpers.AddSimplePropertyRow(propertiesPanel, "MaxA:", emitter.MaxA.ToString());
            RendererHelpers.AddSimplePropertyRow(propertiesPanel, "B:", emitter.B.ToString());
            RendererHelpers.AddSimplePropertyRow(propertiesPanel, "MinB:", emitter.MinB.ToString());
            RendererHelpers.AddSimplePropertyRow(propertiesPanel, "MaxB:", emitter.MaxB.ToString());
            RendererHelpers.AddSimplePropertyRow(propertiesPanel, "C:", emitter.C.ToString());
            RendererHelpers.AddSimplePropertyRow(propertiesPanel, "MinC:", emitter.MinC.ToString());
            RendererHelpers.AddSimplePropertyRow(propertiesPanel, "MaxC:", emitter.MaxC.ToString());
            RendererHelpers.AddSimplePropertyRow(propertiesPanel, "StartScale:", emitter.StartScale.ToString());
            RendererHelpers.AddSimplePropertyRow(propertiesPanel, "FinalScale:", emitter.FinalScale.ToString());
            RendererHelpers.AddSimplePropertyRow(propertiesPanel, "ScaleRand:", emitter.ScaleRand.ToString());
            RendererHelpers.AddSimplePropertyRow(propertiesPanel, "StartTrans:", emitter.StartTrans.ToString());
            RendererHelpers.AddSimplePropertyRow(propertiesPanel, "FinalTrans:", emitter.FinalTrans.ToString());
            RendererHelpers.AddSimplePropertyRow(propertiesPanel, "TransRand:", emitter.TransRand.ToString());
            RendererHelpers.AddSimplePropertyRow(propertiesPanel, "IsParentLocal:", emitter.IsParentLocal.ToString());

            targetPanel.Children.Add(propertiesPanel);
        }
    }
} 