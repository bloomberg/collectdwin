using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Threading;
using System.Diagnostics;
using NLog;

namespace BloombergFLP.CollectdWin
{
    internal class MetricsCollector
    {
        private const int MaxQueueSize = 30000;
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly Aggregator _aggregator;
        private readonly int _interval;
        private readonly Queue<MetricValue> _metricValueQueue;
        private readonly IList<IMetricsPlugin> _plugins;
        private readonly Object _queueLock;
        private readonly int _timeout;
        private Thread _aggregatorThread;
        private Thread _readThread;
        private bool _runAggregatorThread;
        private bool _runReadThread, _runWriteThread;
        private Thread _writeThread;

        public MetricsCollector()
        {
            var config = ConfigurationManager.GetSection("CollectdWinConfig") as CollectdWinConfig;
            if (config == null)
            {
                Logger.Error("Cannot get configuration section");
                return;
            }

            _runReadThread = false;
            _runWriteThread = false;

            var registry = new PluginRegistry();
            _plugins = registry.CreatePlugins();

            _interval = config.GeneralSettings.Interval;
            if (_interval <= 10)
                _interval = 10;

            _timeout = config.GeneralSettings.Timeout;
            if (_timeout <= _interval)
                _timeout = _interval*3;

            bool storeRates = config.GeneralSettings.StoreRates;

            _aggregator = new Aggregator(_timeout, storeRates);

            _metricValueQueue = new Queue<MetricValue>();
            _queueLock = new Object();
        }

        public void ConfigureAll()
        {
            Logger.Trace("ConfigureAll() begin");
            foreach (IMetricsPlugin plugin in _plugins)
                plugin.Configure();
            Logger.Trace("ConfigureAll() return");
        }

        public void StartAll()
        {
            Logger.Trace("StartAll() begin");
            foreach (IMetricsPlugin plugin in _plugins)
                plugin.Start();

            _runWriteThread = true;
            _writeThread = new Thread(WriteThreadProc);
            _writeThread.Start();

            _runReadThread = true;
            _readThread = new Thread(ReadThreadProc);
            _readThread.Start();

            _runAggregatorThread = true;
            _aggregatorThread = new Thread(AggregatorThreadProc);
            _aggregatorThread.Start();
            Logger.Trace("StartAll() return");
        }

        public void StopAll()
        {
            Logger.Trace("StopAll() begin");
            _runReadThread = false;
            _runWriteThread = false;
            _runAggregatorThread = false;

            _readThread.Interrupt();
            _writeThread.Interrupt();
            _aggregatorThread.Interrupt();

            foreach (IMetricsPlugin plugin in _plugins)
                plugin.Stop();
            Logger.Trace("StopAll() end");
        }

        private void ReadThreadProc()
        {
            Logger.Trace("ReadThreadProc() begin");
            int numMetricsDropped = 0;
            while (_runReadThread)
            {
                try
                {
                    foreach (IMetricsPlugin plugin in _plugins)
                    {
                        var readPlugin = plugin as IMetricsReadPlugin;
                        if (readPlugin == null)
                        {
                            // skip if plugin is not a readplugin, it might be a writeplugin
                            continue;
                        }
                        IList<MetricValue> metricValues = readPlugin.Read();
                        if (metricValues == null || !metricValues.Any())
                            continue;
                        lock (_queueLock)
                        {
                            foreach (MetricValue metric in metricValues)
                            {
                                _metricValueQueue.Enqueue(metric);
                                while (_metricValueQueue.Count >= MaxQueueSize)
                                {
                                    // When queue size grows above the Max limit, 
                                    // old entries are removed
                                    _metricValueQueue.Dequeue();
                                    if ((++numMetricsDropped%1000) == 0)
                                    {
                                        Logger.Error(
                                            "Number of metrics dropped : {0}",
                                            numMetricsDropped);
                                    }
                                }
                            }
                        }
                    }
                    Thread.Sleep(_interval*1000);
                }
                catch (Exception ex)
                {
                    //Get a StackTrace object for the exception
                    StackTrace st = new StackTrace(ex, true);

                    Logger.Error("ReadThreadProc() got exception : {0}, {1}", ex.ToString(), st.ToString());
                }
            }
            Logger.Trace("ReadThreadProc() return");
        }

        private void WriteThreadProc()
        {
            Logger.Trace("WriteThreadProc() begin");
            bool needToFlush = false;
            while (_runWriteThread)
            {
                try
                {
                    while (_metricValueQueue.Count > 0)
                    {
                        MetricValue metricValue = null;
                        lock (_queueLock)
                        {
                            if (_metricValueQueue.Count > 0)
                                metricValue = _metricValueQueue.Dequeue();
                        }
                        if (metricValue != null)
                        {
                            needToFlush = true;
                            metricValue.Interval = _interval;

                            _aggregator.Aggregate(ref metricValue);

                            foreach (IMetricsPlugin plugin in _plugins)
                            {
                                var writePlugin = plugin as IMetricsWritePlugin;
                                if (writePlugin == null)
                                {
                                    // skip if plugin is not a writeplugin, it might be a readplugin
                                    continue;
                                }
                                writePlugin.Write(metricValue);
                            }
                        }
                    }
                    if (needToFlush)
                    {
                        needToFlush = false;
                        foreach (IMetricsPlugin plugin in _plugins)
                        {
                            var writePlugin = plugin as IMetricsWritePlugin;
                            if (writePlugin != null)
                            {
                                // flush only if it is a Write plugin                    
                                writePlugin.Flush();
                            }
                        }
                    }
                    if (_metricValueQueue.Count <= 0)
                    {
                        Thread.Sleep(1000);
                    }
                }
                catch (Exception exp)
                {
                    Logger.Error("WriteThreadProc() got exception : ", exp);
                }
            }
            Logger.Trace("WriteThreadProc() return");
        }

        private void AggregatorThreadProc()
        {
            Logger.Trace("AggregatorThreadProc() begin");
            while (_runAggregatorThread)
            {
                try
                {
                    _aggregator.RemoveExpiredEntries();
                    Thread.Sleep(_timeout*1000);
                }
                catch (Exception exp)
                {
                    Logger.Error("AggregatorThreadProc() got exception : ", exp);
                }
            }
            Logger.Trace("AggregatorThreadProc() return");
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