using System;
using System.Collections.Generic;
using System.Configuration;

namespace BloombergFLP.CollectdWin
{

    internal class CollectdWinConfigHelper
    {
        public static IDictionary<string, string> getMetaData()
        {
            IDictionary<string, string> metaData = new Dictionary<string, string>();

            var coreConfig = ConfigurationManager.GetSection("CollectdWinConfig") as CollectdWinConfig;
            if (coreConfig == null)
            {
                throw new Exception("Cannot get configuration section : CollectdWinConfig");
            }
            foreach (CollectdWinConfig.TagConfig tagConfig in coreConfig.MetaData)
            {
                metaData[tagConfig.Name] = tagConfig.Value;
            }
            return (metaData);
        }
    }

    internal class CollectdWinConfig : ConfigurationSection
    {
        [ConfigurationProperty("GeneralSettings", IsRequired = true)]
        public GeneralSettingsConfig GeneralSettings
        {
            get { return (GeneralSettingsConfig) base["GeneralSettings"]; }
            set { base["GeneralSettings"] = value; }
        }

        [ConfigurationProperty("Plugins", IsRequired = true)]
        [ConfigurationCollection(typeof (PluginCollection), AddItemName = "Plugin")]
        public PluginCollection Plugins
        {
            get { return (PluginCollection) base["Plugins"]; }
            set { base["Plugins"] = value; }
        }

        [ConfigurationProperty("MetaData", IsRequired = false)]
        [ConfigurationCollection(typeof(TagCollection), AddItemName = "Tag")]
        public TagCollection MetaData
        {
            get { return (TagCollection)base["MetaData"]; }
            set { base["MetaData"] = value; }
        }

        public static CollectdWinConfig GetConfig()
        {
            return (CollectdWinConfig) ConfigurationManager.GetSection("CollectdWinConfig") ?? new CollectdWinConfig();
        }

        public sealed class GeneralSettingsConfig : ConfigurationElement
        {
            [ConfigurationProperty("Interval", IsRequired = true)]
            public int Interval
            {
                get { return (int) base["Interval"]; }
                set { base["Interval"] = value; }
            }

            [ConfigurationProperty("Timeout", IsRequired = true)]
            public int Timeout
            {
                get { return (int) base["Timeout"]; }
                set { base["Timeout"] = value; }
            }

            [ConfigurationProperty("StoreRates", IsRequired = true)]
            public bool StoreRates
            {
                get { return (bool) base["StoreRates"]; }
                set { base["StoreRates"] = value; }
            }

            [ConfigurationProperty("HostName", IsRequired = false)]
            public string HostName
            {
                get { return (string) base["HostName"]; }
                set { base["HostName"] = value; }
            }
        }

        public sealed class PluginCollection : ConfigurationElementCollection
        {
            protected override ConfigurationElement CreateNewElement()
            {
                return new PluginConfig();
            }

            protected override object GetElementKey(ConfigurationElement element)
            {
                return (((PluginConfig) element).UniqueId);
            }
        }

        public sealed class PluginConfig : ConfigurationElement
        {
            public PluginConfig()
            {
                UniqueId = Guid.NewGuid();
            }

            internal Guid UniqueId { get; set; }

            [ConfigurationProperty("Name", IsRequired = true)]
            public string Name
            {
                get { return (string) base["Name"]; }
                set { base["Name"] = value; }
            }

            [ConfigurationProperty("Class", IsRequired = true)]
            public string Class
            {
                get { return (string) base["Class"]; }
                set { base["Class"] = value; }
            }

            [ConfigurationProperty("Enable", IsRequired = true)]
            public bool Enable
            {
                get { return (bool) base["Enable"]; }
                set { base["Enable"] = value; }
            }
        }

        public sealed class TagCollection : ConfigurationElementCollection
        {
            protected override ConfigurationElement CreateNewElement()
            {
                return new TagConfig();
            }

            protected override object GetElementKey(ConfigurationElement element)
            {
                return (((TagConfig)element).UniqueId);
            }
        }
        public sealed class TagConfig : ConfigurationElement
        {
            public TagConfig()
            {
                UniqueId = Guid.NewGuid();
            }

            internal Guid UniqueId { get; set; }

            [ConfigurationProperty("Name", IsRequired = true)]
            public string Name
            {
                get { return (string)base["Name"]; }
                set { base["Name"] = value; }
            }

            [ConfigurationProperty("Value", IsRequired = true)]
            public string Value
            {
                get { return (string)base["Value"]; }
                set { base["Value"] = value; }
            }           
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