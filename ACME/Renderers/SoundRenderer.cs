using ACME.Utils;
using DatReaderWriter.DBObjs; // Changed from Types to DBObjs for Wave
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Windows.UI.Text;
using Windows.Media.Core; // Added for MediaSource
using Windows.Media.Playback; // Added for MediaPlayer
using Windows.Storage.Streams; // Added for InMemoryRandomAccessStream
using System.Runtime.InteropServices.WindowsRuntime; // Added for AsBuffer
using System.IO; // Added for MemoryStream, BinaryWriter, BinaryReader
using System.Text; // Added for Encoding

namespace ACME.Renderers
{
    /// <summary>
    /// Renders details for Wave (Sound) objects.
    /// </summary>
    public class SoundRenderer : IObjectRenderer
    {
        private MediaPlayer _mediaPlayer;

        // Constructor to initialize MediaPlayer and attach event handlers
        public SoundRenderer()
        {
            _mediaPlayer = new MediaPlayer();
            _mediaPlayer.MediaOpened += MediaPlayer_MediaOpened;
            _mediaPlayer.MediaFailed += MediaPlayer_MediaFailed;
            _mediaPlayer.CurrentStateChanged += MediaPlayer_CurrentStateChanged; 
        }

        public void Render(Panel targetPanel, object data, Dictionary<string, object>? context)
        {
            if (data is not Wave wave) // Changed type check to Wave
            {
                RendererHelpers.AddErrorMessageToPanel(targetPanel, "Invalid data type. Expected Wave.");
                return;
            }

            var mainPanel = new StackPanel() { Margin = new Thickness(12, 0, 0, 20) };

            // --- Simple Properties ---
            // Display ID if available (Wave inherits from DBObj which has Id)
            RendererHelpers.AddSimplePropertyRow(mainPanel, "ID:", wave.Id.ToString("X8")); // Display hex
            RendererHelpers.AddSimplePropertyRow(mainPanel, "Header Size:", $"{wave.Header?.Length ?? 0} bytes");
            RendererHelpers.AddSimplePropertyRow(mainPanel, "Data Size:", $"{wave.Data?.Length ?? 0} bytes");

            // --- Play Button ---
            if (wave.Data != null && wave.Data.Length > 0)
            {
                var playButton = new Button
                {
                    Content = "Play Sound",
                    Margin = new Thickness(0, 10, 0, 0)
                };
                // Store the wave header and data in the button's Tag property
                if (wave.Header != null && wave.Data != null)
                {
                    playButton.Tag = new Tuple<byte[], byte[]>(wave.Header, wave.Data);
                    playButton.Click += PlayButton_Click;
                    mainPanel.Children.Add(playButton);
                }
                else
                {
                    // Handle cases where header or data might be unexpectedly null even if Data has length > 0
                    RendererHelpers.AddInfoMessageToPanel(mainPanel, "Sound data or header missing.", Colors.Orange);
                }
            }
            else
            {
                RendererHelpers.AddInfoMessageToPanel(mainPanel, "No sound data available to play.", Colors.Gray);
            }

            targetPanel.Children.Add(mainPanel); // Add the constructed panel to the target
        }

        /// <summary>
        /// Handles the click event for the Play button.
        /// </summary>
        private async void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("PlayButton_Click entered.");

