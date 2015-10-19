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
            var config = ConfigurationManager.GetSection("WriteHttp") as WriteHttpPluginConfig;
            if (config == null)
            {
                throw new Exception("Cannot get configuration section : WriteHttp");
            }

            _httpWriters.Clear();

            foreach (WriteHttpPluginConfig.WriteHttpNodeConfig node in config.Nodes)
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

                if (node.UserName != null && node.Password != null)
                {
                    /* Possibly misfeature- adding BasicAuthHeaderData to HttpWriter class to efficiently support basic auth,
                     * but saves the ToBase64String string encode on each request. Better, but more expensive, would be
                     * to put both on as secure strings and add a config param to support other auth methods while
                     * building the HttpWebResponse on each call. @FerventGeek */ 
                    writer.BasicAuthHeaderData = System.Convert.ToBase64String(
                        System.Text.Encoding.GetEncoding("ISO-8859-1").GetBytes(node.UserName + ":" + node.Password));
                }

                writer.UseSafeNames = node.UseSafeNames;

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

        private const string SAFE_NAME_CHARS = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789.:-_";
        
        public int BatchSize = 20;
        public bool EnableProxy = false;
        public int MaxIdleTime;
        public int Timeout;
        public string Url;
        public WebProxy WebProxy = null;
        public string BasicAuthHeaderData = null;
        public bool UseSafeNames = false;

        private StringBuilder _batchedMetricStr;
        private int _numMetrics;

        public void Write(MetricValue metric)
        {
            // See notes in SafeifyName()
            if (UseSafeNames)
            {
                metric.PluginInstanceName = SafeifyName(metric.PluginInstanceName);
            }
            
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
                if (BasicAuthHeaderData != null)
                {
                    request.Headers.Add("Authorization", "Basic " + BasicAuthHeaderData);
                }

                // Display service point properties. 
                Logger.Trace("Connection properties: ServicePoint - HashCode:{0}, MaxIdleTime:{1}, IdleSince:{2}",
                    request.ServicePoint.GetHashCode(), request.ServicePoint.MaxIdleTime, request.ServicePoint.IdleSince);

                using (Stream reqStream = request.GetRequestStream())
                {
                    if (Logger.IsTraceEnabled)
                    {
                        Logger.Trace("Adding request body : {0}", metricJsonStr);
                    }
                    
                    reqStream.Write(data, 0, data.Length);
                }

                response = (HttpWebResponse) request.GetResponse();

                // Skip overhead of the trace body read
                if (Logger.IsTraceEnabled) 
                {
                    Stream respStream = response.GetResponseStream();
                    string responseString = new StreamReader(respStream).ReadToEnd();
                    Logger.Trace("Got response: {0}" + responseString);
                }
            }
            catch (WebException ex)
            {
                if (ex.Status == WebExceptionStatus.ProtocolError)
                {
                    HttpWebResponse exceptionResponse = (HttpWebResponse)ex.Response;

                    using (var stream = exceptionResponse.GetResponseStream())
                    using (var reader = new StreamReader(stream))
                    {
                        Logger.Error("Got web exception in http post : {0} - {1}",
                            (int)exceptionResponse.StatusCode, exceptionResponse.StatusCode);  
                        
                        // Skip overhead of trace body read 
                        if (!Logger.IsTraceEnabled) 
                        {
                            string errorBody = reader.ReadToEnd();
                            if (errorBody != null)
                            {
                                Logger.Error(errorBody);
                            }
                        }
                    }
                }
                else
                {
                    Logger.Error("Got web exception in http post : {0}", ex.ToString());
                }
            }
            catch (Exception exp)
            {
                Logger.Error("Got exception in http post : {0}", exp);
            }
            finally
            {
                if (response != null)
                {
                    response.Close();
                }
            }
        }

        private string SafeifyName(string text)
        {
            /* Some receivers balk on brackets and other characters common on InstanceNames
             * in Windows systems.  Check against a subset of URL safe characters and replace
             * with underscores. Regex would be cleaner but slower. Will save for
             * configurable re-write feature later. @FerventGeek */
            
            StringBuilder sb = new StringBuilder();
            foreach (char c in text)
            {
                if (SAFE_NAME_CHARS.IndexOf(c) > -1)
                {
                    sb.Append(c);
                }
                else
                {
                    sb.Append('_');
                }
            }

            return sb.ToString();
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
