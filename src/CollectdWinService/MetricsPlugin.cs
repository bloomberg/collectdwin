using System;
using System.Collections.Generic;
using System.Globalization;
using NLog;

namespace BloombergFLP.CollectdWin
{
    internal interface IMetricsPlugin
    {
        void Configure();
        void Start();
        void Stop();
    }

    internal interface IMetricsReadPlugin : IMetricsPlugin
    {
        IList<MetricValue> Read();
    }

    internal interface IMetricsWritePlugin : IMetricsPlugin
    {
        void Write(MetricValue metric);
        void Flush();
    }

    internal class MetricValue
    {
        private const string MetricJsonFormat =
            @"{{""host"":""{0}"", ""plugin"":""{1}"", ""plugin_instance"":""{2}""," +
            @" ""type"":""{3}"", ""type_instance"":""{4}"", ""time"":{5}, ""interval"":{6}," +
            @" ""dstypes"":[{7}], ""dsnames"":[{8}], ""values"":[{9}]}}";

        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public string HostName { get; set; }
        public string PluginName { get; set; }
        public string PluginInstanceName { get; set; }
        public string TypeName { get; set; }
        public string TypeInstanceName { get; set; }

        public int Interval { get; set; }
        public double Epoch { get; set; }
        public double[] Values { get; set; }

        public string Key()
        {
            return (HostName + "." + PluginName + "." + PluginInstanceName + "." + TypeName + "." + TypeInstanceName);
        }

        public MetricValue DeepCopy()
        {
            var other = (MetricValue) MemberwiseClone();
            other.HostName = String.Copy(HostName);
            other.PluginName = String.Copy(PluginName);
            other.PluginInstanceName = String.Copy(PluginInstanceName);
            other.TypeName = String.Copy(TypeName);
            other.TypeInstanceName = String.Copy(TypeInstanceName);
            other.Values = (double[]) Values.Clone();
            return (other);
        }

        public string GetMetricJsonStr()
        {
            IList<DataSource> dsList = DataSetCollection.Instance.GetDataSource(TypeName);
            var dsNames = new List<string>();
            var dsTypes = new List<string>();
            if (dsList == null)
            {
                Logger.Debug("Invalid type : {0}, not found in types.db", TypeName);
            }
            else
            {
                foreach (DataSource ds in dsList)
                {
                    dsNames.Add(ds.Name);
                    dsTypes.Add(ds.Type.ToString().ToLower());
                }
            }
            String epochStr = Epoch.ToString(CultureInfo.InvariantCulture);
            string dsTypesStr = string.Join(",", dsTypes.ConvertAll(m => string.Format("\"{0}\"", m)).ToArray());
            string dsNamesStr = string.Join(",", dsNames.ConvertAll(m => string.Format("\"{0}\"", m)).ToArray());
            string valStr = string.Join(",", Array.ConvertAll(Values, val => val.ToString(CultureInfo.InvariantCulture)));

            string res = string.Format(MetricJsonFormat, HostName, PluginName,
                PluginInstanceName, TypeName, TypeInstanceName, epochStr,
                Interval, dsTypesStr, dsNamesStr, valStr);
            return (res);
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