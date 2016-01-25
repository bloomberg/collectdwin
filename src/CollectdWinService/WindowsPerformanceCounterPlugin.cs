using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Timers;
using Microsoft.VisualBasic.Devices;
using NLog;

namespace BloombergFLP.CollectdWin
{
    internal class Helper
    {
        public delegate double TransformFunction(double value);

        public static double TotalPhysicalMemoryInBytes = new ComputerInfo().TotalPhysicalMemory;
        public static double FromMBytesToBytes(double value) { return value * 1024 * 1024; }
        public static double PercentFromFreeSpaceToUsedSpace(double value) { return 100 - value; }
        public static double FromAvailableBytesToUsedMemoryPercent(double value)
        {
            return (TotalPhysicalMemoryInBytes - value) * 100 / TotalPhysicalMemoryInBytes;
        }

        public static List<MetricValue> Transform(List<MetricValue> values, TransformFunction functor)
        {
            foreach (MetricValue value in values)
                for(int i=0; i < value.Values.Length; ++i)
                    value.Values[i] = functor(value.Values[i]);
            return values;
        }

        public static string DictionaryValue(Dictionary<string, object> dict, string key)
        {
            return dict.ContainsKey(key) ? dict[key] as string : null;
        }

    }

    internal interface IMetricGenerator
    {
        bool Configure(Dictionary<string, object> config);
        List<MetricValue> NextValues();
    }

    internal abstract class PerformanceCounterGenerator : IMetricGenerator
    {
        static protected string s_hostName = Util.GetHostName();
        static protected readonly Logger Logger = LogManager.GetCurrentClassLogger();
        public string CounterCategory, CounterName, CounterInstance;
        public string CollectdPlugin, CollectdPluginInstance, CollectdType, CollectdTypeInstance;
    
        public virtual bool Configure(Dictionary<string, object> config)
        {
            CounterCategory = Helper.DictionaryValue(config, "Category");
            CounterName = Helper.DictionaryValue(config, "Name");
            CounterInstance = Helper.DictionaryValue(config, "Instance");
            CollectdPlugin = Helper.DictionaryValue(config, "CollectdPlugin");
            CollectdPluginInstance = Helper.DictionaryValue(config, "CollectdPluginInstance");
            CollectdType = Helper.DictionaryValue(config, "CollectdType");
            CollectdTypeInstance = Helper.DictionaryValue(config, "CollectdTypeInstance");
            return true;
        }

        public abstract List<MetricValue> NextValues();

        public MetricValue getMetricValue(List<double> vals)
        {
            var metricValue = new MetricValue
            {
                HostName = s_hostName,
                PluginName = CollectdPlugin,
                PluginInstanceName = CollectdPluginInstance,
                TypeName = CollectdType,
                TypeInstanceName = CollectdTypeInstance,
                Values = vals.ToArray()
            };

            TimeSpan t = DateTime.UtcNow - new DateTime(1970, 1, 1);
            double epoch = t.TotalMilliseconds / 1000;
            metricValue.Epoch = Math.Round(epoch, 3);
            return metricValue;
        }

    }

    internal class PerformanceCounterCategoryInstancesGenerator : PerformanceCounterGenerator
    {
        private PerformanceCounterCategory _performanceCounterCategory;

        public override bool Configure(Dictionary<string, object> config)
        {
            if (!base.Configure(config))
                return false;
            try
            {
                _performanceCounterCategory = new PerformanceCounterCategory(CounterCategory);
                return true;
            }
            catch (Exception exp)
            {
                Logger.Error("Got exception : {0}, while adding performance counter category: {1}", exp, CounterCategory);
                return false;
            }
        }

        public override List<MetricValue> NextValues()
        {
            var metricValueList = new List<MetricValue>();
            var vals = new List<double>();
            vals.Add(_performanceCounterCategory.GetInstanceNames().Length);
            metricValueList.Add(getMetricValue(vals));
            return metricValueList;
        }
    }

    internal class PerformanceCounterMetricGenerator : PerformanceCounterGenerator
    {
        internal class MetricRetriever
        {
            // indicate the real instance when PerformanceCounterMetricGenerator.CounterInstance='*'
            public string Instance;
            public IList<PerformanceCounter> Counters;
            public List<double> Retrive()
            {
                var vals = new List<double>();
                foreach (PerformanceCounter counter in Counters)
                {
                    double val;
                    try
                    {
                        val = counter.NextValue();
                    }
                    catch (InvalidOperationException)
                    {
                        return null;
                    }
                    vals.Add(val);
                }
                return vals;
            }
        }

        public IList<MetricRetriever> MetricRetrievers;

