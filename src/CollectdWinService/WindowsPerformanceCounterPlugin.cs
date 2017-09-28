using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Timers;
using System.Reflection;
using System.Text;
using Microsoft.VisualBasic.Devices;
using NLog;

namespace BloombergFLP.CollectdWin
{
    internal class Helper
    {
        public delegate double TransformFunction(double value);
        public static string DictionaryValue(Dictionary<string, object> dict, string key)
        {
            return dict.ContainsKey(key) ? dict[key] as string : null;
        }
        public static double TotalPhysicalMemoryInBytes = new ComputerInfo().TotalPhysicalMemory;

        // conversion function group
        public static double _ConvertFromMegaBytesToBytes(double value) { return value * 1024 * 1024; }
        public static double _ConvertFromPercentageToRemainingPercentage(double value) { return 100 - value; }
        public static double _ConvertFromMemoryAvailableBytesToUsedPercentage(double value)
        {
            return (TotalPhysicalMemoryInBytes - value) * 100 / TotalPhysicalMemoryInBytes;
        }

        // generator creatation function group
        public static PerformanceCounterGenerator _Create()
        {
            return new PerformanceCounterMetricGenerator();
        }

        public static PerformanceCounterGenerator _CreateAverages()
        {
            return new AveragesGenerator();
        }

        public static PerformanceCounterGenerator _CreateCountInstances()
        {
            return new PerformanceCounterCategoryInstancesGenerator();
        }

        public static PerformanceCounterGenerator _CreateCountProcessesAndThreads()
        {
            return new CountProcessesAndThreadsGenerator();
        }
    }

    internal interface IMetricGenerator
    {
        bool Configure(Dictionary<string, object> config);
        bool Refresh();
        List<MetricValue> NextValues();
    }

    internal abstract class PerformanceCounterGenerator : IMetricGenerator
    {
        static protected string s_hostName = Util.GetHostName();
        static protected readonly Logger Logger = LogManager.GetCurrentClassLogger();
        public string CounterCategory, CounterName, CounterInstance;
        public string CollectdPlugin, CollectdPluginInstance, CollectdType, CollectdTypeInstance;
        protected Helper.TransformFunction _transform;
    
        public virtual bool Configure(Dictionary<string, object> config)
        {
            CounterCategory = Helper.DictionaryValue(config, "Category");
            CounterName = Helper.DictionaryValue(config, "Name");
            CounterInstance = Helper.DictionaryValue(config, "Instance");
            CollectdPlugin = Helper.DictionaryValue(config, "CollectdPlugin");
            CollectdPluginInstance = Helper.DictionaryValue(config, "CollectdPluginInstance");
            CollectdType = Helper.DictionaryValue(config, "CollectdType");
            CollectdTypeInstance = Helper.DictionaryValue(config, "CollectdTypeInstance");

            string transformer = Helper.DictionaryValue(config, "Transformer");
            if (transformer != null)
            {
                MethodInfo transformMethodInfo = typeof(Helper).GetMethod("_ConvertFrom" + transformer);
                if (transformMethodInfo != null)
                {
                    _transform = (Helper.TransformFunction)Delegate.CreateDelegate(typeof(Helper.TransformFunction), transformMethodInfo);
                }
            }
            return true;
        }

        public virtual bool Refresh()
        {
            return true;
        }

        public abstract List<MetricValue> NextValues();

