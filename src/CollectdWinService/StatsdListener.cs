using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using NLog;

namespace BloombergFLP.CollectdWin
{
    internal class StatsdListener
    {
        public delegate void HandleMessage(string message);

        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private readonly IPEndPoint _endPoint;

        private readonly HandleMessage _messageHandler;
        private readonly Socket _socket;
        private bool _run;

        public StatsdListener(int port, HandleMessage handleMessage)
        {
            _messageHandler = handleMessage;
            _endPoint = new IPEndPoint(IPAddress.Any, port);
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        }

        private void BindSocket()
        {
            while (!_socket.IsBound)
            {
                try
                {
                    _socket.Bind(_endPoint);
                }
                catch (Exception exp)
                {
                    Logger.Error("BindSocket failed: ", exp);
                }
                if (_socket.IsBound)
                    break;
                Thread.Sleep(10*1000);
            }
        }

        private void CloseSocket()
        {
            try
            {
                _socket.Shutdown(SocketShutdown.Both);
                _socket.Close();
            }
            catch (Exception exp)
            {
                Logger.Error("CloseSocket failed: ", exp);
            }
        }

        public void Start()
        {
            Logger.Trace("Start() begin");
            var buffer = new byte[1024*4];

            var sender = new IPEndPoint(IPAddress.Any, 0);
            EndPoint remote = sender;

            _run = true;
            while (_run)
            {
                if (!_socket.IsBound)
                {
                    BindSocket();
                }
                try
                {
                    int recv = _socket.ReceiveFrom(buffer, ref remote);
                    string str = Encoding.ASCII.GetString(buffer, 0, recv);
                    str = str.TrimEnd('\r', '\n');

                    _messageHandler(str);
                }
                catch
                {
                    CloseSocket();
                }
            }

            Logger.Trace("Start() end");
        }

        public void Stop()
        {
            Logger.Trace("Stop() begin");
            _run = false;
            // closing socket will cause Socket.ReceiveFrom() blocked call to
            // throw SocketException, a work-around for shutting down a listener.
            CloseSocket();
            Logger.Trace("Stop() end");
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