        private MetricRetriever GetMetricRetriever(string category, string names, string instance)
        {
            string logstr =
                string.Format(
                    "Category:{0} - Instance:{1} - counter:{2}",
                    category, instance, names);
            try
            {
                var metricRetriver = new MetricRetriever();
                metricRetriver.Counters = new List<PerformanceCounter>();
                string[] counterList = names.Split(',');
                foreach (string ctr in counterList)
                    metricRetriver.Counters.Add(new PerformanceCounter(category, ctr.Trim(), instance));
                Logger.Info("Added Performance COUNTER : {0}", logstr);
                return metricRetriver;
            }
            catch (Exception exp)
            {
                Logger.Error("Got exception : {0}, while adding performance counter: {1}", exp, logstr);
                return null;
            }
        }

        public bool Refresh()
        {
            MetricRetrievers = new List<MetricRetriever>();
            if (CounterInstance != null && CounterInstance == "*")
            {
                var cat = new PerformanceCounterCategory(CounterCategory);
                string[] instances = cat.GetInstanceNames();
                foreach (string instance in instances)
                {
                    MetricRetriever metricRetriver = GetMetricRetriever(CounterCategory, CounterName, instance);
                    if (metricRetriver == null)
                        return false;
                    // Replace collectd_plugin_instance with the Instance got from counter
                    metricRetriver.Instance = instance;
                    MetricRetrievers.Add(metricRetriver);
                }
            }
            else
            {
                MetricRetriever metricRetriver = GetMetricRetriever(CounterCategory, CounterName, CounterInstance);
                if (metricRetriver == null)
                    return false;
                MetricRetrievers.Add(metricRetriver);
            }
            return true;
        }

        public override bool Configure(Dictionary<string, object> config)
        {
            if (!base.Configure(config))
                return false;
            return Refresh();
        }

        public override List<MetricValue> NextValues()
        {
            var metricValueList = new List<MetricValue>();
            var missingInstances = new List<MetricRetriever>();
            foreach (MetricRetriever metricRetriver in MetricRetrievers)
            {
                var vals = metricRetriver.Retrive();
                if (vals == null)
                {
                    // The instance is gone
                    missingInstances.Add(metricRetriver);
                }
                else
                {
                    var metricValue = getMetricValue(vals);
                    if (CollectdPluginInstance == null)
                        metricValue.PluginInstanceName = metricRetriver.Instance;
                    metricValueList.Add(metricValue);
                }
            }

            // remove missing instances before return
            foreach (MetricRetriever missingInstance in missingInstances)
            {
                string logstr =
                    string.Format(
                        "Category:{0} - Instance:{1} - counter:{2} - CollectdPlugin:{4} - CollectdPluginInstance:{5} - CollectdType:{6} - CollectdTypeInstance:{7}",
                        CounterCategory, missingInstance.Instance, CounterName, 
                        CollectdPlugin, CollectdPluginInstance, CollectdType, CollectdTypeInstance);
                Logger.Info("Removed Performance COUNTER : {0}", logstr);
                MetricRetrievers.Remove(missingInstance);
            }

            return metricValueList;
        }
    }

    internal class LogicalDiskFreeBytesGenerator : PerformanceCounterMetricGenerator
    {
        public override List<MetricValue> NextValues()
        {
            return Helper.Transform(base.NextValues(), Helper.FromMBytesToBytes);
        }
    }

    internal class LogicalDiskUsedSpacePercentGenerator : PerformanceCounterMetricGenerator
    {
        public override List<MetricValue> NextValues()
        {
            return Helper.Transform(base.NextValues(), Helper.PercentFromFreeSpaceToUsedSpace);
        }
    }

    internal class UsedMemoryPercentGenerator : PerformanceCounterMetricGenerator
    {
        public override List<MetricValue> NextValues()
        {
            return Helper.Transform(base.NextValues(), Helper.FromAvailableBytesToUsedMemoryPercent);
        }
    }

    internal class AveragesGenerator : PerformanceCounterMetricGenerator
    {
        public List<uint> AverageIntervalsInSeconds = new List<uint>();
        public uint MaxIntervalInSeconds;
        public List<List<List<double>>> _samples;
        private System.Threading.Mutex _mutex = new System.Threading.Mutex(); // protect _samples
        private Timer _timer = new Timer(1000);

        ~AveragesGenerator()
        {
            _timer.Dispose();
        }

        private static void OnTakeSample(object source, ElapsedEventArgs e, AveragesGenerator averagesGenerator)
        {
            averagesGenerator.TakeSample();
        }

        private void TakeSample()
        {
            // get a sample for each of MetricRetrievers
            List<List<double>> sample = new List<List<double>>();
            foreach (MetricRetriever metricRetriver in MetricRetrievers)
            {
                var vals = metricRetriver.Retrive();
                if (vals != null)
                    sample.Add(vals);
            }
            _mutex.WaitOne();
            for(int i = 0; i < sample.Count; ++i)
            {
                List<List<double>> sampleList = _samples[i];
                sampleList.Add(sample[i]);
                if (sampleList.Count > MaxIntervalInSeconds)
                    sampleList.RemoveAt(0);
            }
            _mutex.ReleaseMutex();
        }

