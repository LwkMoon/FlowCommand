using Xunit;

namespace FlowCommand.Tests;

/// <summary>
/// FlowCommand never captures audio: Wispr Flow is the only application that uses the microphone.
/// Fails if any source file references audio-capture or speech-recognition APIs.
/// </summary>
public class NoMicrophoneCodeTests
{
    private static readonly string[] Forbidden =
    {
        "System.Speech", "Microsoft.Speech", "NAudio", "CSCore", "Windows.Media.SpeechRecognition",
        "Windows.Media.Capture", "MediaCapture", "AudioGraph", "waveIn", "WaveInEvent", "IAudioClient",
        "MMDevice", "Whisper", "Vosk", "PortAudio", "DeviceCapability", "Capability Name=\"microphone\"",
    };

    private static string RepoRoot() => RepoPaths.Root();

    [Fact]
    public void Sources_contain_no_audio_capture_or_speech_recognition_apis()
    {
        var src = Path.Combine(RepoRoot(), "src");
        var files = Directory.EnumerateFiles(src, "*.*", SearchOption.AllDirectories)
            .Where(f => (f.EndsWith(".cs") || f.EndsWith(".xaml") || f.EndsWith(".csproj") || f.EndsWith(".manifest"))
                        && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                        && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .ToList();
        Assert.NotEmpty(files);

        var hits = new List<string>();
        foreach (var f in files)
        {
            var text = File.ReadAllText(f);
            foreach (var word in Forbidden)
                if (text.Contains(word, StringComparison.Ordinal)) hits.Add($"{Path.GetFileName(f)}: {word}");
        }
        Assert.True(hits.Count == 0, "Forbidden audio/speech references: " + string.Join("; ", hits));
    }

    [Fact]
    public void Core_library_has_no_package_dependencies()
    {
        var csproj = File.ReadAllText(Path.Combine(RepoRoot(), "src", "FlowCommand.Core", "FlowCommand.Core.csproj"));
        Assert.DoesNotContain("PackageReference", csproj);
    }
}
