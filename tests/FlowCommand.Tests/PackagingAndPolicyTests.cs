using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace FlowCommand.Tests;

/// <summary>
/// Guards the things a Microsoft Store submission depends on, so they cannot drift silently:
/// the package manifest, the capabilities it declares, the privacy policy, and the listing text.
/// </summary>
public class PackagingAndPolicyTests
{
    private static string Root => RepoPaths.Root();
    private static string Read(params string[] parts) => File.ReadAllText(Path.Combine(new[] { Root }.Concat(parts).ToArray()));

    private static readonly XNamespace Fnd = "http://schemas.microsoft.com/appx/manifest/foundation/windows10";
    private static readonly XNamespace Uap5 = "http://schemas.microsoft.com/appx/manifest/uap/windows10/5";

    private static XDocument Manifest() => XDocument.Parse(Read("packaging", "AppxManifest.xml"));

    // ---------------------------------------------------------------- package manifest

    [Fact]
    public void Manifest_is_well_formed_and_only_has_the_expected_tokens()
    {
        var text = Read("packaging", "AppxManifest.xml");
        var tokens = Regex.Matches(text, @"\{\{([A-Z_]+)\}\}").Select(m => m.Groups[1].Value).Distinct().OrderBy(x => x).ToArray();
        Assert.Equal(new[] { "IDENTITY_NAME", "PUBLISHER", "PUBLISHER_DISPLAY_NAME", "VERSION" }, tokens);
    }

    [Fact]
    public void Manifest_declares_only_the_capabilities_the_app_needs()
    {
        var caps = Manifest().Descendants().Where(e => e.Parent?.Name == Fnd + "Capabilities").Select(e => e.Attribute("Name")!.Value).OrderBy(x => x).ToArray();
        Assert.Equal(new[] { "internetClient", "radios", "runFullTrust" }, caps);
        var text = Read("packaging", "AppxManifest.xml").ToLowerInvariant();
        foreach (var forbidden in new[] { "microphone", "webcam", "location", "contacts", "documentslibrary", "broadfilesystemaccess" })
            Assert.DoesNotContain($"name=\"{forbidden}\"", text);
    }

    [Fact]
    public void Startup_task_is_declared_disabled_and_matches_the_code()
    {
        var task = Manifest().Descendants(Uap5 + "StartupTask").Single();
        Assert.Equal("false", task.Attribute("Enabled")!.Value);
        var code = Read("src", "FlowCommand", "Platform", "StartupRegistration.cs");
        Assert.Contains($"TaskId = \"{task.Attribute("TaskId")!.Value}\"", code);
    }

    [Fact]
    public void Manifest_minimum_windows_version_matches_the_project()
    {
        var min = Manifest().Descendants(Fnd + "TargetDeviceFamily").Single().Attribute("MinVersion")!.Value;
        var csproj = Read("src", "FlowCommand", "FlowCommand.csproj");
        Assert.Contains($"<SupportedOSPlatformVersion>{min}</SupportedOSPlatformVersion>", csproj);
    }

    [Fact]
    public void Every_tile_the_manifest_names_has_a_generated_image_and_the_icon_exists()
    {
        var assets = Path.Combine(Root, "packaging", "Assets");
        var names = Manifest().Descendants().SelectMany(e => e.Attributes()).Select(a => a.Value)
            .Where(v => v.StartsWith("Assets\\", StringComparison.Ordinal)).Distinct().ToList();
        Assert.NotEmpty(names);
        foreach (var n in names)
        {
            var stem = Path.GetFileNameWithoutExtension(n.Replace('\\', '/'));   // the manifest uses Windows separators; tests also run on Linux
            var found = Directory.EnumerateFiles(assets).Select(Path.GetFileName)
                .Any(f => f!.StartsWith(stem + ".", StringComparison.Ordinal) && f.EndsWith(".png", StringComparison.Ordinal));
            Assert.True(found, $"No image found for {n}");
        }
        Assert.True(File.Exists(Path.Combine(Root, "src", "FlowCommand", "Assets", "FlowCommand.ico")));
    }

    [Fact]
    public void The_program_runs_as_a_normal_user_and_declares_dpi_awareness()
    {
        var manifest = Read("src", "FlowCommand", "app.manifest");
        Assert.Contains("asInvoker", manifest);
        Assert.DoesNotContain("requireAdministrator", manifest);
        Assert.Contains("PerMonitorV2", manifest);
    }

    [Fact]
    public void The_version_is_three_parts_so_the_store_package_can_end_in_dot_zero()
    {
        Assert.Matches(@"<Version>\d+\.\d+\.\d+</Version>", Read("Directory.Build.props"));
    }

    // ---------------------------------------------------------------- privacy and data practices

    [Fact]
    public void The_application_project_has_no_package_dependencies_and_no_telemetry_libraries()
    {
        var csproj = Read("src", "FlowCommand", "FlowCommand.csproj");
        Assert.DoesNotContain("PackageReference", csproj);
        foreach (var word in new[] { "ApplicationInsights", "Sentry", "AppCenter", "Analytics", "Telemetry", "Segment" })
            Assert.DoesNotContain(word, csproj + Read("src", "FlowCommand.Core", "FlowCommand.Core.csproj"));
    }

