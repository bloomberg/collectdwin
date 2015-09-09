using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Net;
using System.Text;
using NLog;

namespace BloombergFLP.CollectdWin
{
    internal class WriteHttpPlugin : IMetricsWritePlugin
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private readonly IList<HttpWriter> _httpWriters;

        public WriteHttpPlugin()
        {
            _httpWriters = new List<HttpWriter>();
        }

        public void Configure()
        {
            var config = ConfigurationManager.GetSection("CollectdWinConfig") as CollectdWinConfig;
            if (config == null)
            {
                throw new Exception("WriteHttpPlugin - Cannot get configuration section : CollectdWinConfig");
            }

            _httpWriters.Clear();

            foreach (CollectdWinConfig.WriteHttpNodeConfig node in config.WriteHttp.Nodes)
            {
                var writer = new HttpWriter
                {
                    Url = node.Url,
                    Timeout = node.Timeout,
                    BatchSize = node.BatchSize,
                    MaxIdleTime = node.MaxIdleTime,
                    EnableProxy = node.Proxy.Enable
                };

                if (writer.EnableProxy)
                {
                    writer.WebProxy = node.Proxy.Url.Length > 0 ? new WebProxy(node.Proxy.Url) : new WebProxy();
                }
                _httpWriters.Add(writer);
            }

            Logger.Info("WriteHttp plugin configured");
        }

        public void Start()
        {
            Logger.Info("WriteHttp - plugin started");
        }

        public void Stop()
        {
            Logger.Info("WriteHttp - plugin stopped");
        }

        public void Write(MetricValue metric)
        {
            if (metric == null)
            {
                Logger.Debug("write() - Invalid null metric");
                return;
            }
            foreach (HttpWriter writer in _httpWriters)
            {
                writer.Write(metric);
            }
        }
    }

    internal class HttpWriter
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public int BatchSize = 20;
        public bool EnableProxy = false;
        public int MaxIdleTime;
        public int Timeout;
        public string Url;
        public WebProxy WebProxy = null;

        private StringBuilder _batchedMetricStr;
        private int _numMetrics;

        public void Write(MetricValue metric)
        {
            string message = metric.GetMetricJsonStr();
            if (_batchedMetricStr == null)
            {
                _batchedMetricStr = new StringBuilder("[").Append(message);
            }
            else
            {
                _batchedMetricStr.Append(",").Append(message);
            }
            _numMetrics++;

            if (_numMetrics < BatchSize) return;

            _batchedMetricStr.Append("]");
            HttpPost(_batchedMetricStr.ToString());
            _batchedMetricStr = null;
            _numMetrics = 0;
        }

        public void HttpPost(string metricJsonStr)
        {
            HttpWebResponse response = null;
            try
            {
                byte[] data = Encoding.UTF8.GetBytes(metricJsonStr);
                var request = (HttpWebRequest) WebRequest.Create(Url);

                request.Method = "POST";
                request.ContentType = "application/json";
                request.ContentLength = data.Length;
                request.UserAgent = "CollectdWin/1.0";
                request.Accept = "*/*";
                request.KeepAlive = true;
                request.Timeout = Timeout;

                if (EnableProxy)
                {
                    request.Proxy = WebProxy;
                }
                if (MaxIdleTime > 0)
                {
                    request.ServicePoint.MaxIdleTime = MaxIdleTime;
                }

                // Display service point properties. 
                Logger.Trace("Connection properties: ServicePoint - HashCode:{0}, MaxIdleTime:{1}, IdleSince:{2}",
                    request.ServicePoint.GetHashCode(), request.ServicePoint.MaxIdleTime, request.ServicePoint.IdleSince);

                using (Stream reqStream = request.GetRequestStream())
                {
                    reqStream.Write(data, 0, data.Length);
                }

                response = (HttpWebResponse) request.GetResponse();
                var statusCode = (int) response.StatusCode;
                if (statusCode < 200 || statusCode >= 300)
                {
                    Logger.Error("Got a bad response code : ", statusCode);
                }

                if (!Logger.IsTraceEnabled) return;

                Stream respStream = response.GetResponseStream();
                string responseString = new StreamReader(respStream).ReadToEnd();
                Logger.Trace("Got response: " + responseString);
            }
            catch (Exception exp)
            {
                Logger.Error("Got exception in http post : ", exp);
            }
            finally
            {
                if (response != null)
                {
                    response.Close();
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