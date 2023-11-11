using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace FileStorage.Contracts.Rest.Impl.FileStorage
{
	public sealed class CommandTcpSocketClient : IDisposable
	{
		private string _ip;
		private readonly int _port;
		private TcpClient _clientSocket;
		private bool _isRun;
		private Thread _thread;
		private readonly object _sync = new object();

		public CommandTcpSocketClient(int port)
		{
			_port = port;
			_isRun = true;
			_thread = new Thread(Ping);
			_thread.Start();
		}

		public bool IsConnected => _clientSocket != null && _clientSocket.Connected;

		private bool _isAuth;
		private readonly object _authLock = new object();


		public delegate void ReceiveMessage(TcpCommonMessage message);
		public event ReceiveMessage EventSentMessage;



		private void Ping()
		{
			while (_isRun)
			{
				try
				{
					if (_isAuth && IsConnected)
					{
						if (_lastSend > DateTime.UtcNow.AddSeconds(5))
						{
							Send(new TcpCommonMessage()
							{
								Type = TcpMessageType.Ping
							});
						}
					}

					Thread.Sleep(200);
				}
				catch
				{
					_isAuth = false;

				}
			}
		}


		public void Connect(string ip)
		{
			_ip = ip;
			Auth();
		}

		private void Auth()
		{
			if (_isAuth && IsConnected)
				return;
			lock (_authLock)
			{
				if (_isAuth && IsConnected)
					return;
				AuthLocal();
			}

		}

		private void AuthLocal()
		{
			_isAuth = false;
			if (IsConnected)
				_clientSocket.Close();

			Connect();

			var authMessage = new TcpCommonMessage()
			{
				RequestId = Guid.NewGuid().ToString(),
				Type = TcpMessageType.OpenConnection,
			};

			var bytes = authMessage.GetCommonMessage();
			_clientSocket.Client.SendBytes(bytes);
			var result = Receive();

			if (_clientSocket.Connected)
			{
				_isAuth = true;
			}
		}
		private DateTime _lastSend = DateTime.UtcNow;
		public TcpCommonMessage GetMessage(string requestId)
		{
			TcpCommonMessage data = null;
			var temp = new List<byte>(255);
			var bytes = Receive();
			temp.AddRange(bytes);
			while (!temp.IsReadToEndCommands())
			{
				bytes = Receive();
				temp.AddRange(bytes);
			}

			bytes = temp.ToArray();
			var commands = bytes.GetCommands();
			foreach (var command in commands)
			{
				if (string.IsNullOrWhiteSpace(command))
					continue;
				var deserializeObject = JsonConvert.DeserializeObject<TcpCommonMessage>(command);
				if (deserializeObject != null && deserializeObject.RequestId == requestId)
				{
					data = deserializeObject;
				}
			}

			return data;
		}


		public void Connect(bool retry = true)
		{

			if (_clientSocket != null && _clientSocket.Connected)
			{
				return;

			}

			if (_clientSocket != null)
			{
				_clientSocket.Dispose();
				_clientSocket = null;
			}

			while (_clientSocket == null || !_clientSocket.Connected)
			{
				try
				{
					if (_clientSocket == null)
					{

						_clientSocket = new TcpClient(AddressFamily.InterNetwork);
						//_clientSocket.Connect(AddressFamily.InterNetwork, SocketType.Stream);
						_clientSocket.ReceiveBufferSize = 65536;
						_clientSocket.ReceiveTimeout = 700;
						_clientSocket.SendTimeout = 1000;
					}

					//_localLogger?.Info($"Try to connect:{_ip}:{_port}");
					if (!_clientSocket.Connected)
					{
						_clientSocket.Connect(_ip, _port);
						if (_clientSocket.Connected)
						{
							//_clientSocket.Send(new Byte[] { 32 });

						}

						else if (!_isRun)
						{
							return;
						}
					}

					if (!retry)
						return;

				}
				catch (Exception ex)
				{
					if (!retry)
						throw;
					_clientSocket?.Dispose();
					_clientSocket = null;
					throw;

				}

				Thread.Sleep(500);
			}
		}



		public void Send(TcpCommonMessage message, bool isAuth = false)
		{
			if (!isAuth || !IsConnected)
			{
				Auth();
			}

			Send(JsonConvert.SerializeObject(message));
			EventSentMessage?.Invoke(message);
		}

		public void Send(string command)
		{
			var bytes = System.Text.Encoding.UTF8.GetBytes(command);
			_clientSocket.Client.SendBytes(bytes);
			_lastSend = DateTime.UtcNow;
		}

		public TcpCommonMessage SendReceive(TcpCommonMessage message, bool isAuth = false)
		{
			lock (_sync)
			{
				Send(message);
				var data = GetMessage(message.RequestId);
				return data;
			}

		}
		public byte[] Receive(int timeout = 10000)
		{
			int i = 0;
			var iter = timeout / 10;
			while (_clientSocket.Connected && _clientSocket.Available < 100 && i < iter)
			{
				Thread.Sleep(10);
				i++;
			}

			if (_clientSocket.Available == 0)
				throw new TimeoutException();

			byte[] buffer = new byte[_clientSocket.Available];

			int size = _clientSocket.Client.Receive(buffer);
			return buffer;
		}


		public void Stop()
		{
			_isRun = false;
			if (_clientSocket != null && _clientSocket.Connected)
				_clientSocket?.Close();
			_clientSocket = null;
			_thread = null;

		}

		public void Dispose()
		{
			Stop();
		}
	}
}
