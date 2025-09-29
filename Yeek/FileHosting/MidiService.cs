using FFMpegCore;
using NFluidsynth;
using TickerQ.Utilities.Base;
using Yeek.Configuration;
using Yeek.FileHosting.Midi;
using Yeek.FileHosting.Model;
using Yeek.FileHosting.Repositories;

namespace Yeek.FileHosting;

public class MidiService
{
    private static readonly SemaphoreSlim Lock = new SemaphoreSlim(1, 1);

    private readonly IFileRepository _fileRepository;
    private readonly ILogger<MidiService> _logger;
    private readonly FileConfiguration _fileConfiguration = new();

    private static readonly List<string> ExtensionsToGenerate =
    [
        //".webm", // WebM (Opus codec)
        //".m4a", // AAC (in MP4 container)
        ".ogg",  // OGG Vorbis (mono for admemes)
        ".mp3", // Fallback
    ];

    public MidiService(IFileRepository context, ILogger<MidiService> logger, IConfiguration configuration)
    {
        _fileRepository = context;
        _logger = logger;
        configuration.Bind(FileConfiguration.Name, _fileConfiguration);
    }

    [TickerFunction(functionName: "CleanupDb", cronExpression: "*/10 * * * *")]
    public async Task CleanupDb(CancellationToken token)
    {
        if (!await Lock.WaitAsync(0, token)) // try to acquire lock without waiting
        {
            _logger.LogDebug("A preview run is currently running... Skipping cleanup.");
            return;
        }

        try
        {
            var files = Directory.EnumerateFiles(_fileConfiguration.UserContentDirectory);
            var allIds = await _fileRepository.GetAllIdsAsync();
            var validIds = new HashSet<Guid>(allIds);

            foreach (var file in files)
            {
                try
                {
                    var fileName = Path.GetFileNameWithoutExtension(file);
                    if (Guid.TryParse(fileName, out var fileId))
                    {
                        if (!validIds.Contains(fileId))
                        {
                            File.Delete(file);
                            _logger.LogInformation("Deleted orphan file: {File}", file);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Skipping file with invalid name format: {File}", file);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process file {File}", file);
                }
            }
        }
        finally
        {
            Lock.Release();
        }
    }

    [TickerFunction(functionName: "GeneratePreviews", cronExpression: "*/2 * * * *")]
    public async Task GeneratePreviews(CancellationToken token)
    {
        if (!await Lock.WaitAsync(0, token)) // try to acquire lock without waiting
        {
            _logger.LogDebug("Job already running. Skipping this run.");
            return;
        }

        try
        {
            var missingFiles = await _fileRepository.GetMissingPreviews(ExtensionsToGenerate.ToArray());

            if (missingFiles.Length == 0)
            {
                return;
            }

            _logger.LogInformation("Generating previews for {count} files!", missingFiles.Length);


            using var settings = new Settings();
            // use number of samples processed as timing source, rather than the system timer
            settings["player.timing-source"].StringValue = "sample";
            // since this is a non-realtime scenario, there is no need to pin the sample data
            settings["synth.lock-memory"].IntValue = 0;
            settings["synth.midi-bank-select"].StringValue = "gm";
            settings["synth.device-id"].IntValue = 16;

            // Recommended settings, gotten from https://github.com/mrbumpy409/GeneralUser-GS/blob/main/documentation/README.md#302-fluidsynth
            settings["synth.reverb.damp"].DoubleValue = 0.3;
            settings["synth.reverb.level"].DoubleValue = 0.7;
            settings["synth.reverb.room-size"].DoubleValue = 0.5;
            settings["synth.reverb.width"].DoubleValue = 0.8;
            settings["synth.chorus.depth"].DoubleValue = 3.6;
            settings["synth.chorus.level"].DoubleValue = 0.55;
            settings["synth.chorus.nr"].IntValue = 4;
            settings["synth.chorus.speed"].DoubleValue = 0.36;

            using var synth = new Synth(settings);

            synth.LoadSoundFont(Path.GetFullPath(_fileConfiguration.SoundFontPath), false);

            foreach (var missingFile in missingFiles)
            {
                if (token.IsCancellationRequested)
                {
                    _logger.LogWarning("Exiting early!");
                    break;
                }

                var uploadedFile = await _fileRepository.GetUploadedFilePureAsync(missingFile);
                if (uploadedFile == null)
                    continue; // ???

                var file = Path.Combine(_fileConfiguration.UserContentDirectory, uploadedFile.RelativePath);
                if (!File.Exists(file))
                {
                    _logger.LogError("File exists in DB but not in file system. Path {File}", uploadedFile.RelativePath);
                    continue;
                }

                // Find out which extensions are missing for this file
                var preview = await _fileRepository.GetFilePreviewOrNullAsync(missingFile);
                var existingExts = preview?.SupportedExtensions ?? [];
                var missingExts = ExtensionsToGenerate
                    .Except(existingExts, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (missingExts.Count == 0)
                {
                    _logger.LogDebug("All previews already exist for {fileId}", missingFile);
                    continue;
                }

                var wavPath = Path.GetTempFileName() + ".wav";
                settings["audio.file.name"].StringValue = wavPath;
                using (var player = new Player(synth))
                {
                    player.Add(file);
                    player.Play();

                    using (var renderer = new FileRenderer(synth))
                    {
                        while (player.Status == FluidPlayerStatus.Playing)
                        {
                            renderer.ProcessBlock();
                        }
                    }
                }

                foreach (var ext in missingExts)
                {
                    var outputPath = Path.Combine(_fileConfiguration.UserContentDirectory, $"{missingFile}{ext}");

                    switch (ext)
                    {
                        case ".webm":
                            await FFMpegArguments
                                .FromFileInput(wavPath)
                                .OutputToFile(outputPath, overwrite: true, options => options
                                    .WithAudioCodec("libopus"))
                                .ProcessAsynchronously();
                            break;

                        case ".m4a":
                            await FFMpegArguments
                                .FromFileInput(wavPath)
                                .OutputToFile(outputPath, overwrite: true, options => options
                                    .WithAudioCodec("aac"))
                                .ProcessAsynchronously();
                            break;

                        case ".mp3":
                            await FFMpegArguments
                                .FromFileInput(wavPath)
                                .OutputToFile(outputPath, overwrite: true, options => options
                                    .WithAudioCodec("libmp3lame"))
                                .ProcessAsynchronously();
                            break;

                        case ".ogg":
                            await FFMpegArguments
                                .FromFileInput(wavPath)
                                .OutputToFile(outputPath, overwrite: true, options => options
                                    .WithAudioCodec("libvorbis")
                                    .WithCustomArgument("-ac 1")) // forces mono
                                .ProcessAsynchronously();
                            break;
                    }
                }

                // Cleanup
                File.Delete(wavPath);
                await _fileRepository.AddFilePreviewAsync(missingFile, new FilePreview()
                {
                    GeneratedAt = DateTime.UtcNow,
                    SupportedExtensions = ExtensionsToGenerate.ToArray(),
                    UploadedFileId = missingFile,
                });
                _logger.LogDebug("Generated previews for {fileId}!", missingFile);
            }
        }
        finally
        {
            Lock.Release();
        }
    }

    public static bool IsMidiFileAMidiFile(MemoryStream stream)
    {
        var wrapper = new MidiStreamWrapper(stream);

        if (wrapper.ReadString(4) != "MThd")
            return false;

        var headerLength = wrapper.ReadUInt32();
        // MIDI specs define that the header is 6 bytes, we only look at the 6 bytes, if its more, we skip ahead.

        wrapper.Skip(2); // format
        var trackCount = wrapper.ReadUInt16();
        wrapper.Skip(2); // time div

        // We now skip ahead if we still have any header length left
        wrapper.Skip((int)(headerLength - 6));

        for (var i = 0; i < trackCount; i++)
        {
            if (wrapper.ReadString(4) != "MTrk")
            {
                // Invalid track header
                return false;
            }

            var trackLength = wrapper.ReadUInt32();
            var trackEnd = wrapper.StreamPosition + trackLength;
            byte? lastStatusByte = null;

            while (wrapper.StreamPosition < trackEnd)
            {
                wrapper.ReadVariableLengthQuantity();

                var firstByte = wrapper.ReadByte();
                if (firstByte >= 0x80)
                {
                    lastStatusByte = firstByte;
                }
                else
                {
                    // Running status: push byte back for reading as data
                    wrapper.Skip(-1);
                }

                // The first event in each MTrk chunk must specify status.
                if (lastStatusByte == null)
                {
                    return false;
                }

                var eventType = (byte)(lastStatusByte & 0xF0);

                switch (lastStatusByte)
                {
                    // Meta events
                    case 0xFF:
                    {
                        wrapper.Skip(1);
                        var metaLength = wrapper.ReadVariableLengthQuantity();
                        wrapper.Skip((int)metaLength);
                        break;
                    }

                    // SysEx events
                    case 0xF0:
                    case 0xF7:
                    {
                        var sysexLength = wrapper.ReadVariableLengthQuantity();
                        wrapper.Skip((int)sysexLength);
                        // Sysex events and meta-events cancel any running status which was in effect.
                        // Running status does not apply to and may not be used for these messages.
                        lastStatusByte = null;
                        break;
                    }


                    default:
                        switch (eventType)
                        {
                            case 0x80: // Note Off
                            case 0x90: // Note On
                            case 0xA0: // Polyphonic Key Pressure
                            case 0xB0: // Control Change
                            case 0xE0: // Pitch Bend
                            {
                                wrapper.Skip(2);
                                break;
                            }

                            case 0xC0: // Program Change
                            case 0xD0: // Channel Pressure
                            {
                                wrapper.Skip(1);
                                break;
                            }

                            default:
                                return false;
                        }
                        break;
                }
            }
        }

        return true;
    }
}