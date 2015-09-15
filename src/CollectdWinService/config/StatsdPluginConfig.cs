using System;
using System.Configuration;

namespace BloombergFLP.CollectdWin
{
    internal class StatsdPluginConfig : ConfigurationSection
    {
        [ConfigurationProperty("Server", IsRequired = true)]
        public ServerConfig Server
        {
            get { return (ServerConfig) base["Server"]; }
            set { base["Server"] = value; }
        }

        [ConfigurationProperty("DeleteCache", IsRequired = true)]
        public DeleteCacheConfig DeleteCache
        {
            get { return (DeleteCacheConfig) base["DeleteCache"]; }
            set { base["DeleteCache"] = value; }
        }

        [ConfigurationProperty("Timer", IsRequired = true)]
        public TimerConfig Timer
        {
            get { return (TimerConfig) base["Timer"]; }
            set { base["Timer"] = value; }
        }

        public static StatsdPluginConfig GetConfig()
        {
            return (StatsdPluginConfig) ConfigurationManager.GetSection("Statsd") ?? new StatsdPluginConfig();
        }

        public sealed class DeleteCacheConfig : ConfigurationElement
        {
            [ConfigurationProperty("Counters", IsRequired = true)]
            public bool Counters
            {
                get { return (bool) base["Counters"]; }
                set { base["Counters"] = value; }
            }

            [ConfigurationProperty("Timers", IsRequired = true)]
            public bool Timers
            {
                get { return (bool) base["Timers"]; }
                set { base["Timers"] = value; }
            }

            [ConfigurationProperty("Gauges", IsRequired = true)]
            public bool Gauges
            {
                get { return (bool) base["Gauges"]; }
                set { base["Gauges"] = value; }
            }

            [ConfigurationProperty("Sets", IsRequired = true)]
            public bool Sets
            {
                get { return (bool) base["Sets"]; }
                set { base["Sets"] = value; }
            }
        }

        public class PercentileCollection : ConfigurationElementCollection
        {
            protected override ConfigurationElement CreateNewElement()
            {
                return new PercentileConfig();
            }

            protected override object GetElementKey(ConfigurationElement element)
            {
                return (((PercentileConfig) element).UniqueId);
            }
        }

        public sealed class PercentileConfig : ConfigurationElement
        {
            public PercentileConfig()
            {
                UniqueId = Guid.NewGuid();
            }

            internal Guid UniqueId { get; set; }

            [ConfigurationProperty("Value", IsRequired = true)]
            public float Value
            {
                get { return (float) base["Value"]; }
                set { base["Value"] = value; }
            }
        }

        public sealed class ServerConfig : ConfigurationElement
        {
            [ConfigurationProperty("Host", IsRequired = true)]
            public string Host
            {
                get { return (string) base["Host"]; }
                set { base["Host"] = value; }
            }

            [ConfigurationProperty("Port", IsRequired = true)]
            public int Port
            {
                get { return (int) base["Port"]; }
                set { base["Port"] = value; }
            }
        }

        public sealed class TimerConfig : ConfigurationElement
        {
            [ConfigurationProperty("Lower", IsRequired = true)]
            public bool Lower
            {
                get { return (bool) base["Lower"]; }
                set { base["Lower"] = value; }
            }

            [ConfigurationProperty("Upper", IsRequired = true)]
            public bool Upper
            {
                get { return (bool) base["Upper"]; }
                set { base["Upper"] = value; }
            }

            [ConfigurationProperty("Sum", IsRequired = true)]
            public bool Sum
            {
                get { return (bool) base["Sum"]; }
                set { base["Sum"] = value; }
            }

            [ConfigurationProperty("Count", IsRequired = true)]
            public bool Count
            {
                get { return (bool) base["Count"]; }
                set { base["Count"] = value; }
            }

            [ConfigurationProperty("Percentiles", IsRequired = false)]
            [ConfigurationCollection(typeof (PercentileCollection), AddItemName = "Percentile")]
            public PercentileCollection Percentiles
            {
                get { return (PercentileCollection) base["Percentiles"]; }
                set { base["Percentiles"] = value; }
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