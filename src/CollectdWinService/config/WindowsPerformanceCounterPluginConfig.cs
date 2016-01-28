using System.Configuration;

namespace BloombergFLP.CollectdWin
{
    internal class WindowsPerformanceCounterPluginConfig : ConfigurationSection
    {
        [ConfigurationProperty("RefreshInstancesConfiguration", IsRequired = true)]
        public RefreshInstancesConfigurationConfig RefreshInstancesConfiguration
        {
            get { return (RefreshInstancesConfigurationConfig)base["RefreshInstancesConfiguration"]; }
            set { base["RefreshInstancesConfiguration"] = value; }
        }

        public sealed class RefreshInstancesConfigurationConfig : ConfigurationElement
        {
            [ConfigurationProperty("Enable", IsRequired = true)]
            public bool Enable
            {
                get { return (bool)base["Enable"]; }
                set { base["Enable"] = value; }
            }

            [ConfigurationProperty("Interval", IsRequired = true)]
            public int Interval
            {
                get { return (int)base["Interval"]; }
                set { base["Interval"] = value; }
            }
        }

        [ConfigurationProperty("Counters", IsRequired = false)]
        [ConfigurationCollection(typeof (CounterConfigCollection), AddItemName = "Counter")]
        public CounterConfigCollection Counters
        {
            get { return (CounterConfigCollection) base["Counters"]; }
            set { base["Counters"] = value; }
        }

        public static WindowsPerformanceCounterPluginConfig GetConfig()
        {
            return
                (WindowsPerformanceCounterPluginConfig)
                    ConfigurationManager.GetSection("WindowsPerformanceCounterPlugin") ??
                new WindowsPerformanceCounterPluginConfig();
        }

        public sealed class CounterConfig : ConfigurationElement
        {
            [ConfigurationProperty("Transformer", IsRequired = false, DefaultValue = "")]
            public string Transformer
            {
                get { return (string)base["Transformer"]; }
                set { base["Transformer"] = value; }
            }

            [ConfigurationProperty("TransformerParameters", IsRequired = false)]
            public string TransformerParameters
            {
                get { return (string)base["TransformerParameters"]; }
                set { base["TransformerParameters"] = value; }
            }

            [ConfigurationProperty("Category", IsRequired = true)]
            public string Category
            {
                get { return (string) base["Category"]; }
                set { base["Category"] = value; }
            }

            [ConfigurationProperty("Name", IsRequired = true)]
            public string Name
            {
                get { return (string) base["Name"]; }
                set { base["Name"] = value; }
            }


            [ConfigurationProperty("Instance", IsRequired = false)]
            public string Instance
            {
                get { return (string) base["Instance"]; }
                set { base["Instance"] = value; }
            }

            [ConfigurationProperty("CollectdPlugin", IsRequired = true)]
            public string CollectdPlugin
            {
                get { return (string) base["CollectdPlugin"]; }
                set { base["CollectdPlugin"] = value; }
            }

            [ConfigurationProperty("CollectdPluginInstance", IsRequired = false)]
            public string CollectdPluginInstance
            {
                get { return (string) base["CollectdPluginInstance"]; }
                set { base["CollectdPluginInstance"] = value; }
            }

            [ConfigurationProperty("CollectdType", IsRequired = true)]
            public string CollectdType
            {
                get { return (string) base["CollectdType"]; }
                set { base["CollectdType"] = value; }
            }

            [ConfigurationProperty("CollectdTypeInstance", IsRequired = true)]
            public string CollectdTypeInstance
            {
                get { return (string) base["CollectdTypeInstance"]; }
                set { base["CollectdTypeInstance"] = value; }
            }
        }

        public sealed class CounterConfigCollection : ConfigurationElementCollection
        {
            protected override ConfigurationElement CreateNewElement()
            {
                return new CounterConfig();
            }

            protected override object GetElementKey(ConfigurationElement element)
            {
                var counterConfig = (CounterConfig) element;
                return (counterConfig.CollectdPlugin + "_" + counterConfig.CollectdPluginInstance + "_" + counterConfig.CollectdType + "_" + counterConfig.CollectdTypeInstance);
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