            if (sender is Button button)
            {
                System.Diagnostics.Debug.WriteLine($"Sender is a Button. Tag type: {button.Tag?.GetType().Name ?? "null"}");
                // Expect Tag to be a Tuple containing Header (Item1) and Data (Item2)
                if (button.Tag is Tuple<byte[], byte[]> waveInfo)
                {
                    byte[] waveHeader = waveInfo.Item1;
                    byte[] waveData = waveInfo.Item2;
                    System.Diagnostics.Debug.WriteLine($"Tag is Tuple<byte[], byte[]>. Header Length: {waveHeader?.Length ?? -1}, Data Length: {waveData?.Length ?? -1}"); 
                    try
                    {
                        System.Diagnostics.Debug.WriteLine("Attempting to create WAV stream...");
                        if (waveHeader == null || waveData == null)
                        {
                            System.Diagnostics.Debug.WriteLine("Header or Data is null within Tuple.");
                            if (button.Parent is Panel p) RendererHelpers.AddErrorMessageToPanel(p, "Playback Error: Missing header or data.");
                            return;
                        }

                        // Create the complete WAV stream using the header and data
                        var wavStream = CreateWavStream(waveHeader, waveData);
                        if (wavStream == null) 
                        {
                            System.Diagnostics.Debug.WriteLine("Failed to create WAV stream.");
                            if (button.Parent is Panel p) RendererHelpers.AddErrorMessageToPanel(p, "Playback Error: Failed to create WAV stream from header.");
                            return;
                        }

                        // Create a MediaSource from the stream (now includes header)
                        var mediaSource = MediaSource.CreateFromStream(wavStream, ""); // Content type hint no longer needed

                        // Set the source for the MediaPlayer and play
                        _mediaPlayer.Source = mediaSource;
                        System.Diagnostics.Debug.WriteLine("MediaPlayer source set. Calling Play()...");
                        _mediaPlayer.Play();
                        System.Diagnostics.Debug.WriteLine("MediaPlayer.Play() called.");
                    }
                    catch (Exception ex)
                    {
                        // Handle potential errors (e.g., invalid WAV data, playback issues)
                        System.Diagnostics.Debug.WriteLine($"Error playing sound: {ex.Message}");
                        // Log first few bytes for format checking
                        string firstBytes = waveData.Length > 8 ? BitConverter.ToString(waveData, 0, 8) : BitConverter.ToString(waveData);
                        System.Diagnostics.Debug.WriteLine($"First bytes of waveData: {firstBytes}");

                        // Show error message in the UI
                        if (button.Parent is Panel parentPanel)
                        {
                            RendererHelpers.AddErrorMessageToPanel(parentPanel, $"Playback Error: {ex.Message}");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Creates a complete WAV audio stream by prepending a WAV header (derived from headerBytes)
        /// to the raw PCM data.
        /// </summary>
        /// <param name="headerBytes">Byte array containing audio format info (expected WAVEFORMATEX layout).</param>
        /// <param name="pcmData">Byte array containing the raw PCM audio samples.</param>
        /// <returns>An IRandomAccessStream containing the full WAV data, or null on error.</returns>
        private IRandomAccessStream? CreateWavStream(byte[] headerBytes, byte[] pcmData)
        {
            try
            {
                if (headerBytes.Length < 16) // Need at least up to wBitsPerSample
                {
                    System.Diagnostics.Debug.WriteLine($"Error creating WAV stream: Header too short ({headerBytes.Length} bytes).");
                    return null;
                }

                ushort wFormatTag, nChannels, nBlockAlign, wBitsPerSample;
                uint nSamplesPerSec, nAvgBytesPerSec;

                // Parse header assuming WAVEFORMATEX structure
                using (var reader = new BinaryReader(new MemoryStream(headerBytes)))
                {
                    wFormatTag = reader.ReadUInt16();       // Offset 0
                    nChannels = reader.ReadUInt16();        // Offset 2
                    nSamplesPerSec = reader.ReadUInt32();   // Offset 4
                    nAvgBytesPerSec = reader.ReadUInt32();  // Offset 8
                    nBlockAlign = reader.ReadUInt16();      // Offset 12
                    wBitsPerSample = reader.ReadUInt16();   // Offset 14
                    // Ignoring cbSize at offset 16 if present
                }

                System.Diagnostics.Debug.WriteLine($"Parsed Header: FormatTag={wFormatTag}, Channels={nChannels}, SampleRate={nSamplesPerSec}, BitsPerSample={wBitsPerSample}, BlockAlign={nBlockAlign}, AvgBytesPerSec={nAvgBytesPerSec}");

                // Basic validation
                if (wFormatTag != 1) // 1 = WAVE_FORMAT_PCM
                {
                    System.Diagnostics.Debug.WriteLine($"Error creating WAV stream: Unsupported format tag ({wFormatTag}). Only PCM (1) is supported.");
                    return null;
                }
                if (nChannels == 0 || nSamplesPerSec == 0 || wBitsPerSample == 0 || nBlockAlign == 0)
                {
                    System.Diagnostics.Debug.WriteLine($"Error creating WAV stream: Invalid audio parameters (zero values).");
                    return null;
                }
                // Optional: Further validation like nBlockAlign == (nChannels * wBitsPerSample / 8)
                // Optional: Further validation like nAvgBytesPerSec == (nSamplesPerSec * nBlockAlign)

                var outputStream = new MemoryStream();
                using (var writer = new BinaryWriter(outputStream, Encoding.UTF8, true))
                {
                    // RIFF chunk descriptor
                    writer.Write(Encoding.ASCII.GetBytes("RIFF"));
                    uint subchunk2Size = (uint)pcmData.Length;
                    uint chunkSize = 36 + subchunk2Size; // 36 = size of header fields before PCM data
                    writer.Write(chunkSize);
                    writer.Write(Encoding.ASCII.GetBytes("WAVE"));

                    // fmt sub-chunk
                    writer.Write(Encoding.ASCII.GetBytes("fmt "));
                    writer.Write(16); // Subchunk1Size for PCM
                    writer.Write(wFormatTag); // AudioFormat (PCM=1)
                    writer.Write(nChannels);
                    writer.Write(nSamplesPerSec);
                    writer.Write(nAvgBytesPerSec);
                    writer.Write(nBlockAlign);
                    writer.Write(wBitsPerSample);

                    // data sub-chunk
                    writer.Write(Encoding.ASCII.GetBytes("data"));
                    writer.Write(subchunk2Size);

                    // Actual sound data
                    writer.Write(pcmData);
                }

                outputStream.Seek(0, SeekOrigin.Begin);
                System.Diagnostics.Debug.WriteLine($"Successfully created WAV stream. Total size: {outputStream.Length} bytes.");
                return outputStream.AsRandomAccessStream();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating WAV stream: {ex.Message}");
                return null;
            }
        }

        // --- MediaPlayer Event Handlers ---

        private void MediaPlayer_MediaOpened(MediaPlayer sender, object args)
        {
            System.Diagnostics.Debug.WriteLine("MediaPlayer_MediaOpened: Media opened successfully. Duration: " + sender.PlaybackSession.NaturalDuration);
        }

        private void MediaPlayer_MediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
        {
            System.Diagnostics.Debug.WriteLine($"MediaPlayer_MediaFailed: Error: {args.Error}, Message: {args.ErrorMessage}, ExtendedErrorCode: {args.ExtendedErrorCode}");
            // Potentially update UI here as well
        }

        private void MediaPlayer_CurrentStateChanged(MediaPlayer sender, object args)
        {
            System.Diagnostics.Debug.WriteLine($"MediaPlayer_CurrentStateChanged: New state = {sender.PlaybackSession.PlaybackState}");
        }

        // Removed private helper methods specific to HeritageGroupRendering
    }
} 