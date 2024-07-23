using FileStorage.Core.Interfaces;
using FileStorage.Core.Interfaces.Settings;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace FileStorage.Contracts.Impl.Connections
{
	public class TcpCommandServer : IDisposable
	{
		private readonly IFileStorageSettings _settings;
		private readonly ITcpClientRegistry _clientRegistry;
		private readonly IMessageProcessingService _messageProcessingService;
		private readonly ILocalLogger _localLogger;
		private readonly TcpListener _serverSocket;
		private bool _isRun;
		public TcpCommandServer(IFileStorageSettings settings,
			ITcpClientRegistry clientRegistry,
			IMessageProcessingService messageProcessingService,
			ILocalLogger localLogger)
		{
			_settings = settings;
			_clientRegistry = clientRegistry;
			_messageProcessingService = messageProcessingService;
			_localLogger = localLogger;
			_serverSocket = new TcpListener(IPAddress.Parse(_settings.Connection.Tcp.Address), _settings.Connection.Tcp.Port);
			_isRun = true;
		}


		private bool IsSteelRun()
		{
			return _isRun;
		}


		public void Start()
		{
			if (!_isRun)
			{
				_isRun = true;
				_clientRegistry.Start();
			}

			StartTcpListener();
		}

		private void StartTcpListener()
		{
			_serverSocket.Start();
			var callStart = false;
			_localLogger?.Info($"Start tcp server {_settings.Connection.Tcp.Address}:{_settings.Connection.Tcp.Port}");
			while (_isRun)
			{
				try
				{
					if (callStart)
						_serverSocket.Start();
					var clientSocket = _serverSocket.AcceptTcpClientAsync().Result;
					clientSocket.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);

					_localLogger?.Info($"AcceptTcpClient {clientSocket.Client.RemoteEndPoint}");
					var stream = clientSocket.GetStream();
					clientSocket.ReceiveBufferSize = 65536;
					clientSocket.WaitDataAndPing(stream, IsSteelRun);

					var bytesFrom = new byte[clientSocket.Available];
					var receivedSize = stream.Read(bytesFrom, 0, (int)bytesFrom.Length);


					var response = _messageProcessingService.Process(bytesFrom.Take(receivedSize).ToArray(), null);
					_localLogger?.Info($"Tcp client processed: {clientSocket.Client.RemoteEndPoint}");


					TcpClientContainerBase tcpChatClient = null;
					var commonRobotMessage = new TcpCommonMessage()
					{
						RequestId = response.Id,
						Type = TcpMessageType.Ok,
						UtcNow = DateTime.UtcNow
					};
					switch (response.Type)
					{

						case TcpMessageType.OpenConnection:
							tcpChatClient = _clientRegistry.AddCommand(clientSocket, stream, string.Empty,
								_messageProcessingService);
							break;

					}

					if (tcpChatClient != null)
					{
						response.Bytes = GetBytes(commonRobotMessage);
						clientSocket.SendWithHeaders(response.Bytes);
						_localLogger?.Info(response.Id + $"Connection established");
					}
					else
					{
						clientSocket.SendWithHeaders(response.Bytes);
						clientSocket.Close();
					}
				}
				catch (InvalidOperationException ioe)
				{
					_localLogger?.Error(ioe.Message, ioe);
					if (ioe.Message.Contains("You must call the Start()"))
					{
						callStart = true;
						Thread.Sleep(500);

					}
				}
				catch (Exception e)
				{
					_localLogger?.Error(e.Message, e);
					Thread.Sleep(20);
				}
			}
		}

		private static byte[] GetBytes(TcpCommonMessage commonHotelMessage)
		{
			return Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(commonHotelMessage));
		}

		public void Stop()
		{
			_clientRegistry.Stop();
			_isRun = false;
		}



		public void Dispose()
		{
			_clientRegistry?.Dispose();
			_serverSocket?.Stop();
		}
	}
}