        public MetricValue GetMetricValue(List<double> vals)
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
        protected PerformanceCounterCategory _performanceCounterCategory;

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
            metricValueList.Add(GetMetricValue(vals));
            return metricValueList;
        }
    }

    internal class CountProcessesAndThreadsGenerator : PerformanceCounterCategoryInstancesGenerator
    {
        protected PerformanceCounter _threadNumberCounter;

        public override bool Configure(Dictionary<string, object> config)
        {
            string category = "Process";
            string name = "Thread Count";
            string instance = "_Total";
            config["Category"] = category;
            if (!base.Configure(config))
                return false;
            try
            {
                _threadNumberCounter = new PerformanceCounter(category, name, instance);
                return true;
            }
            catch (Exception exp)
            {
                Logger.Error("Got exception : {0}, while adding performance counter: {1},{2},{3}", exp, category, name, instance);
                return false;
            }
        }

        public override List<MetricValue> NextValues()
        {
            var metricValueList = new List<MetricValue>();
            var vals = new List<double>();
            vals.Add(_performanceCounterCategory.GetInstanceNames().Length);
            vals.Add(_threadNumberCounter.NextValue());
            metricValueList.Add(GetMetricValue(vals));
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
            public List<double> Retrive(Helper.TransformFunction transform)
            {
                var vals = new List<double>();
                foreach (PerformanceCounter counter in Counters)
                {
                    double val;
                    try
                    {
                        val = counter.NextValue();
                        if (transform != null)
                            val = transform.Invoke(val);
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

        private bool ExistInstance(string instance)
        {
            foreach(MetricRetriever retriever in MetricRetrievers)
                if (instance == null && retriever.Instance == null
                    || instance == retriever.Instance)
                    return true;
            return false;
        }

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

        public override bool Refresh()
        {
            try
            { 
                if (MetricRetrievers == null)
                    MetricRetrievers = new List<MetricRetriever>();
                if (CounterInstance != null && CounterInstance == "*")
                {
                    var cat = new PerformanceCounterCategory(CounterCategory);
                    string[] instances = cat.GetInstanceNames();
                    foreach (string instance in instances)
                    {
                        if (!ExistInstance(instance))
                        {
                            MetricRetriever metricRetriver = GetMetricRetriever(CounterCategory, CounterName, instance);
                            if (metricRetriver == null)
                                return false;
                            // Replace collectd_plugin_instance with the Instance got from counter
                            metricRetriver.Instance = instance;
                            MetricRetrievers.Add(metricRetriver);
                        }
                    }
                }
                else if (MetricRetrievers.Count == 0)
                {
                    MetricRetriever metricRetriver = GetMetricRetriever(CounterCategory, CounterName, CounterInstance);
                    if (metricRetriver == null)
                        return false;
                    MetricRetrievers.Add(metricRetriver);
                }
            }
            catch (Exception exp)
            {
                Logger.Error("Got exception : {0} in Refresh(), CounterCategory: {1}, CounterName: {2}, CounterInstance: {3}", exp, CounterCategory, CounterName, CounterInstance);
                return false;
            }
            return true;
        }

        public override bool Configure(Dictionary<string, object> config)
        {
            return base.Configure(config) && Refresh();
        }

        public override List<MetricValue> NextValues()
        {
            var metricValueList = new List<MetricValue>();
            var missingInstances = new List<MetricRetriever>();
            foreach (MetricRetriever metricRetriver in MetricRetrievers)
            {
                var vals = metricRetriver.Retrive(_transform);
                if (vals == null)
                {
                    // The instance is gone
                    missingInstances.Add(metricRetriver);
                }
                else
                {
                    var metricValue = GetMetricValue(vals);
                    if (CollectdPluginInstance == null || CollectdPluginInstance == String.Empty)
                        metricValue.PluginInstanceName = metricRetriver.Instance;
                    metricValueList.Add(metricValue);
                }
            }

            // remove missing instances before return
            foreach (MetricRetriever missingInstance in missingInstances)
            {
                string logstr =
                    string.Format(
                        "Category:{0} - Instance:{1} - counter:{2} - CollectdPlugin:{3} - CollectdPluginInstance:{4} - CollectdType:{5} - CollectdTypeInstance:{6}",
                        CounterCategory, missingInstance.Instance, CounterName, 
                        CollectdPlugin, CollectdPluginInstance, CollectdType, CollectdTypeInstance);
                Logger.Info("Removed Performance COUNTER : {0}", logstr);
                MetricRetrievers.Remove(missingInstance);
            }

            return metricValueList;
        }
    }

    internal class AveragesGenerator : PerformanceCounterMetricGenerator
    {
        public List<uint> AverageIntervalsInSeconds = new List<uint>();
        public uint MaxIntervalInSeconds;
        public List<List<List<double>>> _samples; // 3-D array of [MetricRetrievers, Seconds, ValuesOfEachCounterName]
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

        private bool TakeSample()
        {
            // get a sample for each of MetricRetrievers
            List<List<double>> sample = new List<List<double>>();
            // each sample is an array of [MetricRetrievers, ValuesOfEachCounterName]
            foreach (MetricRetriever metricRetriver in MetricRetrievers)
            {
                var vals = metricRetriver.Retrive(_transform);  // list of values from each counter
                if (vals == null)
                    return false;
                sample.Add(vals);
            }
            // add the "sample" into "_samples"
            _mutex.WaitOne();
            var retrievers = sample.Count;
            for (int i = 0; i < retrievers; ++i)
            {
                var sampleList = _samples[i];
                var vals = sample[i]; // list of values from each counter
                sampleList.Insert(0, vals);
                if (sampleList.Count > MaxIntervalInSeconds)
                    sampleList.RemoveAt(sampleList.Count-1);
            }
            _mutex.ReleaseMutex();
            return true;
        }

        public override bool Refresh()
        {
            if (MetricRetrievers == null || MetricRetrievers.Count == 0)
                base.Refresh();
            return true;
        }

        public override bool Configure(Dictionary<string, object> config)
        {
            if (!base.Configure(config))
                return false;
            string averageIntervals = Helper.DictionaryValue(config, "TransformerParameters");
            if (averageIntervals == null)
            {
                Logger.Error("AveragesGenerator: no interval is configured for averaging.");
                return false;
            }
            string[] averagesInString = averageIntervals.Split(',');
            foreach (string interval in averagesInString)
            {
                uint val;
                if (!UInt32.TryParse(interval, out val) || val == 0)
                {
                    Logger.Error("AveragesGenerator: average interval should be a positive number.");
                    return false;
                }
                AverageIntervalsInSeconds.Add(val);
            }
            if (AverageIntervalsInSeconds.Count == 0)
            {
                Logger.Error("AveragesGenerator: average intervals are not configured correctly.");
                return false;
            }
            // TODO: support unsorted average values
            // sort is not necessary now. NextValues() can only return a sorted average values ascendantly
            AverageIntervalsInSeconds.Sort();
            MaxIntervalInSeconds = AverageIntervalsInSeconds[AverageIntervalsInSeconds.Count-1];
            // init _samples
            _samples = new List<List<List<double>>>();
            foreach (MetricRetriever metricRetriver in MetricRetrievers)
                _samples.Add(new List<List<double>>());
            // take the first sample
            if (!TakeSample())
            {
                Logger.Error("AveragesGenerator: Failed to take the first sample.");
                return false;
            }
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
            // the average values are saved in a fatten list of [Average values of each MetricRetriever]
            int i = 0;
            foreach (MetricRetriever metricRetriver in MetricRetrievers)
            {
                var averages = new List<double>();
                var sampleList = _samples[i++];
                var sampleCount = sampleList.Count;
                var lastestSample = sampleList[0];
                var sums = new List<double>();
                var counters = lastestSample.Count;
                for (int n = 0; n < counters; ++n)
                    sums.Add(0);
                uint interval = AverageIntervalsInSeconds[0];
                int intervalCount = 0;
                for (int count = 0; count < sampleCount; ++count)
                {
                    List<double> sample = sampleList[count];
                    for (int n = 0; n < counters; ++n)
                        sums[n] += sample[n];
                    if (count + 1 == interval)
                    {
                        foreach (double sum in sums)
                            averages.Add(sum / interval);
                        ++intervalCount;
                        if (intervalCount < AverageIntervalsInSeconds.Count)
                            interval = AverageIntervalsInSeconds[intervalCount];
                    }
                }
                // add the averages for intervals that we don't have enough samples
                for (; intervalCount < AverageIntervalsInSeconds.Count; ++intervalCount)
                    foreach (double sum in sums)
                        averages.Add(sum / sampleCount);

                var metricValue = GetMetricValue(averages);
                if (CollectdPluginInstance == null || CollectdPluginInstance == String.Empty)
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
        private bool _refreshConfiguration;
        private DateTime _instanceRefreshTime;
        private int _refreshConfigurationInterval;

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

            _refreshConfiguration = config.RefreshInstancesConfiguration.Enable;
            _refreshConfigurationInterval = config.RefreshInstancesConfiguration.Interval;

            _instanceRefreshTime = DateTime.Now;

            _metricGenerators.Clear();
            foreach (WindowsPerformanceCounterPluginConfig.CounterConfig counter in config.Counters)
            {
                // configure the object based on the properties
                StringBuilder generatorConfig = new StringBuilder("Configuring metric generator: ");
                Dictionary<string, object> parameters = new Dictionary<string, object>();
                foreach (PropertyInformation property in counter.ElementInformation.Properties)
                {
                    parameters[property.Name] = (object)property.Value;
                    generatorConfig.Append(property.Name + " = " + "'" + property.Value + "', ");
                }
                Logger.Info(generatorConfig.ToString());
                // create the IMetricGenerator object based on GeneratorClass
                //Get the method information using the method info class
                MethodInfo createMethodInfo = typeof(Helper).GetMethod("_Create" + counter.Transformer);
                IMetricGenerator metricGenerator;
                if (createMethodInfo != null)
                {
                    metricGenerator = (IMetricGenerator) createMethodInfo.Invoke(null, null);
                }
                else
                {
                    Logger.Info("Cannot find method for creating metric generator: Transformer={0}, using default generator.", counter.Transformer);
                    metricGenerator = Helper._Create();
                }
                if (!metricGenerator.Configure(parameters))
                {
                    Logger.Error("Cannot config metric generator:{0}", counter.Transformer);
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
            if (DateTime.Now > _instanceRefreshTime.AddSeconds(_refreshConfigurationInterval))
            {
                Logger.Info("WindowsPerformanceCounter reloading configuration");
                foreach (IMetricGenerator metricGenerator in _metricGenerators)
                {
                    metricGenerator.Refresh();
                }
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