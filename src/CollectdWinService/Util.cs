using System;
using System.Configuration;
using System.Net;
using System.Net.Sockets;
using NLog;

namespace BloombergFLP.CollectdWin
{
    public static class Util
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public static double GetNow()
        {
            TimeSpan t = DateTime.UtcNow - new DateTime(1970, 1, 1);
            double epoch = t.TotalMilliseconds/1000;
            double now = Math.Round(epoch, 3);
            return (now);
        }

        public static string GetHostName()
        {
            string hostname = Environment.MachineName.ToLower();
            try
            {
                hostname = Dns.GetHostEntry("localhost").HostName.ToLower();
            }
            catch (SocketException)
            {
                Logger.Warn("Unable to resolve hostname, using MachineName: {0}", hostname);
            }
            var generalConfig = ConfigurationManager.GetSection("CollectdWinConfig") as CollectdWinConfig;
            if (generalConfig == null)
            {
                Logger.Error("Cannot get configuration section");
                return hostname;
            }
            if (! (String.IsNullOrEmpty(generalConfig.GeneralSettings.HostName)))
            {
                hostname = generalConfig.GeneralSettings.HostName;
            }
            return hostname;
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