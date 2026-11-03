using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using FlowCommand.Core.Ai;
using FlowCommand.Core.Config;
using FlowCommand.Core.Pipeline;
using FlowCommand.Platform;

namespace FlowCommand
{
    /// <summary>Owns configuration, secrets, logging and the (re-buildable) command pipeline.</summary>
    internal sealed class ServiceHost
    {
        public static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(70) };

        public ILog Log { get; }
        public ISecretStore Secrets { get; }
        public AppConfig Config { get; private set; } = AppConfig.CreateDefault();
        public CommandPipeline Pipeline { get; private set; } = null!;
        public string? ConfigError { get; private set; }

        public ServiceHost()
        {
            Directory.CreateDirectory(AppPaths.ConfigDir);
            Log = new FileLog(AppPaths.LogFile);
            Secrets = new DpapiSecretStore(AppPaths.SecretsDir);
            var firstRun = !File.Exists(AppPaths.ConfigFile);
            Reload();
            if (firstRun && ConfigError == null) ApplyDetectedBrowser();
        }

        /// <summary>On first run, make "my browser" mean the browser Windows already uses.</summary>
        private void ApplyDetectedBrowser()
        {
            try
            {
                var id = DefaultBrowserDetector.Detect();
                if (id == null || !Config.Applications.ContainsKey(id)) return;
                Config.DefaultApps["browser"] = id;
                SaveConfig();
            }
            catch (Exception ex)
            {
                Log.Warn("Default browser detection failed: " + ex.GetType().Name);
            }
        }

        public void Reload()
        {
            try
            {
                Config = ConfigStore.Load(AppPaths.ConfigFile);
                ConfigError = null;
            }
            catch (ConfigException ex)
            {
                ConfigError = ex.Message;
                Config = AppConfig.CreateDefault();
                Log.Error("Config load failed; using defaults", ex);
            }
            Rebuild();
        }

        public void SaveConfig()
        {
            if (ConfigError != null && File.Exists(AppPaths.ConfigFile))
            {
                // Never silently overwrite a file the user hand-edited into an invalid state.
                File.Copy(AppPaths.ConfigFile, AppPaths.ConfigFile + ".bad", true);
                ConfigError = null;
            }
            ConfigStore.Save(AppPaths.ConfigFile, Config);
            Rebuild();
        }

        public void Rebuild()
        {
            var provider = AiProviderFactory.Create(Config, Http, () => GetApiKey());
            Pipeline = new CommandPipeline(Config, provider, Handlers.Create(Config), Log);
        }

        /// <summary>Key for the currently configured provider.</summary>
        public string? GetApiKey() => GetApiKey(Config.Provider);

        /// <summary>Stored (DPAPI) key first, then the provider's environment variable(s).</summary>
        public string? GetApiKey(string provider)
        {
            var name = ProviderCatalog.Normalize(provider);
            if (!ProviderCatalog.Names.Contains(name)) return null;
            var stored = Secrets.Get(name);
            if (!string.IsNullOrWhiteSpace(stored)) return stored;
            foreach (var v in ProviderCatalog.EnvVars(name))
            {
                var env = Environment.GetEnvironmentVariable(v);
                if (!string.IsNullOrWhiteSpace(env)) return env;
            }
            return null;
        }

        public bool HasApiKey() => !string.IsNullOrWhiteSpace(GetApiKey());
    }
}
