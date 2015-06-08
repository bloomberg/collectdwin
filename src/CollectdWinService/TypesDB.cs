using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using NLog;

namespace BloombergFLP.CollectdWin
{
    public enum DsType
    {
        Absolute,
        Counter,
        Derive,
        Gauge
    };

    public enum Status
    {
        Success,
        Failure
    };

    public class DataSource
    {
        public DataSource(string name, DsType type, double min, double max)
        {
            Name = name;
            Type = type;
            Min = min;
            Max = max;
        }

        public string Name { get; set; }
        public DsType Type { get; set; }
        public double Min { get; set; }
        public double Max { get; set; }
    }

    internal class DataSetCollection
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private static volatile DataSetCollection _instance;
        private static readonly object SyncRoot = new Object();

        private readonly Dictionary<string, IList<DataSource>> _dataSetMap;
        // <Key=dataSetName, value=DataSourceList>

        private DataSetCollection()
        {
            _dataSetMap = new Dictionary<string, IList<DataSource>>();
        }

        public static DataSetCollection Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (SyncRoot)
                    {
                        if (_instance == null)
                        {
                            _instance = new DataSetCollection();
                            _instance.Load();
                        }
                    }
                }
                return _instance;
            }
        }

        private static Status GetDouble(string dstr, out double val)
        {
            if (dstr == "u" || dstr == "U")
            {
                val = Double.NaN;
                return (Status.Success);
            }
            try
            {
                val = Double.Parse(dstr);
            }
            catch (Exception)
            {
                val = Double.NaN;
                return (Status.Failure);
            }
            return (Status.Success);
        }

        public void Print()
        {
            var sb = new StringBuilder();
            foreach (var entry in _dataSetMap)
            {
                sb.Append(string.Format("\n[{0}] ==>", entry.Key));
                foreach (DataSource ds in entry.Value)
                {
                    sb.Append(string.Format(" [{0}:{1}:{2}:{3}]", ds.Name, ds.Type, ds.Min, ds.Max));
                }
            }
            Console.WriteLine(sb.ToString());
        }

        public void Load()
        {
            const string dataSetPattern = @"[\s\t]*(\w+)[\s\t]*(.*)$";
            const string dataSourcePattern = @"(\w+):(ABSOLUTE|COUNTER|DERIVE|GAUGE):([+-]?\w+):([+-]?\w+)[,]?\s*";

            var dataSetRegex = new Regex(dataSetPattern, RegexOptions.IgnoreCase);
            var dataSourceRegex = new Regex(dataSourcePattern, RegexOptions.IgnoreCase);

            string fileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "types.db");
            string[] lines = File.ReadAllLines(fileName);

            foreach (string line in lines)
            {
                if (line.StartsWith("#"))
                {
                    continue;
                }
                Match match = dataSetRegex.Match(line);
                if (match.Groups.Count < 3)
                {
                    Logger.Error("types.db: invalid data set [{0}]", line);
                    continue;
                }
                string dataSetName = match.Groups[1].Value;
                MatchCollection matches = dataSourceRegex.Matches(line);
                if (matches.Count < 1)
                {
                    Logger.Error("types.db: invalid data source [{0}]", line);
                    continue;
                }
                var dataSourceList = new List<DataSource>();
                foreach (Match m in matches)
                {
                    if (m.Groups.Count != 5)
                    {
                        Logger.Error("types.db: cannot parse data source [{0}]", line);
                        dataSourceList.Clear();
                        break;
                    }

                    string dsName = m.Groups[1].Value;
                    var dstype = (DsType) Enum.Parse(typeof (DsType), m.Groups[2].Value, true);

                    double min, max;

                    if (GetDouble(m.Groups[3].Value, out min) != Status.Success)
                    {
                        Logger.Error("types.db: invalid Min value [{0}]", line);
                        dataSourceList.Clear();
                        break;
                    }

                    if (GetDouble(m.Groups[4].Value, out max) != Status.Success)
                    {
                        Logger.Error("types.db: invalid Max value [{0}]", line);
                        dataSourceList.Clear();
                        break;
                    }

                    var ds = new DataSource(dsName, dstype, min, max);
                    dataSourceList.Add(ds);
                }
                if (dataSourceList.Count > 0)
                {
                    _dataSetMap[dataSetName] = dataSourceList;
                }
            }
        }

        public IList<DataSource> GetDataSource(string dataSetName)
        {
            IList<DataSource> dataSourceList;
            return (_dataSetMap.TryGetValue(dataSetName, out dataSourceList) ? (dataSourceList) : (null));
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