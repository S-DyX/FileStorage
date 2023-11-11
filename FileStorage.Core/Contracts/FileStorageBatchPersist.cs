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

	public sealed class FileStorageBatchPersist : IDisposable, IFileStorageBatchPersist
	{
		private readonly IWcfFileStorageService _service;
		private readonly string _externalId;
		private readonly string _storageName;
		private string _id;
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
			FileInProcess.Instance.Register(_id);
			_service.DeleteTemp(_id);

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
				_service.DeleteTemp(_id);
				FileInProcess.Instance.Release(_id);
				_isClosed = true;
			}
		}

		public string Write(Stream stream)
		{

			var length = 11000;
			var offset = 0;

			var byteCount = 0;
			var buffer = new byte[length];
			while ((byteCount = stream.Read(buffer, 0, buffer.Length)) > 0)
			{
				bool close = length > byteCount || offset + byteCount > stream.Length;
				var data = Write(buffer, byteCount, close);
				_isOpen = true;

				_id = data.Id;
				offset += length;
			}
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
		private bool _isClosed;
		private bool _isOpen;
		private readonly string _sessionId;

		public FileStorageBatchPersistById(IWcfFileStorageService service, string id)
		{
			FileInProcess.Instance.Register(id);
			_service = service;
			_id = id;
			_service.DeleteTemp(_id);
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
				_service.DeleteTemp(_id);
				FileInProcess.Instance.Release(_id);
				_isClosed = true;
			}
		}

		public string Write(Stream stream)
		{
			var length = 11000;
			var offset = 0;

			var byteCount = 0;
			var buffer = new byte[length];
			while ((byteCount = stream.Read(buffer, 0, buffer.Length)) > 0)
			{
				var l = stream.Length;
				bool close = length > byteCount || offset + byteCount > l;
				var data = Write(buffer, byteCount, close);
				_isOpen = true;
				_id = data.Id;
				offset += length;
			}
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
