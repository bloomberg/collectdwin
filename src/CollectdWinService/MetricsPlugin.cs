using System;
using System.Collections.Generic;
using System.Globalization;
using NLog;
using System.Web.Script.Serialization;

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

    public class MetricValue
    {
        private const string MetricJsonFormat =
            @"{{""host"":""{0}"", ""plugin"":""{1}"", ""plugin_instance"":""{2}""," +
            @" ""type"":""{3}"", ""type_instance"":""{4}"", ""time"":{5}, ""interval"":{6}," +
            @" ""dstypes"":[{7}], ""dsnames"":[{8}], ""values"":[{9}]{10}}}";
        private const string MetaDataJsonFormat = @", ""meta"":{0}";

        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private readonly IDictionary<string, string> Meta = new SortedDictionary<string, string>();
      
        public string HostName { get; set; }
        public string PluginName { get; set; }
        public string PluginInstanceName { get; set; }
        public string TypeName { get; set; }
        public string TypeInstanceName { get; set; }

        public int Interval { get; set; }
        public double Epoch { get; set; }
        public double[] Values { get; set; }

        public IDictionary<string, string> MetaData
        {
            get
            {
                return Meta;
            }
        }

        public void AddMetaData(string tagName, string tagValue)
        {
            Meta[tagName] = tagValue;
        }

        public void AddMetaData(IDictionary<string, string> meta)
        {
            if (meta == null)
            {
                return;
            }
            foreach(var tag in meta)
            {
                Meta[tag.Key] = tag.Value;
            }
        }

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

        public string EscapeString(string str)
        {
            if (string.IsNullOrEmpty(str))
            {
                return (str);
            }
            return (str.Replace(@"\", @"\\"));
        }

        public string GetMetaDataJsonStr()
        {            
            return(new JavaScriptSerializer().Serialize(MetaData));
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


            var metaDataStr = "";
            if (MetaData.Count > 0)
            {
                metaDataStr = string.Format(MetaDataJsonFormat, GetMetaDataJsonStr());
            }
            var res = "";
            try
            {
                res = string.Format(MetricJsonFormat, HostName, PluginName,
                    EscapeString(PluginInstanceName), TypeName, EscapeString(TypeInstanceName), epochStr,
                    Interval, dsTypesStr, dsNamesStr, valStr, metaDataStr);
            }
            catch (Exception exp)
            {
                Logger.Error("Got exception in json conversion : {0}", exp);
            }
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