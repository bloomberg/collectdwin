using System;
using System.Collections.Generic;
using NLog;

namespace BloombergFLP.CollectdWin
{
    internal class StatsdAggregator
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private readonly bool _delCounter;
        private readonly bool _delGauge;
        private readonly bool _delSet;
        private readonly bool _delTimer;
        private readonly string _hostName;
        private readonly Object _lock;
        private readonly Dictionary<string, StatsdMetric> _metrics;
        private readonly float[] _percentiles;
        private readonly bool _timerCount;
        private readonly bool _timerLower;
        private readonly bool _timerSum;
        private readonly bool _timerUpper;

        public StatsdAggregator(bool delCounter, bool delTimer, bool delGauge, bool delSet, bool timerLower,
            bool timerUpper, bool timerSum, bool timerCount, float[] percentiles)
        {
            _lock = new Object();
            _metrics = new Dictionary<string, StatsdMetric>();
            _hostName = Util.GetHostName();

            _delCounter = delCounter;
            _delTimer = delTimer;
            _delGauge = delGauge;
            _delSet = delSet;

            _timerLower = timerLower;
            _timerUpper = timerUpper;
            _timerSum = timerSum;
            _timerCount = timerCount;

            _percentiles = percentiles;

            if (_percentiles != null && _percentiles.Length > 0)
                StatsdMetric.Latency.HistogramEnabled = true;
            else
                StatsdMetric.Latency.HistogramEnabled = false;

            Logger.Info(
                "Statsd config - delCounter:{0}, delTimer:{1}, delGauge:{2}, delSet:{3}, isHistogramEnabled:{4}",
                _delCounter, _delTimer, _delGauge, _delSet, StatsdMetric.Latency.HistogramEnabled);
        }

        public void AddMetric(StatsdMetric metric)
        {
            string key = metric.Type + "_" + metric.Name;
            lock (_lock)
            {
                StatsdMetric oldMetric;
                if (_metrics.TryGetValue(key, out oldMetric))
                {
                    //merge
                    double val = metric.Value;
                    oldMetric.AddValue(val);
                }
                else
                {
                    _metrics[key] = metric;
                }
            }
        }

        public IList<MetricValue> Read()
        {
            lock (_lock)
            {
                var res = new List<MetricValue>();
                var removeList = new List<string>();
                foreach (var pair in _metrics)
                {
                    StatsdMetric metric = pair.Value;
                    if (metric.NumUpdates <= 0 &&
                        ((_delCounter && metric.Type == StatsdMetric.StatsdType.StatsdCounter) ||
                         (_delTimer && metric.Type == StatsdMetric.StatsdType.StatsdTimer) ||
                         (_delGauge && metric.Type == StatsdMetric.StatsdType.StatsdGauge) ||
                         (_delSet && metric.Type == StatsdMetric.StatsdType.StatsdSet)))
                    {
                        removeList.Add(pair.Key);
                        continue;
                    }
                    var metricVal = new MetricValue
                    {
                        HostName = _hostName,
                        PluginName = "statsd",
                        PluginInstanceName = "",
                        TypeInstanceName = metric.Name,
                        Values = new[] {metric.GetMetric()}
                    };
                    switch (metric.Type)
                    {
                        case StatsdMetric.StatsdType.StatsdGauge:
                            metricVal.TypeName = "gauge";
                            break;
                        case StatsdMetric.StatsdType.StatsdTimer:
                            metricVal.TypeName = "latency";
                            metricVal.TypeInstanceName += "-average";
                            break;
                        case StatsdMetric.StatsdType.StatsdSet:
                            metricVal.TypeName = "objects";
                            break;
                        default:
                            metricVal.TypeName = "derive";
                            break;
                    }
                    TimeSpan t = DateTime.UtcNow - new DateTime(1970, 1, 1);
                    double epoch = t.TotalMilliseconds/1000;
                    metricVal.Epoch = Math.Round(epoch, 3);

                    res.Add(metricVal);

                    if (metric.Type == StatsdMetric.StatsdType.StatsdTimer)
                    {
                        if (_timerLower)
                        {
                            MetricValue lowerValue = metricVal.DeepCopy();
                            lowerValue.TypeInstanceName = metric.Name + "-lower";
                            lowerValue.Values[0] = metric.Lat.Min;
                            res.Add(lowerValue);
                        }
                        if (_timerUpper)
                        {
                            MetricValue upperValue = metricVal.DeepCopy();
                            upperValue.TypeInstanceName = metric.Name + "-upper";
                            upperValue.Values[0] = metric.Lat.Max;
                            res.Add(upperValue);
                        }

                        if (_timerSum)
                        {
                            MetricValue upperSum = metricVal.DeepCopy();
                            upperSum.TypeInstanceName = metric.Name + "-Sum";
                            upperSum.Values[0] = metric.Lat.Sum;
                            res.Add(upperSum);
                        }
                        if (_timerCount)
                        {
                            MetricValue upperCount = metricVal.DeepCopy();
                            upperCount.TypeInstanceName = metric.Name + "-count";
                            upperCount.Values[0] = metric.Lat.Num;
                            res.Add(upperCount);
                        }
                        Histogram histogram = metric.Lat.Histogram;
                        if (_percentiles != null && _percentiles.Length > 0 && histogram != null)
                        {
                            foreach (float percentile in _percentiles)
                            {
                                double val = histogram.GetPercentile(percentile);

                                MetricValue mv = metricVal.DeepCopy();
                                mv.TypeInstanceName = metric.Name + "-percentile-" + percentile;
                                mv.Values[0] = val;
                                res.Add(mv);
                            }
                        }
                    }
                    metric.Reset();
                }
                Logger.Debug("Removing entries that were not updated:{0}", removeList.Count);
                foreach (string key in removeList)
                {
                    _metrics.Remove(key);
                }
                return (res);
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