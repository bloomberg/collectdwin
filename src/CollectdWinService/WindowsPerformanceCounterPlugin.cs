using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using NLog;

namespace BloombergFLP.CollectdWin
{
    internal struct Metric
    {
        public string Category;
        public string CollectdPlugin, CollectdPluginInstance, CollectdType, CollectdTypeInstance;
        public string CounterName;
        public IList<PerformanceCounter> Counters;
        public string Instance;
        public uint ScaleDownFactor;
        public uint ScaleUpFactor;
    }

    internal class WindowsPerformanceCounterPlugin : IMetricsReadPlugin
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly IList<Metric> _metrics;
        private string _hostName;

        public WindowsPerformanceCounterPlugin()
        {
            _metrics = new List<Metric>();
        }

        public void Configure()
        {
            var config =
                ConfigurationManager.GetSection("WindowsPerformanceCounter") as WindowsPerformanceCounterPluginConfig;
            if (config == null)
            {
                throw new Exception("Cannot get configuration section : WindowsPerformanceCounter");
            }

            _hostName = Util.GetHostName();

            _metrics.Clear();

            foreach (WindowsPerformanceCounterPluginConfig.CounterConfig counter in config.Counters)
            {
                if (counter.Instance == "*")
                {
                    var cat = new PerformanceCounterCategory(counter.Category);
                    string[] instances = cat.GetInstanceNames();
                    foreach (string instance in instances)
                    {
                        // Replace collectd_plugin_instance with the Instance got from counter
                        AddPerformanceCounter(counter.Category, counter.Name,
                            instance, counter.ScaleUpFactor,
                            counter.ScaleDownFactor, counter.CollectdPlugin,
                            instance, counter.CollectdType,
                            counter.CollectdTypeInstance);
                    }
                }
                else
                {
                    AddPerformanceCounter(counter.Category, counter.Name,
                        counter.Instance, counter.ScaleUpFactor,
                        counter.ScaleDownFactor, counter.CollectdPlugin,
                        counter.CollectdPluginInstance, counter.CollectdType,
                        counter.CollectdTypeInstance);
                }
            }
            Logger.Info("WindowsPerformanceCounter plugin configured");
        }

        public void Start()
        {
            Logger.Info("WindowsPerformanceCounter plugin started");
        }

        public void Stop()
        {
            Logger.Info("WindowsPerformanceCounter plugin stopped");
        }

        public IList<MetricValue> Read()
        {
            var metricValueList = new List<MetricValue>();
            foreach (Metric metric in _metrics)
            {
                var vals = new List<double>();
                var missingInstances = new List<PerformanceCounter>();
                foreach (PerformanceCounter ctr in metric.Counters)
                {
                    double val;
                    try
                    {
                         val = ctr.NextValue();
                    }
                    catch (InvalidOperationException)
                    {
                        // The instance is gone
                        missingInstances.Add(ctr);
                        continue;
                    }
                    if (metric.ScaleUpFactor > 0)
                    {
                        val = val*metric.ScaleUpFactor;
                    }
                    else
                    {
                        if (metric.ScaleDownFactor > 0)
                        {
                            val = val/metric.ScaleDownFactor;
                        }
                    }
                    vals.Add(val);
                }

                foreach (PerformanceCounter missingInstance in missingInstances)
                {
                    string logstr =
                        string.Format(
                            "Category:{0} - Instance:{1} - counter:{2} - ScaleUpFactor:{3} - ScaleDownFactor:{4} -  CollectdPlugin:{5} - CollectdPluginInstance:{6} - CollectdType:{7} - CollectdTypeInstance:{8}",
                            metric.Category, metric.Instance, metric.CounterName, metric.ScaleUpFactor, metric.ScaleDownFactor, metric.CollectdPlugin, metric.CollectdPluginInstance,
                            metric.CollectdType, metric.CollectdTypeInstance);
                    Logger.Info("Removed Performance COUNTER : {0}", logstr);
                    metric.Counters.Remove(missingInstance);
                }

                var metricValue = new MetricValue
                {
                    HostName = _hostName,
                    PluginName = metric.CollectdPlugin,
                    PluginInstanceName = metric.CollectdPluginInstance,
                    TypeName = metric.CollectdType,
                    TypeInstanceName = metric.CollectdTypeInstance,
                    Values = vals.ToArray()
                };

                TimeSpan t = DateTime.UtcNow - new DateTime(1970, 1, 1);
                double epoch = t.TotalMilliseconds/1000;
                metricValue.Epoch = Math.Round(epoch, 3);

                metricValueList.Add(metricValue);
            }
            return (metricValueList);
        }

        private void AddPerformanceCounter(string category, string names, string instance, uint scaleUpFactor,
            uint scaleDownFactor, string collectdPlugin, string collectdPluginInstance, string collectdType,
            string collectdTypeInstance)
        {
            string logstr =
                string.Format(
                    "Category:{0} - Instance:{1} - counter:{2} - ScaleUpFactor:{3} - ScaleDownFactor:{4} -  CollectdPlugin:{5} - CollectdPluginInstance:{6} - CollectdType:{7} - CollectdTypeInstance:{8}",
                    category, instance, names, scaleUpFactor, scaleDownFactor, collectdPlugin, collectdPluginInstance,
                    collectdType, collectdTypeInstance);

            try
            {
                var metric = new Metric();
                string[] counterList = names.Split(',');
                metric.Counters = new List<PerformanceCounter>();
                foreach (string ctr in counterList)
                    metric.Counters.Add(new PerformanceCounter(category, ctr.Trim(), instance));
                metric.Category = category;
                metric.Instance = instance;
                metric.CounterName = names;
                metric.ScaleUpFactor = scaleUpFactor;
                metric.ScaleDownFactor = scaleDownFactor;
                metric.CollectdPlugin = collectdPlugin;
                metric.CollectdPluginInstance = collectdPluginInstance;
                metric.CollectdType = collectdType;
                metric.CollectdTypeInstance = collectdTypeInstance;

                _metrics.Add(metric);
                Logger.Info("Added Performance COUNTER : {0}", logstr);
            }
            catch (Exception exp)
            {
                Logger.Error("Got exception : {0}, while adding performance counter: {1}", exp, logstr);
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