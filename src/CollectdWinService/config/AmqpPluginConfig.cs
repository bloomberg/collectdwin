using System.Configuration;

namespace BloombergFLP.CollectdWin
{
    internal class AmqpPluginConfig : ConfigurationSection
    {
        [ConfigurationProperty("Publish", IsRequired = false)]
        public PublishConfig Publish
        {
            get { return (PublishConfig) base["Publish"]; }
            set { base["Publish"] = value; }
        }

        public static AmqpPluginConfig GetConfig()
        {
            return (AmqpPluginConfig) ConfigurationManager.GetSection("Amqp") ?? new AmqpPluginConfig();
        }

        public sealed class PublishConfig : ConfigurationElement
        {
            [ConfigurationProperty("Name", IsRequired = true)]
            public string Name
            {
                get { return (string) base["Name"]; }
                set { base["Name"] = value; }
            }

            [ConfigurationProperty("Host", IsRequired = false)]
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

            [ConfigurationProperty("VirtualHost", IsRequired = false)]
            public string VirtualHost
            {
                get { return (string) base["VirtualHost"]; }
                set { base["VirtualHost"] = value; }
            }

            [ConfigurationProperty("User", IsRequired = false)]
            public string User
            {
                get { return (string) base["User"]; }
                set { base["User"] = value; }
            }

            [ConfigurationProperty("Password", IsRequired = false)]
            public string Password
            {
                get { return (string) base["Password"]; }
                set { base["Password"] = value; }
            }

            [ConfigurationProperty("Exchange", IsRequired = false)]
            public string Exchange
            {
                get { return (string) base["Exchange"]; }
                set { base["Exchange"] = value; }
            }

            [ConfigurationProperty("RoutingKeyPrefix", IsRequired = false)]
            public string RoutingKeyPrefix
            {
                get { return (string) base["RoutingKeyPrefix"]; }
                set { base["RoutingKeyPrefix"] = value; }
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