    [Fact]
    public void Network_destinations_in_the_providers_are_all_named_in_the_privacy_policy()
    {
        var policy = Read("docs", "PRIVACY.md");
        var aiDir = Path.Combine(Root, "src", "FlowCommand.Core", "Ai");
        var hosts = Directory.EnumerateFiles(aiDir, "*.cs")
            .SelectMany(f => Regex.Matches(File.ReadAllText(f), "https?://([A-Za-z0-9.-]+)").Select(m => m.Groups[1].Value))
            .Distinct().ToList();
        Assert.NotEmpty(hosts);
        foreach (var host in hosts)
            Assert.True(policy.Contains(host, StringComparison.Ordinal), $"PRIVACY.md does not mention {host}");
    }

    [Fact]
    public void Only_the_ai_provider_and_host_code_use_http()
    {
        var offenders = new List<string>();
        foreach (var dir in new[] { Path.Combine(Root, "src", "FlowCommand"), Path.Combine(Root, "src", "FlowCommand.Core") })
            foreach (var f in Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories))
            {
                if (f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")) continue;
                var name = Path.GetFileName(f);
                var inAi = f.Contains($"{Path.DirectorySeparatorChar}Ai{Path.DirectorySeparatorChar}");
                if (inAi || name == "ServiceHost.cs" || name == "SettingsWindow.xaml.cs") continue;
                if (Regex.IsMatch(File.ReadAllText(f), @"\bHttpClient\b|\bWebClient\b|\bTcpClient\b|\bSocket\b"))
                    offenders.Add(name);
            }
        Assert.True(offenders.Count == 0, "Unexpected network code in: " + string.Join(", ", offenders));
    }

    // ---------------------------------------------------------------- listing

    private static string Section(string markdown, string heading)
    {
        var m = Regex.Match(markdown, @"^## " + Regex.Escape(heading) + @"\s*$(.*?)(?=^## |\z)", RegexOptions.Multiline | RegexOptions.Singleline);
        Assert.True(m.Success, "Missing section: " + heading);
        return m.Groups[1].Value.Trim();
    }

    [Fact]
    public void Search_terms_follow_the_store_limits_and_name_no_other_product()
    {
        var listing = Read("docs", "store", "LISTING.md");
        var block = Regex.Match(listing, "<!-- search-terms:start -->(.*?)<!-- search-terms:end -->", RegexOptions.Singleline).Groups[1].Value;
        var terms = block.Split('\n').Select(l => l.Trim()).Where(l => l.StartsWith("- ")).Select(l => l[2..].Trim()).ToList();
        Assert.InRange(terms.Count, 1, 7);
        Assert.All(terms, t => Assert.True(t.Length <= 30, $"Search term too long: {t}"));
        Assert.Equal(terms.Count, terms.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        foreach (var t in terms)
            foreach (var brand in new[] { "wispr", "gemini", "claude", "anthropic", "google", "microsoft", "chatgpt", "openai" })
                Assert.DoesNotContain(brand, t, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Description_discloses_dependencies_and_generative_ai_at_the_start_and_fits_the_limits()
    {
        var listing = Read("docs", "store", "LISTING.md");
        var description = Section(listing, "Description");
        var opening = description[..Math.Min(700, description.Length)];
        Assert.Contains("separate dictation tool", opening);
        Assert.Contains("API key", opening);
        Assert.Contains("generative AI", opening);
        Assert.True(description.Length <= 10_000);

        Assert.True(Section(listing, "Short description").Length <= 270, "Short description is longer than the 270 characters most views show");
        Assert.True(Section(listing, "What's new in this version (initial release notes)").Length <= 1_500);

        var features = Section(listing, "Features").Split('\n').Where(l => l.StartsWith("- ")).ToList();
        Assert.InRange(features.Count, 1, 20);
        Assert.All(features, f => Assert.True(f.Length - 2 <= 200, "Feature too long: " + f));
    }

    [Fact]
    public void Listing_makes_no_partnership_claims_and_says_so()
    {
        var description = Section(Read("docs", "store", "LISTING.md"), "Description");
        Assert.Contains("not affiliated", description);
        Assert.DoesNotContain("official", description, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("partner", description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Documentation_uses_no_em_dashes()
    {
        var files = new List<string> { Path.Combine(Root, "README.md"), Path.Combine(Root, "SECURITY.md"), Path.Combine(Root, "CHANGELOG.md") };
        files.AddRange(Directory.EnumerateFiles(Path.Combine(Root, "docs"), "*.md", SearchOption.AllDirectories));
        var hits = files.Where(File.Exists).Where(f => File.ReadAllText(f).Contains('\u2014')).Select(Path.GetFileName).ToList();
        Assert.True(hits.Count == 0, "Em-dash found in: " + string.Join(", ", hits));
    }
}
