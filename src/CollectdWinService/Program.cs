using System;
using System.Configuration;
using System.ServiceProcess;
using NLog;

namespace BloombergFLP.CollectdWin
{
    public static class Program
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        ///     The main entry point for the application.
        /// </summary>
        public static void Main(string[] args)
        {
            var config = ConfigurationManager.GetSection("CollectdWinConfig") as CollectdWinConfig;
            if (config == null)
            {
                Logger.Error("Main(): cannot get configuration section");
                return;
            }

            var collectdWinService = new CollectdWinService();

            if (Array.Find(args, s => s.Equals(@"/console")) != null)
            {
                // run as a console application for testing and debugging purpose
                collectdWinService.StartService();
            }
            else
            {
                // run as a windows service
                ServiceBase[] servicesToRun = {collectdWinService};
                ServiceBase.Run(servicesToRun);
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