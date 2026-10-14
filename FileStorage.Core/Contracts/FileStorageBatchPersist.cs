using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileStorage.Contracts
{
	public interface IFileStorageBatchPersist
	{
		string Write(Stream stream);
		string Write(string value);

		FileInfoResponse Write(byte[] buffer, int byteCount, bool close);
	}

	internal static class BatchStreamHelper
	{
		public const int ChunkSize = 11000;

		/// <summary>
		/// Читает из потока, пока буфер не заполнится или поток не закончится.
		/// Stream.Read может вернуть меньше запрошенного, даже если данные ещё есть.
		/// </summary>
		public static int ReadFull(Stream stream, byte[] buffer)
		{
			var total = 0;
			int read;
			while (total < buffer.Length && (read = stream.Read(buffer, total, buffer.Length - total)) > 0)
			{
				total += read;
			}
			return total;
		}
	}

	public sealed class FileStorageBatchPersist : IDisposable, IFileStorageBatchPersist
	{
		private readonly IWcfFileStorageService _service;
		private readonly string _externalId;
		private readonly string _storageName;
		private string _id;
		// id, под которым файл зарегистрирован в FileInProcess. _id может смениться после Write,
		// а освобождать нужно именно зарегистрированный ключ.
		private readonly string _registeredId;
		private bool _isClosed;
		private bool _isOpen;
		private readonly string _sessionId;

		public FileStorageBatchPersist(IWcfFileStorageService service, string externalId, string storageName = null)
		{
			_service = service;
			_externalId = externalId;
			_storageName = storageName;
			_sessionId = Guid.NewGuid().ToString();
			_id = _service.GetIdByExternal(externalId);
			_registeredId = _id;
			FileInProcess.Instance.Register(_registeredId);
			try
			{
				_service.DeleteTemp(_id);
			}
			catch
			{
				// объект не создан, Dispose не вызовут — иначе ключ остался бы занятым навсегда
				FileInProcess.Instance.Release(_registeredId);
				throw;
			}

		}
		public void Dispose()
		{
			Close();

			//if (_isOpen && !_isClosed && !string.IsNullOrEmpty(_id))
		}

		public void Close()
		{
			if (!_isClosed)
			{
				_isClosed = true;
				try
				{
					_service.DeleteTemp(_id);
				}
				finally
				{
					// ключ освобождаем, даже если удаление временного файла не удалось
					FileInProcess.Instance.Release(_registeredId);
				}
			}
		}

		public string Write(Stream stream)
		{
			// Блок отправляется только после чтения следующего: так заранее известно, какой блок последний,
			// и не нужен stream.Length (его нет у сетевых потоков). Раньше файл размером, кратным 11000 байт,
			// никогда не закрывался, а неполное чтение посреди потока закрывало его раньше времени.
			var buffer = new byte[BatchStreamHelper.ChunkSize];
			var next = new byte[BatchStreamHelper.ChunkSize];
			var byteCount = BatchStreamHelper.ReadFull(stream, buffer);
			do
			{
				var nextCount = byteCount > 0 ? BatchStreamHelper.ReadFull(stream, next) : 0;
				var data = Write(buffer, byteCount, nextCount == 0);
				_isOpen = true;
				_id = data.Id;

				var tmp = buffer;
				buffer = next;
				next = tmp;
				byteCount = nextCount;
			}
			while (byteCount > 0);
			return _id;
		}

		public string Write(string value)
		{
			var send = Encoding.UTF8.GetBytes(value);
			var request = new FileBatchRequest()
			{
				Id = _id,
				Bytes = send,
				Close = true,
				StorageName = _storageName,
				SessionId= _sessionId
			};
			var data = _service.SaveByBatchRequest(request);
			return _id;
		}

		public FileInfoResponse Write(byte[] buffer, int byteCount, bool close)
		{
			var send = new byte[byteCount];
			Array.Copy(buffer, send, byteCount);
			var data = _service.SaveByBatchRequest(new FileBatchRequest()
			{
				Id = _id,
				Bytes = send,
				Close = close,
				StorageName = _storageName,
				SessionId = _sessionId
			});
			return data;
		}
	}



	public sealed class FileStorageBatchPersistById : IDisposable, IFileStorageBatchPersist
	{
		private readonly IWcfFileStorageService _service;

		private string _id;
		// id, под которым файл зарегистрирован в FileInProcess. _id может смениться после Write,
		// а освобождать нужно именно зарегистрированный ключ.
		private readonly string _registeredId;
		private bool _isClosed;
		private bool _isOpen;
		private readonly string _sessionId;

		public FileStorageBatchPersistById(IWcfFileStorageService service, string id)
		{
			FileInProcess.Instance.Register(id);
			_registeredId = id;
			_service = service;
			_id = id;
			try
			{
				_service.DeleteTemp(_id);
			}
			catch
			{
				// объект не создан, Dispose не вызовут — иначе ключ остался бы занятым навсегда
				FileInProcess.Instance.Release(_registeredId);
				throw;
			}
			_sessionId = Guid.NewGuid().ToString();
		}

		public void Dispose()
		{
			Close();

			//if (_isOpen && !_isClosed && !string.IsNullOrEmpty(_id))
		}

		public void Close()
		{
			if (!_isClosed)
			{
				_isClosed = true;
				try
				{
					_service.DeleteTemp(_id);
				}
				finally
				{
					// ключ освобождаем, даже если удаление временного файла не удалось
					FileInProcess.Instance.Release(_registeredId);
				}
			}
		}

		public string Write(Stream stream)
		{
			// Блок отправляется только после чтения следующего: так заранее известно, какой блок последний,
			// и не нужен stream.Length (его нет у сетевых потоков). Раньше файл размером, кратным 11000 байт,
			// никогда не закрывался, а неполное чтение посреди потока закрывало его раньше времени.
			var buffer = new byte[BatchStreamHelper.ChunkSize];
			var next = new byte[BatchStreamHelper.ChunkSize];
			var byteCount = BatchStreamHelper.ReadFull(stream, buffer);
			do
			{
				var nextCount = byteCount > 0 ? BatchStreamHelper.ReadFull(stream, next) : 0;
				var data = Write(buffer, byteCount, nextCount == 0);
				_isOpen = true;
				_id = data.Id;

				var tmp = buffer;
				buffer = next;
				next = tmp;
				byteCount = nextCount;
			}
			while (byteCount > 0);
			return _id;
		}

		public string Write(string value)
		{
			throw new NotImplementedException();
		}

		public FileInfoResponse Write(byte[] buffer, int byteCount, bool close)
		{
			var send = new byte[byteCount];
			Array.Copy(buffer, send, byteCount);
			var request = new FileBatchRequest()
			{
				Id = _id,
				Bytes = send,
				Close = close,
				SessionId = _sessionId
			};
			var data = _service.SaveByBatchRequest(request);
			return data;
		}
	}
}
