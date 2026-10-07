namespace WebDownload.Server.Models;

/// <summary>One RVC voice the page offers: the .pth and .index files that Applio trained.</summary>
public class VoiceModelSettings
{
    // Short id the page sends back (letters, digits, - and _). Defaults to the position in the list.
    public string? Id { get; set; }

    // What the page shows. Defaults to the .pth file name.
    public string? Name { get; set; }

    // Full path of the model (.pth) and of its index (.index) on the machine that runs the app.
    public string PthPath { get; set; } = "";
    public string IndexPath { get; set; } = "";

    // Pitch shift in semitones the page starts with for this voice (for example +12 when a male
    // singer is converted to a female voice, -12 for the opposite, 0 for the same range).
    public int DefaultPitch { get; set; }
}

/// <summary>
/// Bind to the "VoiceSwap" section of appsettings.json. IIS never runs the AI: the app starts Applio's
/// own Python (core.py infer) as a background process, once per job, and waits for it to finish.
///
/// Step 1 (split into vocals and music) re-uses the "Splitter" section: Demucs location, models,
/// quality labels, and the media picker (menus, extensions, rpm). Only what is specific to the
/// voice swap lives here.
///
/// List-valued settings are null by default for the reason given in SplitterSettings (list binding appends).
/// </summary>
public class VoiceSwapSettings
{
    // The Applio folder (the one that holds core.py). It is also the working directory of the process:
    // Applio reads some of its own files by relative path.
    public string ApplioFolder { get; set; } = @"C:\Tools\Applio";

    // Applio's Python. Empty = <ApplioFolder>\env\python.exe, where Applio's installer puts it.
    public string PythonExecutablePath { get; set; } = "";

    // Applio's command line script, relative to ApplioFolder.
    public string ScriptName { get; set; } = "core.py";

    // The voices to choose from. At least one is needed.
    public List<VoiceModelSettings>? Models { get; set; }

    // Results go to <OutputFolder>\audio and <OutputFolder>\video. Keep it inside
    // ApplicationSettings:MediaDrive so /medias can serve the files back as links.
    public string OutputFolder { get; set; } = @"d:\medias\voiceswap";

    // Scratch space for stems and the converted voice. Empty = system temp folder.
    public string TempFolder { get; set; } = "";

    // ---- Applio (RVC) settings. Names follow Applio's "infer" options. ----------------------------------------
    public string F0Method { get; set; } = "rmvpe";
    public double IndexRate { get; set; } = 0.5;        // 0 - 1: how much of the trained voice's character is pulled in
    public double Protect { get; set; } = 0.33;         // 0 - 0.5: protects consonants and breaths
    public double VolumeEnvelope { get; set; } = 1.0;   // 0 - 1
    public string EmbedderModel { get; set; } = "contentvec";
    public int SpeakerId { get; set; } = 0;
    public bool SplitAudio { get; set; } = true;        // also gives real progress; Applio reports chunk by chunk
    public bool CleanAudio { get; set; } = false;
    public double CleanStrength { get; set; } = 0.7;
    public bool F0Autotune { get; set; } = false;
    public double F0AutotuneStrength { get; set; } = 1.0;

    // Any further Applio "infer" arguments, added as they are, one list item per argument,
    // for example [ "--proposed-pitch", "--proposed-pitch-threshold", "155" ].
    public List<string>? ExtraArgs { get; set; }

    // When the GPU run fails (out of memory on a 6 GB card, driver problems) try once more on the CPU.
    public bool FallbackToCpu { get; set; } = true;

    // A job's Applio process is stopped after this long. 0 = no limit.
    public int InferenceTimeoutMinutes { get; set; } = 60;

    // ---- Mixing (ffmpeg) ---------------------------------------------------------------------------------------
    // {0} = music gain in dB, {1} = converted voice gain in dB. Input 0 is the music, input 1 the voice;
    // the filter graph must end in the pad named by MixOutputLabel. duration=first keeps the song's length.
    public string MixFilterTemplate { get; set; } =
        "[0:a]aformat=sample_rates=44100:channel_layouts=stereo,volume={0}dB[m];" +
        "[1:a]aformat=sample_rates=44100:channel_layouts=stereo,volume={1}dB[v];" +
        "[m][v]amix=inputs=2:duration=first:dropout_transition=0:normalize=0,alimiter=limit=0.95[mix]";
    public string MixOutputLabel { get; set; } = "mix";
    public double MusicGainDb { get; set; } = 0;
    public double VocalGainDb { get; set; } = 0;

    public string Mp3Bitrate { get; set; } = "320k";
    public string VideoAudioBitrate { get; set; } = "256k";
    public string MixTrackTitle { get; set; } = "Voice swap";

    // Also save the converted voice alone as an MP3 (handy when tuning pitch and the mix).
    public bool KeepConvertedVoice { get; set; } = true;

    public int MaxConcurrentJobs { get; set; } = 1;

    public IReadOnlyList<string> GetExtraArgs() =>
        (ExtraArgs ?? new List<string>()).Where(a => !string.IsNullOrWhiteSpace(a)).ToList();

    public string GetPython() =>
        string.IsNullOrWhiteSpace(PythonExecutablePath)
            ? Path.Combine(ApplioFolder, "env", "python.exe")
            : PythonExecutablePath;

    public string GetTempRoot() =>
        string.IsNullOrWhiteSpace(TempFolder) ? Path.Combine(Path.GetTempPath(), "VoiceSwap") : TempFolder;

    // The configured voices with their ids and names filled in; entries without a .pth path are dropped.
    public IReadOnlyList<ResolvedVoiceModel> GetModels()
    {
        var result = new List<ResolvedVoiceModel>();
        var i = 0;
        foreach (var m in Models ?? new List<VoiceModelSettings>())
        {
            i++;
            if (string.IsNullOrWhiteSpace(m.PthPath)) continue;
            var id = string.IsNullOrWhiteSpace(m.Id) ? i.ToString() : m.Id.Trim();
            var name = string.IsNullOrWhiteSpace(m.Name) ? Path.GetFileNameWithoutExtension(m.PthPath) : m.Name.Trim();
            result.Add(new ResolvedVoiceModel(id, name, m.PthPath.Trim(), (m.IndexPath ?? "").Trim(), m.DefaultPitch));
        }
        return result;
    }
}

public record ResolvedVoiceModel(string Id, string Name, string PthPath, string IndexPath, int DefaultPitch);
