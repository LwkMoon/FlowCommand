using System.Collections.Generic;
using FlowCommand.Core.Actions;
using FlowCommand.Core.Config;

namespace FlowCommand.Platform
{
    /// <summary>The complete list of capabilities. To add one: ActionCatalog spec + a handler here.</summary>
    internal static class Handlers
    {
        public static IReadOnlyList<IActionHandler> Create(AppConfig cfg)
        {
            var resolver = new AppResolver(cfg);
            return new IActionHandler[]
            {
                new ApplicationActions(resolver),
                new WindowActions(resolver),
                new DesktopActions(),
                new LocationActions(cfg, resolver),
                new SystemActions(),
                new WebActions(cfg, resolver),
                new BluetoothActions(),
                new PortActions(),
            };
        }
    }
}
