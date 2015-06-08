using System.ServiceProcess;
using NLog;

namespace BloombergFLP.CollectdWin
{
    public class CollectdWinService : ServiceBase
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private MetricsCollector _metricsCollector;

        public CollectdWinService()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            StartService(args);
        }

        protected override void OnStop()
        {
            StopService();
        }

        private void InitializeComponent()
        {
            ServiceName = "Bloomberg Metrics Collector Service";
        }

        // public accessibility for running as a console application
        public virtual void StartService(params string[] args)
        {
            Logger.Trace("StartService() begin");
            _metricsCollector = new MetricsCollector();
            _metricsCollector.ConfigureAll();
            _metricsCollector.StartAll();
            Logger.Trace("StartService() return");
        }

        // public accessibility for running as a console application
        public virtual void StopService()
        {
            Logger.Trace("StopService() begin");
            _metricsCollector.StopAll();
            Logger.Trace("StopService() return");
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