using System;
using System.Collections.Generic;
using System.Linq;
using NLog;

namespace BloombergFLP.CollectdWin
{
    internal class CacheEntry
    {
        public MetricValue MetricRate;
        public double[] RawValues;

        public CacheEntry(MetricValue metricValue)
        {
            MetricRate = metricValue;
            RawValues = (double[]) metricValue.Values.Clone();
        }
    }

    internal class Aggregator
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly Dictionary<string, CacheEntry> _cache;
        private readonly Object _cacheLock;
        private readonly bool _storeRates;
        private readonly int _timeoutSeconds;

        public Aggregator(int timeoutSeconds, bool storeRates)
        {
            _cache = new Dictionary<string, CacheEntry>();
            _cacheLock = new Object();
            _timeoutSeconds = timeoutSeconds;
            _storeRates = storeRates;
        }

        public void Aggregate(ref MetricValue metricValue)
        {
            // If rates are not stored then there is nothing to aggregate
            if (!_storeRates)
            {
                return;
            }
            IList<DataSource> dsl = DataSetCollection.Instance.GetDataSource(metricValue.TypeName);
            if (dsl == null || metricValue.Values.Length != dsl.Count)
            {
                return;
            }

            double now = Util.GetNow();

            lock (_cacheLock)
            {
                CacheEntry cEntry;
                string key = metricValue.Key();

                if (!(_cache.TryGetValue(key, out cEntry)))
                {
                    cEntry = new CacheEntry(metricValue.DeepCopy());
                    for (int i = 0; i < metricValue.Values.Length; i++)
                    {
                        if (dsl[i].Type == DsType.Derive ||
                            dsl[i].Type == DsType.Absolute ||
                            dsl[i].Type == DsType.Counter)
                        {
                            metricValue.Values[i] = double.NaN;
                            cEntry.MetricRate.Values[i] = double.NaN;
                        }
                    }
                    cEntry.MetricRate.Epoch = now;
                    _cache[key] = cEntry;
                    return;
                }
                for (int i = 0; i < metricValue.Values.Length; i++)
                {
                    double rawValNew = metricValue.Values[i];
                    double rawValOld = cEntry.RawValues[i];
                    double rawValDiff = rawValNew - rawValOld;
                    double timeDiff = cEntry.MetricRate.Epoch - now;

                    double rateNew = rawValDiff/timeDiff;

                    switch (dsl[i].Type)
                    {
                        case DsType.Gauge:
                            // no rates calculations are done, values are stored as-is for gauge
                            cEntry.RawValues[i] = rawValNew;
                            cEntry.MetricRate.Values[i] = rawValNew;
                            break;

                        case DsType.Absolute:
                            // similar to gauge, except value will be divided by time diff
                            cEntry.MetricRate.Values[i] = metricValue.Values[i]/timeDiff;
                            cEntry.RawValues[i] = rawValNew;
                            metricValue.Values[i] = cEntry.MetricRate.Values[i];
                            break;

                        case DsType.Derive:
                            cEntry.RawValues[i] = rawValNew;
                            cEntry.MetricRate.Values[i] = rateNew;
                            metricValue.Values[i] = rateNew;

                            break;

                        case DsType.Counter:
                            // Counters are very simlar to derive except when counter wraps around                                
                            if (rawValNew < rawValOld)
                            {
                                // counter has wrapped around
                                cEntry.MetricRate.Values[i] = metricValue.Values[i]/timeDiff;
                                cEntry.RawValues[i] = rawValNew;
                                metricValue.Values[i] = cEntry.MetricRate.Values[i];
                            }
                            else
                            {
                                cEntry.MetricRate.Values[i] = rateNew;
                                cEntry.RawValues[i] = rawValNew;
                                metricValue.Values[i] = rateNew;
                            }
                            break;
                    }

                    // range checks
                    if (metricValue.Values[i] < dsl[i].Min)
                    {
                        metricValue.Values[i] = dsl[i].Min;
                        cEntry.RawValues[i] = metricValue.Values[i];
                    }
                    if (metricValue.Values[i] > dsl[i].Max)
                    {
                        metricValue.Values[i] = dsl[i].Max;
                        cEntry.RawValues[i] = metricValue.Values[i];
                    }

                    cEntry.MetricRate.Epoch = now;
                    _cache[key] = cEntry;
                }
            }
        }

        public void RemoveExpiredEntries()
        {
            // If rates are not stored then there is nothing to remove
            if (!_storeRates)
            {
                return;
            }
            double now = Util.GetNow();
            double expirationTime = now - _timeoutSeconds;
            var removeList = new List<string>();

            lock (_cacheLock)
            {
                removeList.AddRange(from pair in _cache
                    let cEntry = pair.Value
                    where cEntry.MetricRate.Epoch < expirationTime
                    select pair.Key);
                if (removeList.Count > 0)
                    Logger.Debug("Removing expired entries: {0}", removeList.Count);
                foreach (string key in removeList)
                {
                    _cache.Remove(key);
                }
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