        public override bool Configure(Dictionary<string, object> config)
        {
            if (!base.Configure(config))
                return false;
            string averageIntervals = Helper.DictionaryValue(config, "AverageIntervals");
            if (averageIntervals == null)
                return false;
            string[] averagesInString = averageIntervals.Split(',');
            foreach (string interval in averagesInString)
            {
                uint val;
                if (!UInt32.TryParse(interval, out val) || val == 0)
                    return false;
                AverageIntervalsInSeconds.Add(val);
            }
            if (AverageIntervalsInSeconds.Count == 0)
                return false;
            AverageIntervalsInSeconds.Sort();
            MaxIntervalInSeconds = AverageIntervalsInSeconds[AverageIntervalsInSeconds.Count-1];
            // init _samples
            _samples = new List<List<List<double>>>();
            foreach (MetricRetriever metricRetriver in MetricRetrievers)
                _samples.Add(new List<List<double>>());
                // take the first sample
            TakeSample();
            // set up sampling timer
            _timer.Elapsed += (sender, e) => OnTakeSample(sender, e, this); ;
            _timer.AutoReset = true;
            _timer.Enabled = true;
            return true;
        }

        public override List<MetricValue> NextValues()
        {
            var metricValueList = new List<MetricValue>();
            _mutex.WaitOne();
            int i = 0;
            foreach (MetricRetriever metricRetriver in MetricRetrievers)
            {
                List<double> average = new List<double>();
                List<List<double>> sampleList = _samples[i]; ++i;
                List<double> sum = new List<double>();
                for (int n = 0; n < sampleList[0].Count; ++n)
                    sum.Add(0);
                uint interval = AverageIntervalsInSeconds[0];
                int intervalCount = 0;
                for (int count=0; count < sampleList.Count; ++count)
                {
                    List<double> sample = sampleList[i];
                    for (int n=0; n < sum.Count; ++n)
                        sum[n] += sample[n];
                    if (count + 1 == interval)
                    {
                        foreach (double s in sum)
                            average.Add(s / interval);
                        ++intervalCount;
                        if (intervalCount < AverageIntervalsInSeconds.Count)
                        {
                            interval = AverageIntervalsInSeconds[intervalCount];
                        }
                    }
                }
                for (; intervalCount < AverageIntervalsInSeconds.Count; ++intervalCount)
                {
                    foreach (double s in sum)
                        average.Add(s / sampleList.Count);
                }

                var metricValue = getMetricValue(average);
                if (CollectdPluginInstance == null)
                    metricValue.PluginInstanceName = metricRetriver.Instance;

                metricValueList.Add(metricValue);
            }
            _mutex.ReleaseMutex();
            return metricValueList;
        }
    }

    internal class WindowsPerformanceCounterPlugin : IMetricsReadPlugin
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly IList<IMetricGenerator> _metricGenerators;
        private bool _reloadConfiguration;
        private DateTime _configurationReloadTime;
        private int _reloadConfigurationInterval;

        public WindowsPerformanceCounterPlugin()
        {
            _metricGenerators = new List<IMetricGenerator>();
        }

        public void Configure()
        {
            var config =
                ConfigurationManager.GetSection("WindowsPerformanceCounter") as WindowsPerformanceCounterPluginConfig;
            if (config == null)
            {
                throw new Exception("Cannot get configuration section : WindowsPerformanceCounter");
            }

            _reloadConfiguration = config.ReloadConfiguration.Enable;
            _reloadConfigurationInterval = config.ReloadConfiguration.Interval;

            _configurationReloadTime = DateTime.Now;

            _metricGenerators.Clear();
            foreach (WindowsPerformanceCounterPluginConfig.CounterConfig counter in config.Counters)
            {
                // create the IMetricGenerator object based on GeneratorClass
                Type classType = Type.GetType(counter.GeneratorClass);
                if (classType == null)
                {
                    Logger.Error("Cannot create metric generator class:{0}", counter.GeneratorClass);
                    continue;
                }
                var metricGenerator = (IMetricGenerator) Activator.CreateInstance(classType);
                // configure the object based on the properties
                Dictionary<string, object> parameters = new Dictionary<string, object>();
                foreach (PropertyInformation property in counter.ElementInformation.Properties)
                {
                    parameters[property.Name] = (object) property.Value;
                }
                if (!metricGenerator.Configure(parameters))
                {
                    Logger.Error("Cannot config metric generator:{0}", counter);
                    continue;
                }
                // add it to the list
                _metricGenerators.Add(metricGenerator);
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
            if (DateTime.Now > _configurationReloadTime.AddSeconds(_reloadConfigurationInterval))
            {
                Logger.Info("WindowsPerformanceCounter reloading configuration");
                Configure();
            }
            var metricValueList = new List<MetricValue>();
            foreach (IMetricGenerator metricGenerator in _metricGenerators)
            {
                metricValueList.AddRange(metricGenerator.NextValues());
            }
            return metricValueList;
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