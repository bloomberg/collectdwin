using System.Configuration;

namespace BloombergFLP.CollectdWin
{
    internal class WriteHttpPluginConfig : ConfigurationSection
    {
        [ConfigurationProperty("Nodes", IsRequired = false)]
        [ConfigurationCollection(typeof (WriteHttpNodeConfigCollection), AddItemName = "Node")]
        public WriteHttpNodeConfigCollection Nodes
        {
            get { return (WriteHttpNodeConfigCollection) base["Nodes"]; }
            set { base["Nodes"] = value; }
        }

        public static WriteHttpPluginConfig GetConfig()
        {
            return (WriteHttpPluginConfig) ConfigurationManager.GetSection("WriteHttp") ?? new WriteHttpPluginConfig();
        }

        public sealed class WriteHttpNodeConfig : ConfigurationElement
        {
            [ConfigurationProperty("Name", IsRequired = true)]
            public string Name
            {
                get { return (string) base["Name"]; }
                set { base["Name"] = value; }
            }

            [ConfigurationProperty("Url", IsRequired = true)]
            public string Url
            {
                get { return (string) base["Url"]; }
                set { base["Url"] = value; }
            }

            [ConfigurationProperty("Timeout", IsRequired = true)]
            public int Timeout
            {
                get { return (int) base["Timeout"]; }
                set { base["Timeout"] = value; }
            }

            [ConfigurationProperty("BatchSize", IsRequired = true)]
            public int BatchSize
            {
                get { return (int) base["BatchSize"]; }
                set { base["BatchSize"] = value; }
            }

            [ConfigurationProperty("MaxIdleTime", IsRequired = false)]
            public int MaxIdleTime
            {
                get { return (int) base["MaxIdleTime"]; }
                set { base["MaxIdleTime"] = value; }
            }

            [ConfigurationProperty("UserName", IsRequired = false)]
            public string UserName
            {
                get { return (string)base["UserName"]; }
                set { base["UserName"] = value; }
            }

            [ConfigurationProperty("Password", IsRequired = false)]
            public string Password
            {
                get { return (string)base["Password"]; }
                set { base["Password"] = value; }
            }

            [ConfigurationProperty("SafeCharsRegex", IsRequired = false)]
            public string SafeCharsRegex
            {
                get { return (string)base["SafeCharsRegex"]; }
                set { base["SafeCharsRegex"] = value; }
            }

            [ConfigurationProperty("ReplaceWith", IsRequired = false)]
            public string ReplaceWith
            {
                get { return (string)base["ReplaceWith"]; }
                set { base["ReplaceWith"] = value; }
            }

            [ConfigurationProperty("Proxy", IsRequired = true)]
            public ProxyConfig Proxy
            {
                get { return (ProxyConfig) base["Proxy"]; }
                set { base["Proxy"] = value; }
            }

            public sealed class ProxyConfig : ConfigurationElement
            {
                [ConfigurationProperty("Enable", IsRequired = true)]
                public bool Enable
                {
                    get { return (bool) base["Enable"]; }
                    set { base["Enable"] = value; }
                }

                [ConfigurationProperty("Url", IsRequired = true)]
                public string Url
                {
                    get { return (string) base["Url"]; }
                    set { base["Url"] = value; }
                }
            }
        }

        public sealed class WriteHttpNodeConfigCollection : ConfigurationElementCollection
        {
            protected override ConfigurationElement CreateNewElement()
            {
                return new WriteHttpNodeConfig();
            }

            protected override object GetElementKey(ConfigurationElement element)
            {
                var nodeConfig = (WriteHttpNodeConfig) element;
                return (nodeConfig.Name);
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
