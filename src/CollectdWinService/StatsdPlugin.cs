using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Threading;
using NLog;

namespace BloombergFLP.CollectdWin
{
    internal class StatsdPlugin : IMetricsReadPlugin
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private bool _delCounters;
        private bool _delGauges, _delSets;
        private bool _delTimers;
        private float[] _percentiles;
        private int _port;
        private bool _running;
        private StatsdAggregator _statsdAggregator;
        private StatsdListener _statsdListener;
        private Thread _statsdThread;
        private bool _timerCount;
        private bool _timerLower;
        private bool _timerSum;
        private bool _timerUpper;

        public StatsdPlugin()
        {
            _running = false;
        }

        public void Configure()
        {
            var config = ConfigurationManager.GetSection("CollectdWinConfig") as CollectdWinConfig;
            if (config == null)
            {
                throw new Exception("Cannot get configuration section : CollectdWinConfig");
            }

            _port = config.Statsd.Port;

            _delCounters = config.Statsd.DeleteCache.Counters;
            _delTimers = config.Statsd.DeleteCache.Timers;
            _delGauges = config.Statsd.DeleteCache.Gauges;
            _delSets = config.Statsd.DeleteCache.Sets;

            _timerLower = config.Statsd.Timer.Lower;
            _timerUpper = config.Statsd.Timer.Upper;
            _timerSum = config.Statsd.Timer.Sum;
            _timerCount = config.Statsd.Timer.Count;
            _percentiles =
                (from CollectdWinConfig.StatsdConfig.PercentileConfig percentileConfig in
                    config.Statsd.Timer.Percentiles
                    select percentileConfig.Value).ToArray();

            _statsdAggregator = new StatsdAggregator(_delCounters, _delTimers, _delGauges, _delSets, _timerLower,
                _timerUpper,
                _timerSum, _timerCount, _percentiles);
            Logger.Info("Statsd plugin configured");
        }

        public void Start()
        {
            if (_running)
                return;

            _statsdListener = new StatsdListener(_port, HandleMessage);
            _statsdThread = new Thread(_statsdListener.Start);
            _statsdThread.Start();

            _running = true;
            Logger.Info("Statsd plugin started");
        }

        public void Stop()
        {
            if (!_running)
                return;
            _statsdListener.Stop();
            _statsdThread.Interrupt();

            _running = false;
            Logger.Info("Statsd plugin stopped");
        }

        public IList<MetricValue> Read()
        {
            return (_statsdAggregator.Read());
        }

        public void HandleMessage(string message)
        {
            IList<StatsdMetric> metrics = StatsdMetricParser.Parse(message);
            foreach (StatsdMetric metric in metrics)
            {
                _statsdAggregator.AddMetric(metric);
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