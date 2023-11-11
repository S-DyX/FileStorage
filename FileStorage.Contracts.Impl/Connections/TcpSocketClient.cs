using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using FileStorage.Core.Interfaces;
using FileStorage.Core.Interfaces.Settings;
using Newtonsoft.Json;

namespace FileStorage.Contracts.Impl.Connections
{
    public sealed class TcpSocketHotelClient : IDisposable
    {
        private readonly string _host;
        private readonly ILocalLogger _localLogger;
        private TcpClient _clientSocket;
        private NetworkStream _stream;
        private object _sync = new object();
        private Task _processingThread;

        public TcpClient ClientSocket => _clientSocket;
        private bool _isRun;

        private int _port;
        private string _address;
        public TcpSocketHotelClient(IFileStorageSettings settings, ILocalLogger localLogger = null)
        {
            _port = 11581;
            _address = "127.0.0.1";
            var connectionTcp = settings?.Connection?.Tcp;
            if (connectionTcp != null)
            {
                _address = connectionTcp?.Address;
                _port = connectionTcp.Port;
            }

            _host = $"{_address}:{_port}";
            this._localLogger = localLogger;
        }


        public delegate void RobotMessage(TcpCommonMessage[] messages);
        public event RobotMessage EventRobotMessage;


        private bool _isAuth;

        private readonly object _syncAuth = new object();
        public bool IsConnected
        {
            get
            {
                return _clientSocket != null && _clientSocket.Connected;
            }
        }

        public bool IsAuth
        {
            get
            {
                return _isAuth;
            }
        }
        public void Auth()
        {
            if (_isAuth && IsConnected)
                return;
            AuthLocal();
            if (!_isAuth)
                Thread.Sleep(1000);
        }

        private void AuthLocal()
        {


            lock (_syncAuth)
            {
                if (IsConnected && _isAuth)
                    return;

                if (IsConnected)
                    _clientSocket.Close();

                Connect();

                if (!IsConnected)
                {
                    _localLogger?.Error("Connection is close; TcpSocketRobotClient");
                    return;
                }

                _localLogger?.Error("Command Socket start auth");

                var authMessage = GetRobotAuth();
                var bytes = GetBytes(authMessage);
                SendLocal(bytes);

                _localLogger?.Info("Command Socket start auth message sent");
                TcpCommonMessage message = null;
                for (int i = 0; i < 10; i++)
                {
                    message = Process();
                    if (message != null)
                        break;
                    Thread.Sleep(100);
                }
                if (message != null)
                {
                    switch (message.Type)
                    {
                        case TcpMessageType.OpenConnection:
                        case TcpMessageType.Ok:
                            _isAuth = true;
                            _localLogger?.Info("Command connection Established");
                            if (!_isRun)
                            {
                                _isRun = true;

                                EventRobotMessage?.Invoke(new[] { message });
                                if (_processingThread == null)
                                    _processingThread = Task.Factory.StartNew(GetMessage);
                            }

                            break;
                        default:
                            EventRobotMessage?.Invoke(new[] { message });
                            break;
                    }
                }
            }
        }

        private TcpCommonMessage GetRobotAuth()
        {
            var authMessage = new TcpCommonMessage()
            {
                Type = TcpMessageType.OpenConnection,
                UtcNow = DateTime.UtcNow,
            };
            authMessage.RequestId = Guid.NewGuid().ToString();
            return authMessage;
        }

        private void Connect(bool retry = false)
        {
            if (!IsConnected)
            {
                int count = 0;
                _localLogger?.Info($"Command Socket start connect:{_host}");
                lock (_sync)
                {
                    if (IsConnected)
                        return;

                    _isAuth = false;

                    while (!IsConnected)
                    {

                        count++;
                        try
                        {
                            if (_clientSocket != null && _clientSocket.Connected)
                                continue;

                            if (_clientSocket != null)
                            {
                                _clientSocket.Dispose();
                                _clientSocket = null;
                            }

                            if (_clientSocket == null)
                            {
                                _clientSocket = new TcpClient();
                                _clientSocket.ReceiveTimeout = 1000;
                                _clientSocket.SendTimeout = 1000;
                            }
                            _localLogger?.Info($"Command Socket Try to connect:{_host},{_clientSocket.Client.Connected}");
                            if (!_clientSocket.Client.Connected)
                            {
                                _localLogger?.Info($"Command Socket open:{_host},{_clientSocket.Client.Connected}");

                                var ip = IPAddress.Parse(_address);
                                _clientSocket.Connect(ip, _port);
                                _stream = _clientSocket.GetStream();
                                _localLogger?.Info($"Command Socket connection return:{_host},{_clientSocket.Connected}");
                            }

                        }
                        catch (Exception ex)
                        {
                            _localLogger?.Error($"Error to connect:{ex}", ex);
                            _clientSocket?.Dispose();
                            _clientSocket = null;
                            if (count > 100)
                                Thread.Sleep(4000);
                            Thread.Sleep(400);
                            if (!retry)
                                return;


                        }
                    }


                }
            }
        }


