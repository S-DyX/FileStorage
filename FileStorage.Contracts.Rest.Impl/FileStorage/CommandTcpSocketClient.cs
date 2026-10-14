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
					// Раньше условие было _lastSend > UtcNow + 5 с — оно никогда не выполнялось, и пинг не отправлялся.
					// Пинг идёт под той же блокировкой, что и запросы, чтобы кадры не перемешались в сокете.
					if (_isAuth && IsConnected && _lastSend < DateTime.UtcNow.AddSeconds(-5) && Monitor.TryEnter(_sync))
					{
						try
						{
							Send(new TcpCommonMessage()
							{
								Type = TcpMessageType.Ping
							}, true);
						}
						finally
						{
							Monitor.Exit(_sync);
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

			// после переподключения недочитанные кадры прежнего соединения не нужны
			_frameReader.Clear();
			var bytes = authMessage.GetCommonMessage();
			_clientSocket.Client.SendBytes(bytes);
			// ждём ответ именно на рукопожатие
			GetMessage(authMessage.RequestId);

			if (_clientSocket.Connected)
			{
				_isAuth = true;
			}
		}
		private DateTime _lastSend = DateTime.UtcNow;

		// кадры ответов, полученные частично, дочитываются при следующем Receive
		private readonly TcpFrameReader _frameReader = new TcpFrameReader();
		public TcpCommonMessage GetMessage(string requestId, int timeout = 10000)
		{
			// Читаем кадры, пока не придёт ответ именно на этот запрос. Раньше разбирался только первый кадр,
			// и кадр считался полным раньше времени: запоздавший ответ на прерванный запрос или неполный кадр
			// давали null вместо ответа.
			var deadline = DateTime.UtcNow.AddMilliseconds(timeout);
			while (true)
			{
				var remaining = (int)(deadline - DateTime.UtcNow).TotalMilliseconds;
				if (remaining <= 0)
					throw new TimeoutException();
				var bytes = Receive(remaining);
				foreach (var command in _frameReader.Append(bytes, bytes.Length))
				{
					var message = JsonConvert.DeserializeObject<TcpCommonMessage>(command);
					if (message != null && message.RequestId == requestId)
						return message;
				}
			}
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
			var client = _clientSocket;
			var deadline = DateTime.UtcNow.AddMilliseconds(timeout);
			// Ждём любые данные. Раньше ждали минимум 100 байт, и на коротком ответе уходило 10 с до таймаута.
			while (client != null && client.Connected && client.Available == 0 && DateTime.UtcNow < deadline)
			{
				Thread.Sleep(1);
			}

			if (client == null || client.Available == 0)
				throw new TimeoutException();

			var buffer = new byte[client.Available];
			var size = client.Client.Receive(buffer);
			// Receive может прочитать меньше, чем Available: раньше хвост из нулей попадал в разбор ответа
			if (size < buffer.Length)
				Array.Resize(ref buffer, size);
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
