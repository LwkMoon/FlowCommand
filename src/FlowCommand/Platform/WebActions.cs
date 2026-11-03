using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FlowCommand.Core.Actions;
using FlowCommand.Core.Config;
using FlowCommand.Core.Util;

namespace FlowCommand.Platform
{
    /// <summary>
    /// open_website and search_web. Website URLs come only from the "websites" section of config.json; for a search
    /// the AI supplies only the words, which are percent-encoded into the configured https "searchUrl" template.
    /// </summary>
    internal sealed class WebActions : IActionHandler
    {
        private readonly AppConfig _cfg;
        private readonly AppResolver _resolver;

        public WebActions(AppConfig cfg, AppResolver resolver) { _cfg = cfg; _resolver = resolver; }

        public IReadOnlyCollection<string> Actions { get; } = new[] { "open_website", "search_web" };

        public Task<ActionResult> ExecuteAsync(ValidatedPlan plan, ActionContext ctx, CancellationToken ct) =>
            Task.FromResult(plan.Action == "open_website" ? OpenSite(plan.Arg("site")) : Search(plan.Arg("query")));

        private ActionResult OpenSite(string query)
        {
            var sites = _cfg.Websites.ToList();
            var names = sites.Select(kv => (IReadOnlyList<string>)new[] { kv.Key }).ToList();
            var m = NameMatcher.Find(query, names);
            if (m.IsAmbiguous)
                return ActionResult.Fail($"\u201c{query}\u201d matches several websites: {string.Join(", ", m.Ambiguous.Take(5).Select(i => sites[i].Key))}.");
            if (!m.Found)
                return ActionResult.Fail($"No website called \u201c{query}\u201d is configured. Add it under \"websites\" in config.json, or say \u201csearch for \u2026\u201d instead.");

            var (name, url) = (sites[m.Index].Key, sites[m.Index].Value);
            if (!WebTargets.TryNormalizeWebsite(url, out var safeUrl))
                return ActionResult.Fail($"The address configured for {name} isn't a valid http(s) URL.");
            return Launch(safeUrl, name);
        }

        private ActionResult Search(string query)
        {
            if (!WebTargets.TryBuildSearchUrl(_cfg.SearchUrl, query, out var url))
                return ActionResult.Fail("The \"searchUrl\" in config.json must be an https address containing {query}.");
            var shown = query.Length > 60 ? query.Substring(0, 60) + "\u2026" : query;
            return Launch(url, $"a search for \u201c{shown}\u201d");
        }

        private ActionResult Launch(string url, string label)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(_cfg.WebsiteBrowser))
                {
                    var browser = _resolver.Resolve(_cfg.WebsiteBrowser);
                    if (browser.Success)
                    {
                        Process.Start(new ProcessStartInfo(browser.App!.LaunchTarget)
                        {
                            UseShellExecute = true,
                            Arguments = "\"" + url.Replace("\"", "%22") + "\"",
                        });
                        return new ActionResult(true, $"Asked {browser.App.DisplayName} to open {label}.", false);
                    }
                }
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                return new ActionResult(true, $"Asked your default browser to open {label}.", false);
            }
            catch (Exception ex)
            {
                return ActionResult.Fail($"Couldn't open {label}: {ex.Message}");
            }
        }
    }
}
