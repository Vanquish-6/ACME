using DatReaderWriter.DBObjs;
using Microsoft.UI.Xaml.Controls; // Renamed Image to UIImage
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices.WindowsRuntime;
using DatReaderWriter.Enums;
using DatReaderWriter; // For DatDatabase
using DatReaderWriter.Types; // For ColorARGB
using SixLabors.ImageSharp; // Main ImageSharp
using SixLabors.ImageSharp.Formats; // For Configuration, ImageFormatManager
using SixLabors.ImageSharp.PixelFormats; // For Rgba32, etc.
using SixLabors.ImageSharp.Processing; // For image operations if needed
using BCnEncoder.Decoder; // Added for BCnEncoder
using BCnEncoder.Shared;  // Added for BCnEncoder
using Microsoft.UI;
using Windows.Storage.Streams;
using UIImage = Microsoft.UI.Xaml.Controls.Image; // Alias for WinUI Image
using System.IO; // For MemoryStream (still needed for WriteableBitmap conversion)

// Alias ImageSharp Rgba32 to avoid conflict with BCnEncoder's ColorRgba32
using ImageSharpRgba32 = SixLabors.ImageSharp.PixelFormats.Rgba32;

namespace ACME.Renderers
{
    public class RenderSurfaceRenderer : IObjectRenderer
    {
        public void Render(Panel panel, object obj, Dictionary<string, object>? context)
        {
            if (obj is not RenderSurface renderSurface)
            {
                // Handle error or invalid type
                RendererHelpers.AddErrorMessageToPanel(panel, "Invalid object type passed to RenderSurfaceRenderer.");
                return;
            }

            panel.Children.Clear(); // Clear previous content

            // Basic Info
            RendererHelpers.AddSimplePropertyRow(panel, "ID", $"0x{renderSurface.Id:X8}");
            RendererHelpers.AddSimplePropertyRow(panel, "Width", renderSurface.Width.ToString());
            RendererHelpers.AddSimplePropertyRow(panel, "Height", renderSurface.Height.ToString());
            RendererHelpers.AddSimplePropertyRow(panel, "Format", renderSurface.Format.ToString());
            bool isPaletted = renderSurface.Format == PixelFormat.PFID_INDEX16 || renderSurface.Format == PixelFormat.PFID_P8;
            if (isPaletted)
            {
                RendererHelpers.AddSimplePropertyRow(panel, "Default Palette", $"0x{renderSurface.DefaultPaletteId:X8}");
            }
            RendererHelpers.AddSeparator(panel);
            RendererHelpers.AddSectionHeader(panel, "Texture Preview");

            // Use full type name SixLabors.ImageSharp.Image<Rgba32>
            SixLabors.ImageSharp.Image<Rgba32>? image = null;
            string? errorMessage = null;

            try
            {
                int width = renderSurface.Width;
                int height = renderSurface.Height;
                byte[] sourceData = renderSurface.SourceData;

                if (width <= 0 || height <= 0 || sourceData == null || sourceData.Length == 0)
                {
                    errorMessage = "Invalid texture dimensions or missing source data.";
                }
                else
                {
                    switch (renderSurface.Format)
                    {
                        case PixelFormat.PFID_A8R8G8B8: // 32-bit ARGB
                            if (sourceData.Length >= width * height * 4)
                                // Use full type name
                                image = SixLabors.ImageSharp.Image.LoadPixelData<Bgra32>(sourceData, width, height).CloneAs<Rgba32>();
                            else
                                errorMessage = "Source data too small for A8R8G8B8 format.";
                            break;

                        case PixelFormat.PFID_R8G8B8: // 24-bit RGB
                             if (sourceData.Length >= width * height * 3)
                                // Use full type name
                                // image = SixLabors.ImageSharp.Image.LoadPixelData<Rgb24>(sourceData, width, height).CloneAs<Rgba32>(); // REMOVED Redundant line
                                // Load as Bgr24 assuming source is BGR, then convert to Rgba32
                                image = SixLabors.ImageSharp.Image.LoadPixelData<Bgr24>(sourceData, width, height).CloneAs<Rgba32>();
                            else
                                errorMessage = "Source data too small for R8G8B8 format.";
                            break;

                        // --- Handle Paletted Format --- 
                        case PixelFormat.PFID_INDEX16: // 16-bit index
                        case PixelFormat.PFID_P8:      // 8-bit index
                            if (!isPaletted) break; // Should not happen, but safety check

                            // 1. Get Database from context
                            DatDatabase? db = null;
                            if (context != null && context.TryGetValue("Database", out var dbObject) && dbObject is DatDatabase database)
                            {
                                db = database;
                            }
                            if (db == null)
                            {
                                errorMessage = "Database context not found. Cannot load palette for paletted texture.";
                                break;
                            }

                            // 2. Load Palette
                            if (!db.TryReadFile<Palette>(renderSurface.DefaultPaletteId, out var palette) || palette == null)
                            {
                                errorMessage = $"Could not load required Palette: 0x{renderSurface.DefaultPaletteId:X8}";
                                break;
                            }

                            // 3. Create image and apply palette
                            int bytesPerPixel = (renderSurface.Format == PixelFormat.PFID_INDEX16) ? 2 : 1;
                            if (sourceData.Length < width * height * bytesPerPixel)
                            {
                                errorMessage = "Source data too small for paletted format.";
                                break;
                            }

                            // Use full type name and assert non-null for indexing
                            image = new SixLabors.ImageSharp.Image<Rgba32>(width, height);
                            for (int y = 0; y < height; y++)
                            {
                                for (int x = 0; x < width; x++)
                                {
                                    int indexOffset = (y * width + x) * bytesPerPixel;
                                    int paletteIndex = 0;
                                    if (renderSurface.Format == PixelFormat.PFID_INDEX16)
                                    {
                                        paletteIndex = BitConverter.ToUInt16(sourceData, indexOffset); // Assuming Little Endian
                                    }
                                    else // PFID_P8
                                    {
                                        paletteIndex = sourceData[indexOffset];
                                    }

                                    if (paletteIndex < palette.Colors.Count)
                                    {
                                        // Use separate R, G, B, A properties from ColorARGB
                                        ColorARGB colorArgbObj = palette.Colors[paletteIndex];
                                        image[x, y] = new Rgba32(colorArgbObj.Red, colorArgbObj.Green, colorArgbObj.Blue, colorArgbObj.Alpha); // Use indexer
                                    }
                                    else
                                    {
                                        image[x, y] = new Rgba32(255, 0, 255, 255); // Error color (Magenta)
                                    }
                                }
                            }
                            break;
                            // --- End Paletted Format --- 

                        case PixelFormat.PFID_DXT1: // BC1
                            try
                            {
                                var decoder = new BcDecoder();
                                // Decode the BC1/DXT1 data
                                ColorRgba32[] decodedPixelStructs = decoder.DecodeRaw(sourceData, width, height, CompressionFormat.Bc1);

                                if (decodedPixelStructs != null && decodedPixelStructs.Length == width * height)
                                {
                                    // Convert BCnEncoder.ColorRgba32[] to SixLabors.ImageSharp.PixelFormats.Rgba32[]
                                    ImageSharpRgba32[] imageSharpPixels = new ImageSharpRgba32[decodedPixelStructs.Length];
                                    for (int i = 0; i < decodedPixelStructs.Length; i++)
                                    {
                                        var bcPixel = decodedPixelStructs[i];
                                        imageSharpPixels[i] = new ImageSharpRgba32(bcPixel.r, bcPixel.g, bcPixel.b, bcPixel.a);
                                    }

                                    // Load the ImageSharp-compatible pixel data
                                    image = SixLabors.ImageSharp.Image.LoadPixelData<ImageSharpRgba32>(imageSharpPixels, width, height);
                                }
                                else
                                {
                                    errorMessage = "BCnEncoder failed to decode BC1 or returned unexpected pixel data size.";
                                }
                            }
                            catch (Exception bcEx)
                            {
                                errorMessage = $"Failed to decode DXT1/BC1 data using BCnEncoder: {bcEx.Message}";
                                Debug.WriteLine($"BCnEncoder DXT1 Decoding Error: {bcEx}");
                            }
                            break;

                        case PixelFormat.PFID_DXT5: // BC3
                            try
                            {
                                var decoder = new BcDecoder();
                                // Decode to BCnEncoder's ColorRgba32 format
                                ColorRgba32[] decodedPixelStructs = decoder.DecodeRaw(sourceData, width, height, CompressionFormat.Bc3);

                                if (decodedPixelStructs != null && decodedPixelStructs.Length == width * height)
                                {
                                    // Convert BCnEncoder.ColorRgba32[] to SixLabors.ImageSharp.PixelFormats.Rgba32[]
                                    ImageSharpRgba32[] imageSharpPixels = new ImageSharpRgba32[decodedPixelStructs.Length];
                                    for (int i = 0; i < decodedPixelStructs.Length; i++)
                                    {
                                        var bcPixel = decodedPixelStructs[i];
                                        imageSharpPixels[i] = new ImageSharpRgba32(bcPixel.r, bcPixel.g, bcPixel.b, bcPixel.a);
                                    }

                                    // Load the ImageSharp-compatible pixel data
                                    image = SixLabors.ImageSharp.Image.LoadPixelData<ImageSharpRgba32>(imageSharpPixels, width, height);
                                }
                                else
                                {
                                    errorMessage = "BCnEncoder failed to decode or returned unexpected pixel data size.";
                                }
                            }
                            catch (Exception bcEx)
                            {
                                errorMessage = $"Failed to decode DXT5/BC3 data using BCnEncoder: {bcEx.Message}";
                                Debug.WriteLine($"BCnEncoder DXT5 Decoding Error: {bcEx}");
                            }
                            break;

                        // TODO: Add cases for other formats like DXT1, DXT3, JPEG etc.
                        // DXT1 would be CompressionFormat.Bc1
                        // DXT3 would be CompressionFormat.Bc2
                        default:
                            errorMessage = $"Pixel format {renderSurface.Format} is not currently supported for preview.";
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                errorMessage = $"Error processing texture data: {ex.Message}";
                Debug.WriteLine($"Texture Rendering Error: {ex}");
            }

            if (image != null)
            {
                try
                {
                    // Use image! inside this block where we know it's not null
                    var writeableBitmap = new WriteableBitmap(image!.Width, image!.Height);
                    var bufferSize = image.Width * image.Height * 4; // Rgba32 is 4 bytes per pixel
                    var pixelData = new byte[bufferSize];

                    // Copy pixel data from ImageSharp image (RGBA) to the byte array
                    image.Frames.RootFrame.CopyPixelDataTo(pixelData);

                    // Copy and convert from RGBA byte array to WriteableBitmap buffer (BGRA)
                    using (var stream = writeableBitmap.PixelBuffer.AsStream())
                    {
                        for (int i = 0; i < bufferSize; i += 4)
                        {
                            // Swap R and B channels for BGRA format
                            stream.WriteByte(pixelData[i + 2]); // B
                            stream.WriteByte(pixelData[i + 1]); // G
                            stream.WriteByte(pixelData[i + 0]); // R
                            stream.WriteByte(pixelData[i + 3]); // A
                        }
                    }

                    // Use alias UIImage
                    var imageControl = new UIImage
                    {
                        Source = writeableBitmap,
                        Stretch = Microsoft.UI.Xaml.Media.Stretch.Uniform,
                        MaxWidth = Math.Max(256, image.Width), // Use image! here too
                        MaxHeight = Math.Max(256, image.Height),
                        Margin = new Microsoft.UI.Xaml.Thickness(0, 5, 0, 5)
                    };
                    panel.Children.Add(imageControl);
                }
                 catch (Exception ex)
                {
                    errorMessage = $"Error converting texture to displayable image: {ex.Message}";
                    Debug.WriteLine($"Texture Conversion Error: {ex}");
                     RendererHelpers.AddErrorMessageToPanel(panel, errorMessage);
                }
            }
            else
            {
                // Display placeholder or error message if image couldn't be created
                RendererHelpers.AddErrorMessageToPanel(panel, errorMessage ?? "Could not generate texture preview.");
            }
        }
    }
} 