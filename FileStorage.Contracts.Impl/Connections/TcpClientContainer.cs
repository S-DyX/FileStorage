using FileStorage.Core.Interfaces;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FileStorage.Contracts.Impl.Connections
{

	public enum TcpClientTypes
	{
		ReadFile = 0,
		WriteFile
	}

	public delegate void ReceiveMessage(byte[] message);

	public interface IUidObject
	{
		string Uid { get; }

		string StorageName { get; }

	}
	public delegate void StopHandler(TcpClientContainerBase item);

	public interface IWebSocketContainer
	{
		event StopHandler StopEvent;


		bool CanCall { get; }

		int CallPerSecond { get; set; }


		int Increment();

		int Decrement();

		void Stop();

		bool Broadcast<T>(T message);

		bool Broadcast(byte[] bytes);
	}

	public abstract class TcpClientContainerBase : IWebSocketContainer
	{
		public TcpClientContainerBase(TcpClientTypes clientType)
		{
			ClientType = clientType;
			CallPerSecond = 24;
		}

		public int Count;
		public int Seconds;
		public int CallPerSecond { get; set; }
		public IUidObject User { get; set; }
		public bool CanCall => CurrentCallPerSeconds <= CallPerSecond;
		public int CurrentCallPerSeconds;
		public DateTime LastSent;
		public ClientInfo ClientInfo { get; set; }
		public TcpClientTypes ClientType { get; }

		public event StopHandler StopEvent;
		public IUidObject Obj { get; protected set; }
		public TcpClient Client { get; protected set; }

		private readonly object _sync = new object();
		public int Increment()
		{
			lock (_sync)
			{
				LastSent = DateTime.UtcNow;
				CurrentCallPerSeconds++;
				return Interlocked.Increment(ref Count);
			}

		}
		public bool Broadcast<T>(T message)
		{
			var json = JsonConvert.SerializeObject(message);
			var bytes = Encoding.UTF8.GetBytes(json);
			var clientClient = Client;
			return Send(clientClient, bytes);
		}
		public virtual bool Broadcast(byte[] bytes)
		{
			return Send(Client, bytes);
		}
		private static bool Send(TcpClient clientClient, byte[] message)
		{
			if (!clientClient.Connected || message == null)
				return false;
			var broadcastStream = clientClient.GetStream();

			message = message.AddHeader();



			broadcastStream.Write(message, 0, message.Length);
			broadcastStream.Flush();
			return true;
		}
		public int Decrement()
		{
			lock (_sync)
			{
				CurrentCallPerSeconds--;
				return Interlocked.Decrement(ref Count);
			}
		}

		public abstract void Start();

		public virtual void Stop()
		{
			StopEvent?.Invoke(this);
		}
	}

	public class TcpClientContainer : TcpClientContainerBase, IDisposable
	{

		private readonly ILocalLogger _localLogger;

		private NetworkStream _stream;
		private readonly IMessageProcessingService _messageProcessingService;
		private bool _isRun;

		//public bool IsRunProcess
		//{
		//    get => _isRun;
		//    set { _isRun = value; }
		//}
		public event ReceiveMessage ReceiveMessageEvent;

		private Task _mainTask;



		public TcpClientContainer(TcpClient client,
			IUidObject obj,
			NetworkStream stream,
			IMessageProcessingService messageProcessingService,
			ILocalLogger localLogger,
			TcpClientTypes clientType,
			int callPerSecond = 24)
			: base(clientType)
		{
			CallPerSecond = callPerSecond;
			_stream = stream;
			_messageProcessingService = messageProcessingService;
			_localLogger = localLogger;
			Obj = obj;
			Client = client;
		}

		public bool Connected => Client.Connected;


		public bool IsRun()
		{
			return _isRun;
		}

		private void Process()
		{
			var buffer = new List<byte>(65525);
			while (_isRun)
			{
				try
				{
					var client = Client;
					client.ReceiveBufferSize = 65525;
					client.WaitData(_stream, IsRun, 10);

					if (!_isRun || !client.Connected)
						return;
					var clientAvailable = client.Available;
					if (clientAvailable == 0)
						continue;

					var bytesFrom = new byte[clientAvailable];

					var receivedSize = _stream.Read(bytesFrom, 0, (int)bytesFrom.Length);
					buffer.AddRange(bytesFrom);
					if (buffer.IsReadToEndCommands())
					{
						_localLogger?.Info($"Received ping, {bytesFrom.Length}");
						var array = buffer.ToArray();
						buffer.Clear();
						var commands = array.GetCommands().Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
						if (commands.Any())
						{
							_messageProcessingService.Process(array, this);
						}
					}


				}
				catch (ObjectDisposedException ode)
				{
					Stop();
					_localLogger?.Error(ode.Message, ode);
				}

				catch (SocketException socketException)
				{
					if (socketException.ErrorCode == 10054)
					{
						Stop();
					}
					_localLogger?.Error(socketException.Message, socketException);
				}
				catch (Exception ex)
				{
					var se = ex.InnerException as SocketException;
					if (se != null && (se.ErrorCode == 10054 || se.ErrorCode == 10053))
					{
						Stop();

					}
					_localLogger?.Error(ex.Message, ex);
				}
			}
		}




		public override void Start()
		{
			if (!_isRun)
			{
				_isRun = true;
				_mainTask = Task.Factory.StartNew(Process);
			}
		}
		public override void Stop()
		{
			try
			{
				if (_isRun)
				{
					_isRun = false;
					//var mainTask = _mainTask;
					var stream = _stream;
					_mainTask = null;
					Client?.Close();
					stream?.Dispose();
					//mainTask?.Dispose();
				}
			}
			catch (Exception e)
			{
				_localLogger?.Error(e.Message, e);
			}

			base.Stop();

		}
		public override bool Broadcast(byte[] message)
		{
			try
			{
				return base.Broadcast(message);
			}
			catch (ObjectDisposedException e)
			{
				Stop();
				_localLogger?.Error(e.Message, e);
				return false;
			}
		}
		public bool Send(byte[] message)
		{
			var clientClient = Client;
			if (clientClient == null || !clientClient.Connected)
				return false;
			try
			{
				var bytes = new List<byte>(message.Length + 2);
				bytes.Add(123);
				bytes.Add((byte)'@');
				bytes.AddRange(BitConverter.GetBytes(DateTime.UtcNow.Ticks));
				bytes.AddRange(BitConverter.GetBytes(message.Length));
				bytes.AddRange(message);
				bytes.Add((byte)'#');

				var broadcastStream = clientClient.GetStream();
				var array = bytes.ToArray();
				broadcastStream.Write(array, 0, array.Length);
				broadcastStream.Flush();

			}
			catch (SocketException se)
			{
				if (se.ErrorCode == 10054 || se.ErrorCode == 10053)
				{
					Stop();
					_localLogger?.Error(se.Message, se);
					return false;
				}
			}
			catch (ObjectDisposedException e)
			{
				Stop();
				_localLogger?.Error(e.Message, e);
				return false;
			}
			return true;
		}
		public bool JustSend(byte[] message)
		{
			var broadcastStream = Client.GetStream();
			broadcastStream.Write(message, 0, message.Length);
			broadcastStream.Flush();
			return true;
		}

		public long? Ping(byte[] message)
		{
			if (Client == null)
				return null;
			long? timestamp = null;

			if (message != null)
			{
				try
				{
					var sw = new Stopwatch();
					sw.Start();
					Broadcast(message);
					sw.Stop();
					timestamp = sw.ElapsedMilliseconds;
				}
				catch (Exception ex)
				{
					_localLogger.Error(ex.Message, ex);
				}

			}

			return timestamp;

		}
		public void Dispose()
		{
			Stop();

		}
	}
}
