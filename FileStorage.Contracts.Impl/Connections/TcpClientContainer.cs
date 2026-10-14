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
			// JSON пишется сразу в кадр: без строки, её UTF-8 копии и копий в AddHeader.
			// Раньше ответ с файлом занимал в памяти 6–8 размеров файла, теперь — около двух.
			using (var frame = SocketExt.SerializeFrame(message, EstimateFrameSize(message)))
			{
				return SendFrame(Client, frame.GetBuffer(), (int)frame.Length);
			}
		}

		private static int EstimateFrameSize<T>(T message)
		{
			// base64 увеличивает данные в 4/3 раза; запас — под остальные поля
			var common = (object)message as TcpCommonMessage;
			var content = common?.ReadCommand?.Content ?? common?.WriteCommand?.Content;
			return content == null ? 512 : (int)Math.Min(int.MaxValue / 2, content.LongLength * 4 / 3 + 1024);
		}

		private static bool SendFrame(TcpClient client, byte[] frame, int length)
		{
			if (client == null || !client.Connected)
				return false;
			var broadcastStream = client.GetStream();
			broadcastStream.Write(frame, 0, length);
			broadcastStream.Flush();
			return true;
		}
		public virtual bool Broadcast(byte[] bytes)
		{
			return Send(Client, bytes);
		}
		private static bool Send(TcpClient clientClient, byte[] message)
		{
			if (message == null)
				return false;
			var frame = message.AddHeader();
			return SendFrame(clientClient, frame, frame.Length);
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

		private Thread _mainTask;



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
			// Блокирующее чтение в переиспользуемый буфер. Раньше поток опрашивал сокет каждые 5 мс,
			// на каждую порцию данных создавал новый массив, List<byte> и его копию, а команды разбирал дважды.
			// Stop() закрывает соединение, и Read завершается исключением или нулём.
			var readBuffer = new byte[64 * 1024];
			var frameReader = new TcpFrameReader();
			try
			{
				Client.ReceiveBufferSize = 65536;
			}
			catch (Exception ex)
			{
				_localLogger?.Error(ex.Message, ex);
			}

			while (_isRun)
			{
				try
				{
					var stream = _stream;
					if (stream == null)
						return;
					var received = stream.Read(readBuffer, 0, readBuffer.Length);
					if (received == 0)
					{
						// удалённая сторона закрыла соединение
						Stop();
						return;
					}

					var commands = frameReader.Append(readBuffer, received);
					if (commands.Count > 0)
					{
						_localLogger?.Info($"Received commands: {commands.Count}");
						_messageProcessingService.Process(commands, this);
					}
				}
				catch (ObjectDisposedException ode)
				{
					if (!_isRun)
						return; // соединение закрыто через Stop()
					Stop();
					_localLogger?.Error(ode.Message, ode);
					return;
				}
				catch (SocketException socketException)
				{
					if (!_isRun)
						return;
					if (socketException.ErrorCode == 10054)
					{
						Stop();
					}
					_localLogger?.Error(socketException.Message, socketException);
				}
				catch (Exception ex)
				{
					if (!_isRun)
						return;
					var se = ex.InnerException as SocketException;
					if (se != null && (se.ErrorCode == 10054 || se.ErrorCode == 10053))
					{
						Stop();
					}
					_localLogger?.Error(ex.Message, ex);
				}

				// при оборванном соединении Read сразу упадёт снова — не крутимся в цикле
				var client = Client;
				if (client == null || !client.Connected)
				{
					Stop();
					return;
				}
			}
		}

		public override void Start()
		{
			if (!_isRun)
			{
				_isRun = true;
				_mainTask = new Thread(Process);
				_mainTask.Start();
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
				// '{' '@' + время (Int64) + длина (Int32) + данные + '#' — один массив вместо List<byte> и ToArray
				var array = new byte[message.Length + 15];
				array[0] = 123;
				array[1] = (byte)'@';
				BitConverter.TryWriteBytes(new Span<byte>(array, 2, 8), DateTime.UtcNow.Ticks);
				BitConverter.TryWriteBytes(new Span<byte>(array, 10, 4), message.Length);
				Buffer.BlockCopy(message, 0, array, 14, message.Length);
				array[array.Length - 1] = (byte)'#';

				var broadcastStream = clientClient.GetStream();
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
