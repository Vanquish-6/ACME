using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI;
using Windows.UI.Text;
using DatReaderWriter.DBObjs;
using DatReaderWriter.Types;
using DatReaderWriter.Enums;
using ACME.Utils;
using System.Reflection;

namespace ACME.Renderers
{
    public class AnimationRenderer : IObjectRenderer
    {
        public void Render(Panel targetPanel, object data, Dictionary<string, object>? context)
        {
            if (data is not Animation anim)
            {
                RendererHelpers.AddErrorMessageToPanel(targetPanel, "Invalid data type. Expected Animation.");
                return;
            }

            var mainPanel = new StackPanel() { Margin = (Thickness)Application.Current.Resources["RendererMainPanelMargin"] };

            // --- Basic Properties ---
            RendererHelpers.AddSimplePropertyRow(mainPanel, "Flags:", anim.Flags.ToString());
            RendererHelpers.AddSimplePropertyRow(mainPanel, "Num Parts:", anim.NumParts.ToString());
            RendererHelpers.AddSeparator(mainPanel);

            // --- Position Frames (if present) ---
            if (anim.Flags.HasFlag(AnimationFlags.PosFrames) && anim.PosFrames.Count > 0)
            {
                var posFramesExpander = new Expander
                {
                    Header = $"Position Frames ({anim.PosFrames.Count})",
                    Margin = (Thickness)Application.Current.Resources["RendererExpanderMargin"],
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };
                var posFramesPanel = new StackPanel { Margin = (Thickness)Application.Current.Resources["RendererNestedPanelMargin"] };
                RenderFrames(posFramesPanel, anim.PosFrames, context);
                posFramesExpander.Content = posFramesPanel;
                mainPanel.Children.Add(posFramesExpander);
                RendererHelpers.AddSeparator(mainPanel);
            }

            // --- Part Frames ---
            var partFramesExpander = new Expander
            {
                Header = $"Part Frames ({anim.PartFrames.Count})",
                Margin = (Thickness)Application.Current.Resources["RendererExpanderMargin"],
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            var partFramesPanel = new StackPanel { Margin = (Thickness)Application.Current.Resources["RendererNestedPanelMargin"] };
            RenderPartFrames(partFramesPanel, anim.PartFrames, context);
            partFramesExpander.Content = partFramesPanel;
            mainPanel.Children.Add(partFramesExpander);

            targetPanel.Children.Add(mainPanel);
        }

        private void RenderFrames(Panel parentPanel, List<DatReaderWriter.Types.Frame> frames, Dictionary<string, object>? context)
        {
            if (frames.Count == 0)
            {
                RendererHelpers.AddInfoMessageToPanel(parentPanel, "No frames defined.", Colors.Gray);
                return;
            }

            for (int i = 0; i < frames.Count; i++)
            {
                var frame = frames[i];
                var frameExpander = new Expander
                {
                    Header = $"Frame {i}",
                    Margin = (Thickness)Application.Current.Resources["RendererExpanderMargin"]
                };
                var framePanel = new StackPanel { Margin = (Thickness)Application.Current.Resources["RendererNestedPanelMargin"] };
                // RendererHelpers.RenderObjectProperties(framePanel, frame, context); // REMOVED: Avoid deep recursion
                // Directly render Frame properties
                if (frame != null)
                {
                    RendererHelpers.AddSimplePropertyRow(framePanel, "Origin:", $"X={frame.Origin.X:F2}, Y={frame.Origin.Y:F2}, Z={frame.Origin.Z:F2}");
                    RendererHelpers.AddSimplePropertyRow(framePanel, "Orientation:", $"W={frame.Orientation.W:F4}, X={frame.Orientation.X:F4}, Y={frame.Orientation.Y:F4}, Z={frame.Orientation.Z:F4}");
                }
                else
                {
                    RendererHelpers.AddInfoMessageToPanel(framePanel, "(null)", Colors.Gray);
                }
                frameExpander.Content = framePanel;
                parentPanel.Children.Add(frameExpander);
            }
        }

        private void RenderPartFrames(Panel parentPanel, List<AnimationFrame> partFrames, Dictionary<string, object>? context)
        {
            if (partFrames.Count == 0)
            {
                RendererHelpers.AddInfoMessageToPanel(parentPanel, "No part frames defined.", Colors.Gray);
                return;
            }

            for (int i = 0; i < partFrames.Count; i++)
            {
                var partFrame = partFrames[i];
                var partFrameExpander = new Expander
                {
                    Header = $"Part Frame {i}",
                    Margin = (Thickness)Application.Current.Resources["RendererExpanderMargin"]
                };
                var partFramePanel = new StackPanel { Margin = (Thickness)Application.Current.Resources["RendererNestedPanelMargin"] };

                // Frames
                var framesExpander = new Expander
                {
                    Header = $"Frames ({partFrame.Frames.Count})",
                    Margin = (Thickness)Application.Current.Resources["RendererExpanderMargin"]
                };
                var framesPanel = new StackPanel { Margin = (Thickness)Application.Current.Resources["RendererNestedPanelMargin"] };
                RenderFrames(framesPanel, partFrame.Frames, context);
                framesExpander.Content = framesPanel;
                partFramePanel.Children.Add(framesExpander);

                // Hooks
                var hooksExpander = new Expander
                {
                    Header = $"Hooks ({partFrame.Hooks.Count})",
                    Margin = (Thickness)Application.Current.Resources["RendererExpanderMargin"]
                };
                var hooksPanel = new StackPanel { Margin = (Thickness)Application.Current.Resources["RendererNestedPanelMargin"] };
                RenderHooks(hooksPanel, partFrame.Hooks, context);
                hooksExpander.Content = hooksPanel;
                partFramePanel.Children.Add(hooksExpander);

                partFrameExpander.Content = partFramePanel;
                parentPanel.Children.Add(partFrameExpander);
            }
        }

        private void RenderHooks(Panel parentPanel, List<AnimationHook> hooks, Dictionary<string, object>? context)
        {
            if (hooks.Count == 0)
            {
                RendererHelpers.AddInfoMessageToPanel(parentPanel, "No hooks defined.", Colors.Gray);
                return;
            }

            for (int i = 0; i < hooks.Count; i++)
            {
                var hook = hooks[i];
                // Display hook type and index directly, no expander to avoid recursion
                var hookText = $"[{i}] {hook?.GetType().Name ?? "Unknown Hook"}";

                // Attempt to get Direction property if it exists (common for hooks)
                try {
                    var dirProp = hook?.GetType().GetProperty("Direction");
                    if (dirProp != null && dirProp.PropertyType == typeof(AnimationHookDir)) {
                        var dirValue = (AnimationHookDir?)dirProp.GetValue(hook);
                        hookText += $" (Direction: {dirValue?.ToString() ?? "N/A"})";
                    }
                } catch { /* Ignore reflection errors */ }
                
                parentPanel.Children.Add(new TextBlock {
                   Text = hookText,
                   Margin = (Thickness)Application.Current.Resources["RendererHookTextMargin"]
                });
            }
        }
    }
} 