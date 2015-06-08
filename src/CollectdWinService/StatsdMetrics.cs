using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NLog;

namespace BloombergFLP.CollectdWin
{
    internal class StatsdMetric
    {
        public enum StatsdType
        {
            StatsdCounter,
            StatsdTimer,
            StatsdGauge,
            StatsdSet
        };

        private readonly HashSet<double> _set;

        public StatsdMetric(string name, StatsdType type, double val)
        {
            Name = name;
            Type = type;
            _set = null;
            Lat = null;
            Value = val;
            NumUpdates = 1;

            switch (Type)
            {
                case StatsdType.StatsdSet:
                    _set = new HashSet<double> {val};
                    break;
                case StatsdType.StatsdTimer:
                    Lat = new Latency();
                    Lat.Min = Lat.Max = Lat.Sum = val;
                    Lat.Num = 1;
                    break;
            }
        }

        public string Name { get; private set; }
        public StatsdType Type { get; private set; }
        public double Value { get; private set; }
        public Latency Lat { get; private set; }
        public int NumUpdates { get; private set; }

        public double GetMetric()
        {
            switch (Type)
            {
                case StatsdType.StatsdSet:
                    return (_set.Count);

                case StatsdType.StatsdTimer:
                    return (Lat.Sum/Lat.Num);
            }
            return (Value);
        }

        public void Reset()
        {
            NumUpdates = 0;

            if (Type == StatsdType.StatsdSet && _set != null)
            {
                _set.Clear();
            }
            if (Type == StatsdType.StatsdTimer && Lat != null)
            {
                Lat.Reset();
            }
        }

        public void AddValue(double value)
        {
            switch (Type)
            {
                case StatsdType.StatsdCounter:
                    Value += value;
                    break;
                case StatsdType.StatsdSet:
                    _set.Add(value);
                    break;
                case StatsdType.StatsdGauge:
                    Value = value;
                    break;
                case StatsdType.StatsdTimer:
                    Lat.AddValue(value);
                    break;
            }
            NumUpdates++;
        }

        public class Latency
        {
            public double Max;
            public double Min;
            public int Num;
            public double Sum;

            public Latency()
            {
                Min = Max = Sum = 0;
                Num = 0;
                Histogram = null;
                if (HistogramEnabled)
                {
                    Histogram = new Histogram();
                }
            }

            public Histogram Histogram { get; private set; }

            public static bool HistogramEnabled { get; set; }

            public void AddValue(double val)
            {
                Min = (val < Min) ? val : Min;
                Max = (val > Max) ? val : Max;
                Sum += val;
                Num++;
                if (HistogramEnabled)
                {
                    Histogram.AddValue(val);
                }
            }

            public void Reset()
            {
                Min = Max = Sum = 0;
                Num = 0;
                if (HistogramEnabled)
                {
                    Histogram.Reset();
                }
            }
        };
    }

    internal class StatsdMetricParser
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public static IList<StatsdMetric> Parse(string str)
        {
            var metrics = new List<StatsdMetric>();
            string[] lines = str.Split('\n');
            metrics.AddRange(lines.Select(ParseLine).Where(metric => metric != null));
            return (metrics);
        }

        /*
         * StatsD metric collection protocol
         *   - metrics are separated by newlines
         *   - each line ar generally of the form:
         *     <metric name>:<value>|<type>
         *     ** Gauges     :   <metric name>:<value>|g
         *     ** Counters   :   <metric name>:<value>|c[|@<sample rate>]
         *     ** Timers     :   <metric name>:<value>|ms
         *     ** Sets       :   <metric name>:<value>|s
         */

        public static StatsdMetric ParseLine(string line)
        {
            const string pattern = @"^(?<name>.*):(?<value>.*)\|(?<type>.*)(\|\@(?<rate>.*))?$";
            var regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.RightToLeft);
            Match match = regex.Match(line);

            if (!match.Success)
            {
                Logger.Debug("Parser: Invalid statsd format [{0}]", line);
                return (null);
            }
            GroupCollection groups = match.Groups;

            string name = groups["name"].Value;
            string valstr = groups["value"].Value;
            string typestr = groups["type"].Value;
            string ratestr = groups["rate"].Value;

            if (String.IsNullOrEmpty(name) || String.IsNullOrEmpty(valstr) || String.IsNullOrEmpty(typestr))
            {
                Logger.Debug("Parser: name/value/type are not optional [{0}]", line);
                return (null);
            }
            StatsdMetric.StatsdType type;

            if (String.Compare(typestr, "g", StringComparison.OrdinalIgnoreCase) == 0)
                type = StatsdMetric.StatsdType.StatsdGauge;
            else if (String.Compare(typestr, "c", StringComparison.OrdinalIgnoreCase) == 0)
                type = StatsdMetric.StatsdType.StatsdCounter;
            else if (String.Compare(typestr, "s", StringComparison.OrdinalIgnoreCase) == 0)
                type = StatsdMetric.StatsdType.StatsdSet;
            else if (String.Compare(typestr, "ms", StringComparison.OrdinalIgnoreCase) == 0)
                type = StatsdMetric.StatsdType.StatsdTimer;
            else
            {
                Logger.Debug("Parser: invalid type [{0}]", line);
                return (null);
            }
            double value;
            try
            {
                value = Convert.ToDouble(valstr);
            }
            catch (Exception)
            {
                Logger.Debug("Parser: invalid value [{0}]", line);
                return (null);
            }

            double rate = 0;
            try
            {
                if (!String.IsNullOrEmpty(ratestr))
                    rate = Convert.ToDouble(ratestr);
            }
            catch (Exception)
            {
                Logger.Debug("Parser: invalid rate [{0}]", line);
                return (null);
            }

            if (!string.IsNullOrEmpty(ratestr) && (rate <= 0 || rate > 1))
            {
                Logger.Debug("Parser: invalid rate range [{0}]", line);
                return (null);
            }

            if (!string.IsNullOrEmpty(ratestr) && type != StatsdMetric.StatsdType.StatsdCounter)
            {
                Logger.Debug("Parser: rate is supported only for Counters [{0}]", line);
                return (null);
            }

            if (!string.IsNullOrEmpty(ratestr))
            {
                value = value/rate;
            }

            var metric = new StatsdMetric(name, type, value);
            return (metric);
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