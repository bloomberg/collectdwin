using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using NLog;

namespace BloombergFLP.CollectdWin
{
    internal class PluginRegistry
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private readonly Dictionary<string, string> _registry = new Dictionary<string, string>();

        public PluginRegistry()
        {
            var config = ConfigurationManager.GetSection("CollectdWinConfig") as CollectdWinConfig;
            if (config == null)
            {
                Logger.Error("Cannot get configuration section");
                return;
            }
            foreach (
                CollectdWinConfig.PluginConfig pluginConfig in
                    config.PluginRegistry.Cast<CollectdWinConfig.PluginConfig>()
                        .Where(pluginConfig => pluginConfig.Enable))
            {
                _registry[pluginConfig.Name] = pluginConfig.Class;
            }
        }

        public IList<IMetricsPlugin> CreatePlugins()
        {
            IList<IMetricsPlugin> plugins = new List<IMetricsPlugin>();
            foreach (var entry in _registry)
            {
                Type classType = Type.GetType(entry.Value);
                if (classType == null)
                {
                    Logger.Error("Cannot create plugin:{0}, class:{1}", entry.Key, entry.Value);
                    continue;
                }
                var plugin = (IMetricsPlugin) Activator.CreateInstance(classType);
                plugins.Add(plugin);
            }
            return (plugins);
        }
    }
}

// ----------------------------------------------------------------------------
// Copyright (C) 2015 Bloomberg Finance L.P.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
// ----------------------------- END-OF-FILE ----------------------------------