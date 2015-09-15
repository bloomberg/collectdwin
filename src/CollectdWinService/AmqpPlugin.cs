using System;
using System.Configuration;
using System.Text;
using NLog;
using RabbitMQ.Client;

namespace BloombergFLP.CollectdWin
{
    internal class AmqpPlugin : IMetricsWritePlugin
    {
        private const int ConnectionRetryDelay = 60; // 1 minute
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly Object _connectionLock;
        private IModel _channel;
        private bool _connected;
        private IConnection _connection;
        private string _exchange;
        private double _lastConnectTime;
        private string _routingKeyPrefix;
        private string _url;

        public AmqpPlugin()
        {
            _connected = false;
            _lastConnectTime = 0;
            _connectionLock = new object();
        }

        public void Configure()
        {
            var config = ConfigurationManager.GetSection("Amqp") as AmqpPluginConfig;
            if (config == null)
            {
                throw new Exception("Cannot get configuration section : Amqp");
            }

            string user = config.Publish.User;
            string password = config.Publish.Password;
            string host = config.Publish.Host;
            int port = config.Publish.Port;
            string vhost = config.Publish.VirtualHost;

            _url = "amqp://" + user + ":" + password + "@" + host + ":" + port + "/" + vhost;
            _exchange = config.Publish.Exchange;
            _routingKeyPrefix = config.Publish.RoutingKeyPrefix;
            Logger.Info("Amqp plugin configured");
        }

        public void Start()
        {
            Logger.Trace("Start() begin.");
            StartConnection();
            Logger.Info("Amqp plugin started");
        }

        public void Stop()
        {
            Logger.Trace("CloseConnection() begin");
            CloseConnection();
            Logger.Info("Amqp plugin stopped");
        }

        public void Write(MetricValue metric)
        {
            if (metric == null)
            {
                Logger.Debug("write() - Invalid null metric");
                return;
            }
            if (!_connected)
                StartConnection();
            if (_connected && _channel != null)
            {
                string routingKey = GetAmqpRoutingKey(metric);
                string message = metric.GetMetricJsonStr();
                try
                {
                    _channel.BasicPublish(_exchange, routingKey, null, Encoding.UTF8.GetBytes(message));
                }
                catch
                {
                    CloseConnection();
                }
            }
        }

        public void StartConnection()
        {
            double now = Util.GetNow();
            if (now < (_lastConnectTime + ConnectionRetryDelay))
            {
                return;
            }
            lock (_connectionLock)
            {
                try
                {
                    var cf = new ConnectionFactory {Uri = _url};
                    _connection = cf.CreateConnection();
                    _channel = _connection.CreateModel();

                    _connected = true;
                    _lastConnectTime = Util.GetNow();
                    Logger.Debug("Connection started.");
                }
                catch (Exception exp)
                {
                    Logger.Error("Got exception when connecting to AMQP broker : ", exp);
                }
            }
        }

        public void CloseConnection()
        {
            lock (_connectionLock)
            {
                try
                {
                    _channel.Close();
                    _connection.Close();
                    Logger.Debug("Connection closed.");
                }
                catch (Exception exp)
                {
                    Logger.Error("Got exception when closing AMQP connection : ", exp);
                }
                _connected = false;
            }
        }

        private string GetAmqpRoutingKey(MetricValue metric)
        {
            string routingKey = _routingKeyPrefix + "." + metric.HostName + "." + metric.PluginName + "." +
                                metric.PluginInstanceName + "." + metric.TypeName + "." + metric.TypeInstanceName;

            return (routingKey);
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