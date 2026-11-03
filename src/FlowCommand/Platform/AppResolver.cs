using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using FlowCommand.Core.Config;
using FlowCommand.Core.Util;
using Microsoft.Win32;

namespace FlowCommand.Platform
{
    internal sealed record ResolvedApp(string DisplayName, string LaunchTarget, string? ProcessName, string? PathContains);
    internal sealed record ResolveResult(ResolvedApp? App, string? Error) { public bool Success => App != null; }

    /// <summary>
    /// Turns a friendly name into something launchable. Order: configured apps (+aliases, known paths)
    /// → Start Menu shortcuts → registered "App Paths". Never executes anything itself.
    /// </summary>
    internal sealed class AppResolver
    {
        private sealed record StartEntry(string Name, string Path);
        private static readonly Regex ExeName = new Regex(@"^[A-Za-z0-9._-]{1,64}$");

        private readonly AppConfig _cfg;
        private List<StartEntry>? _startCache;
        private DateTime _startCacheAt;

        public AppResolver(AppConfig cfg) { _cfg = cfg; }

        public ResolveResult Resolve(string query)
        {
            var entries = _cfg.Applications.ToList();
            var names = entries.Select(kv => (IReadOnlyList<string>)NamesOf(kv.Key, kv.Value)).ToList();
            var m = NameMatcher.Find(query, names);
            if (m.IsAmbiguous)
                return Err($"\u201c{query}\u201d matches several configured apps: {string.Join(", ", m.Ambiguous.Select(i => entries[i].Value.DisplayName))}.");
            if (m.Found) return ResolveConfigured(entries[m.Index].Value);
            return ResolveDiscovered(query);
        }

        private static List<string> NamesOf(string id, ApplicationEntry e)
        {
            var l = new List<string> { id, e.DisplayName };
            l.AddRange(e.Aliases);
            return l.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        }

        private ResolveResult ResolveConfigured(ApplicationEntry e)
        {
            string? target = e.Paths
                .Select(p => Environment.ExpandEnvironmentVariables(p))
                .FirstOrDefault(File.Exists);

            if (target == null)
            {
                foreach (var name in NamesOf("", e))
                {
                    var hit = FindStart(name);
                    if (hit.Entry != null) { target = hit.Entry.Path; break; }
                }
            }
            if (target == null && e.ProcessName != null && ExeName.IsMatch(e.ProcessName))
                target = AppPathsLookup(e.ProcessName + ".exe");

            if (target == null)
                return Err($"{e.DisplayName} is in your config but doesn't appear to be installed. Add its path under \"applications\" in config.json.");

            var process = e.ProcessName ?? ProcessFromTarget(target);
            return new ResolveResult(new ResolvedApp(e.DisplayName, target, process, e.PathContains), null);
        }

        private ResolveResult ResolveDiscovered(string query)
        {
            var hit = FindStart(query);
            if (hit.Ambiguous.Count > 1)
                return Err($"\u201c{query}\u201d matches several installed apps: {string.Join(", ", hit.Ambiguous.Take(5))}. Which one?");
            if (hit.Entry != null)
                return new ResolveResult(new ResolvedApp(hit.Entry.Name, hit.Entry.Path, ProcessFromTarget(hit.Entry.Path), null), null);

            var exe = new string(query.Where(char.IsLetterOrDigit).ToArray()) + ".exe";
            if (ExeName.IsMatch(exe))
            {
                var p = AppPathsLookup(exe);
                if (p != null) return new ResolveResult(new ResolvedApp(query, p, Path.GetFileNameWithoutExtension(p), null), null);
            }
            return Err($"No app called \u201c{query}\u201d was found. You can add it under \"applications\" in config.json.");
        }

        private static ResolveResult Err(string message) => new ResolveResult(null, message);

        // ---- Start Menu ----
        private sealed record StartHit(StartEntry? Entry, List<string> Ambiguous);

        private StartHit FindStart(string query)
        {
            var all = StartEntries();
            var m = NameMatcher.Find(query, all.Select(e => (IReadOnlyList<string>)new[] { e.Name }).ToList());
            if (m.Found) return new StartHit(all[m.Index], new List<string>());
            if (m.IsAmbiguous) return new StartHit(null, m.Ambiguous.Select(i => all[i].Name).ToList());
            return new StartHit(null, new List<string>());
        }

        private List<StartEntry> StartEntries()
        {
            if (_startCache != null && DateTime.UtcNow - _startCacheAt < TimeSpan.FromMinutes(2)) return _startCache;
            var list = new List<StartEntry>();
            var opts = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true };
            foreach (var root in new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
                Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            })
            {
                var programs = Path.Combine(root, "Programs");
                if (!Directory.Exists(programs)) continue;
                try
                {
                    foreach (var f in Directory.EnumerateFiles(programs, "*.lnk", opts))
                    {
                        var name = Path.GetFileNameWithoutExtension(f);
                        if (name.IndexOf("uninstall", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                        list.Add(new StartEntry(name, f));
                    }
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            _startCache = list;
            _startCacheAt = DateTime.UtcNow;
            return list;
        }

        // ---- helpers ----
        private static string? AppPathsLookup(string exe)
        {
            foreach (var hive in new[] { Registry.LocalMachine, Registry.CurrentUser })
            {
                try
                {
                    using (var k = hive.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\" + exe))
                    {
                        var v = (k?.GetValue(null) as string)?.Trim().Trim('"');
                        if (!string.IsNullOrWhiteSpace(v) && File.Exists(v)) return v;
                    }
                }
                catch { }
            }
            return null;
        }

        private static string? ProcessFromTarget(string path)
        {
            try
            {
                var ext = Path.GetExtension(path).ToLowerInvariant();
                if (ext == ".exe") return Path.GetFileNameWithoutExtension(path);
                if (ext != ".lnk") return null;
                var t = Type.GetTypeFromProgID("WScript.Shell");
                if (t == null) return null;
                dynamic sh = Activator.CreateInstance(t)!;
                try
                {
                    dynamic sc = sh.CreateShortcut(path);
                    string target = sc.TargetPath;
                    return !string.IsNullOrWhiteSpace(target) && target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                        ? Path.GetFileNameWithoutExtension(target)
                        : null;
                }
                finally { Marshal.FinalReleaseComObject((object)sh); }
            }
            catch { return null; }
        }
    }
}