        private Dictionary<TcpMessageType, TcpCommonMessage> _commands = new Dictionary<TcpMessageType, TcpCommonMessage>();

        private readonly object _syncCommands = new object();

        public void Send(TcpCommonMessage message, bool withAuth = true)
        {
            lock (_syncCommands)
            {
                _commands[message.Type] = message;
            }

            Thread.Sleep(200);
            byte[] bytes = null;
            lock (_syncCommands)
            {
                var data = _commands.Values.ToArray();
                if (data.Any())
                {
                    bytes = GetBytes(data);
                    _commands.Clear();
                }
            }

            if (bytes != null)
                Send(bytes, withAuth);
        }



        public byte[] GetBytes<TValue>(TValue data)
        {
            var text = JsonConvert.SerializeObject(data);
            return System.Text.Encoding.UTF8.GetBytes(text);
        }
        public void Send(byte[] outStream, bool withAuth = true)
        {
            if (withAuth)
                Auth();
            if (IsConnected)
                SendLocal(outStream);


        }
        private void SendLocal(byte[] bytes)
        {
            //Console.WriteLine("Send data");
            lock (_sync)
            {
                _stream?.SendBytes(bytes);
            }

        }
        private void GetMessage()
        {
            while (_isRun)
            {
                var data = string.Empty;
                try
                {
                    if (!IsConnected || !_isAuth)
                    {
                        Auth();
                        continue;
                    }


                    _localLogger?.Info("Command Socket Receive message");

                    int i = 0;
                    while (_isRun && IsConnected && (!_stream.DataAvailable || _clientSocket.Available == 0))
                    {
                        Thread.Sleep(10);
                        if (i == 50)
                        {
                            _stream.SendBytes(new Byte[] { 32 });
                            i = 0;
                        }

                        i++;
                    }

                    if (_clientSocket.Available > 0 || _stream.DataAvailable)
                    {
                        var bytesFrom = new byte[_clientSocket.Available];
                        var receivedSize = _stream.Read(bytesFrom, 0, (int)bytesFrom.Length);
                        _localLogger?.Info($"Tcp socket message from server. size={receivedSize}");

                        if (receivedSize < 3)
                            continue;



                        _localLogger?.Info($"From server received message data {data}");
                        var commands = bytesFrom.GetCommands();
                        foreach (var command in commands)
                        {
                            if (command.StartsWith("{"))
                            {
                                var single = JsonConvert.DeserializeObject<TcpCommonMessage>(command);
                                Evaluate(new[] { single });
                            }
                            else if (command.StartsWith("["))
                            {
                                var array = JsonConvert.DeserializeObject<TcpCommonMessage[]>(command);
                                if (array != null)
                                    Evaluate(array);
                            }
                            else
                            {

                                var start = 0;
                                var list = new List<TcpCommonMessage>();
                                for (var index = 0; index < command.Length; index++)
                                {
                                    var str = command[index];
                                    switch (str)
                                    {
                                        case '[':
                                            start = index;
                                            break;
                                        case ']':
                                            if (index > start)
                                            {
                                                var value = command.Substring(start, index - start + 1);
                                                var messages = JsonConvert.DeserializeObject<TcpCommonMessage[]>(value);
                                                if (messages != null)
                                                {
                                                    list.AddRange(messages);

                                                }
                                            }

                                            break;

                                    }
                                }

                                if (list.Any())
                                {
                                    Evaluate(list.ToArray());
                                }
                            }
                        }
                    }
                }

                catch (Exception e)
                {
                    _localLogger?.Error($"{data};{e.Message}", e);
                    Thread.Sleep(500);
                }
            }


        }

        private void Evaluate(TcpCommonMessage[] messages)
        {
            EventRobotMessage?.Invoke(messages);

        }

        private static int _count = 0;

        private bool IsRun()
        {
            return _isRun;
        }
        private TcpCommonMessage Process()
        {
            if (!IsConnected)
                return null;
            var bytesFrom = new byte[4096];
            _localLogger?.Info("Try to Receive message");
            lock (_sync)
            {
                if (_stream.DataAvailable)
                {
                    var bytesLen = _stream.Read(bytesFrom, 0, bytesFrom.Length);

                }
                else
                {
                    return null;
                }
            }
            var commands = bytesFrom.GetCommands();
            var last = commands.LastOrDefault();
            if (!string.IsNullOrEmpty(last))
            {
                return JsonConvert.DeserializeObject<TcpCommonMessage>(last);
            }


            return null;
        }


        public void Stop()
        {
            _isRun = false;
            _clientSocket?.Close();
            _stream?.Dispose();
            _stream = null;
            _processingThread?.Dispose();
            _clientSocket = null;

        }

        public void Dispose()
        {
            Stop();
        }
    }
}
