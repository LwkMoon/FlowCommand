using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FlowCommand.Core.Actions;
using FlowCommand.Core.Config;
using FlowCommand.Core.Util;

namespace FlowCommand.Platform
{
    /// <summary>open_folder / open_project / open_settings. Paths come only from Windows or from your config file, never from the AI.</summary>
    internal sealed class LocationActions : IActionHandler
    {
        private readonly AppConfig _cfg;
        private readonly AppResolver _resolver;

        public LocationActions(AppConfig cfg, AppResolver resolver) { _cfg = cfg; _resolver = resolver; }

        public IReadOnlyCollection<string> Actions { get; } = new[] { "open_folder", "open_project", "open_settings" };

        public Task<ActionResult> ExecuteAsync(ValidatedPlan plan, ActionContext ctx, CancellationToken ct)
        {
            switch (plan.Action)
            {
                case "open_folder": return OpenFolderAsync(plan.Arg("folder"), ct);
                case "open_project": return OpenProjectAsync(plan.Arg("project"), ct);
                default: return OpenSettingsAsync(plan.Arg("page"), ct);
            }
        }

        // ---------- folders ----------
        private static readonly Dictionary<string, (string Uri, string Title)> ShellFolders =
            new Dictionary<string, (string, string)>
            {
                ["recycle_bin"] = ("shell:RecycleBinFolder", "Recycle Bin"),
                ["this_pc"] = ("shell:MyComputerFolder", "This PC"),
            };

        private async Task<ActionResult> OpenFolderAsync(string query, CancellationToken ct)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var key in ActionCatalog.BuiltInFolders)
            {
                if (ShellFolders.TryGetValue(key, out var shell)) { map[key] = shell.Uri; continue; }
                var p = KnownFolders.Resolve(key);
                if (!string.IsNullOrEmpty(p)) map[key] = p;
            }
            foreach (var kv in _cfg.Folders) map[kv.Key] = Environment.ExpandEnvironmentVariables(kv.Value);

            var keys = map.Keys.ToList();
            var m = NameMatcher.Find(query, keys.Select(k => (IReadOnlyList<string>)new[] { k }).ToList());
            if (!m.Found)
                return ActionResult.Fail($"Unknown folder \u201c{query}\u201d. Available: {string.Join(", ", keys)}.");

            var name = keys[m.Index];
            var path = map[name];
            var isShell = path.StartsWith("shell:", StringComparison.OrdinalIgnoreCase);
            if (!isShell && !Directory.Exists(path)) return ActionResult.Fail($"That folder doesn't exist: {path}");
            try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
            catch (Exception ex) { return ActionResult.Fail($"Couldn't open the folder: {ex.Message}"); }

            var leaf = isShell ? (ShellFolders.TryGetValue(name, out var sf) ? sf.Title : name) : LeafName(path);
            var shown = name.Replace('_', ' ') + (isShell ? "" : $" ({path})");
            for (var i = 0; i < 12; i++)
            {
                await Task.Delay(250, ct);
                if (ExplorerWindowShowing(leaf))
                    return new ActionResult(true, $"Opened {shown}.");
            }
            return new ActionResult(true, $"Asked Explorer to open {shown}, but no matching window appeared.", false);
        }

        private static string LeafName(string path)
        {
            var trimmed = path.TrimEnd('\\', '/');
            var leaf = Path.GetFileName(trimmed);
            return string.IsNullOrEmpty(leaf) ? trimmed : leaf;
        }

        private static bool ExplorerWindowShowing(string leaf) =>
            WindowTools.TopLevelWindows().Any(w =>
                WindowTools.ClassName(w.Handle) == "CabinetWClass" &&
                w.Title.IndexOf(leaf, StringComparison.OrdinalIgnoreCase) >= 0);

        // ---------- projects ----------
        private async Task<ActionResult> OpenProjectAsync(string query, CancellationToken ct)
        {
            var projects = _cfg.Projects.ToList();
            if (projects.Count == 0)
                return ActionResult.Fail("No projects are configured yet. Add them under \"projects\" in config.json (tray icon \u2192 Open config folder).");

            var names = projects.Select(kv =>
            {
                var l = new List<string> { kv.Key };
                l.AddRange(kv.Value.Aliases);
                return (IReadOnlyList<string>)l;
            }).ToList();
            var m = NameMatcher.Find(query, names);
            if (m.IsAmbiguous)
                return ActionResult.Fail($"\u201c{query}\u201d matches several projects: {string.Join(", ", m.Ambiguous.Select(i => projects[i].Key))}.");
            if (!m.Found)
                return ActionResult.Fail($"No project called \u201c{query}\u201d. Known projects: {string.Join(", ", projects.Select(p => p.Key))}.");

            var (name, entry) = (projects[m.Index].Key, projects[m.Index].Value);
            var path = Environment.ExpandEnvironmentVariables(entry.Path ?? "");
            if (!Directory.Exists(path))
                return ActionResult.Fail($"The folder for project \u201c{name}\u201d doesn't exist: {path}. Fix the path in config.json.");

            var editor = _resolver.Resolve(string.IsNullOrWhiteSpace(entry.Editor) ? "vscode" : entry.Editor!);
            if (!editor.Success)
            {
                try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
                catch (Exception ex) { return ActionResult.Fail($"Couldn't open the project folder: {ex.Message}"); }
                return new ActionResult(true, $"Opened {name} in Explorer (editor not found: {editor.Error})", false);
            }

            var app = editor.App!;
            try
            {
                Process.Start(new ProcessStartInfo(app.LaunchTarget)
                {
                    UseShellExecute = true,
                    Arguments = "\"" + path.Replace("\"", "") + "\"",
                });
            }
            catch (Exception ex) { return ActionResult.Fail($"Couldn't launch {app.DisplayName}: {ex.Message}"); }

            var leaf = LeafName(path);
            if (app.ProcessName != null)
            {
                for (var i = 0; i < 24; i++)
                {
                    await Task.Delay(250, ct);
                    if (WindowTools.WindowsOf(app).Any(w => w.Title.IndexOf(leaf, StringComparison.OrdinalIgnoreCase) >= 0))
                        return new ActionResult(true, $"Opened project {name} in {app.DisplayName}.");
                }
            }
            return new ActionResult(true, $"Asked {app.DisplayName} to open project {name}, but its window hasn't been confirmed yet.", false);
        }

        // ---------- settings ----------
        private async Task<ActionResult> OpenSettingsAsync(string page, CancellationToken ct)
        {
            if (!ActionCatalog.SettingsPageUris.TryGetValue(page, out var uri)) return ActionResult.Fail($"Unknown settings page: {page}");
            try { Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true }); }
            catch (Exception ex) { return ActionResult.Fail($"Couldn't open Settings: {ex.Message}"); }

            for (var i = 0; i < 12; i++)
            {
                await Task.Delay(250, ct);
                if (WindowTools.TopLevelWindows().Any(w => w.Title == "Settings" || w.Title.StartsWith("Settings", StringComparison.OrdinalIgnoreCase)))
                    return new ActionResult(true, $"Opened Settings \u2192 {page.Replace('_', ' ')}.");
            }
            return new ActionResult(true, $"Asked Windows to open Settings \u2192 {page.Replace('_', ' ')}, but no Settings window appeared.", false);
        }
    }
}
