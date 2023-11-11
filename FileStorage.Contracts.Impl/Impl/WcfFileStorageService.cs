using System;
using System.Collections.Generic;
using System.IO;

namespace FileStorage.Contracts.Impl.Impl
{


	public class WcfFileStorageService : IWcfFileStorageService
	{
		private readonly IFileStorageService _fileStorageService;

		public WcfFileStorageService(IFileStorageService fileStorageService)
		{
			_fileStorageService = fileStorageService;
		}

		public Stream GetStream(string id)
		{
			return _fileStorageService.GetStream(id, null);
		}

		public Stream GetStreamNamed(string id, string storageName)
		{
			return _fileStorageService.GetStream(id, storageName);
		}

		public FileInfoResponse SaveStream(Stream stream)
		{
			//if (OperationContext.Current.IncomingMessageHeaders != null)
			//{
			//	MessageHeaders headers = OperationContext.Current.IncomingMessageHeaders;
			//	string identity = headers.GetHeader<string>("externalId", "http://www.file.mindscan.ru");
			//	if (!string.IsNullOrEmpty(identity))
			//		return _fileStorageService.SaveStream(stream, identity.GetSha1Hash());

			//}
			return _fileStorageService.SaveStream(stream, null);
		}

		public byte[] GetBytes(string id)
		{
			return _fileStorageService.GetBytes(id, null);
		}

		public byte[] GetBytesNamed(string id, string storageName)
		{
			return _fileStorageService.GetBytes(id, storageName);
		}

		public FileInfoResponse SaveBytes(byte[] bytes)
		{
			return _fileStorageService.SaveBytes(bytes, null);
		}

		public FileInfoResponse SaveBytesByExternal(byte[] bytes, string externalId)
		{
			return _fileStorageService.SaveBytesByExternal(bytes, externalId, null);
		}

		public byte[] GetBytesByExternal(string externalId)
		{
			return _fileStorageService.GetBytesByExternal(externalId, null);
		}

		public string GetExistIdByExternal(string externalId)
		{
			return _fileStorageService.GetExistIdByExternal(externalId, null);
		}

		public string GetIdByExternal(string externalId)
		{
			return _fileStorageService.GetIdByExternal(externalId);
		}

		public string GetFullNameByExternal(string externalId)
		{

			return _fileStorageService.GetFullNameByExternal(externalId, null);
		}

		public string GetFullName(string id)
		{
			return _fileStorageService.GetFullName(id, null);
		}

		public void RefreshLog()
		{
			_fileStorageService.RefreshLog(null);
		}

		//public List<string> GetIds(long offset, int count)
		//{
		//	return _fileStorageService.GetIds(offset, count);
		//}

		//public List<string> GetFromIds(string fromId, long offset, int count)
		//{
		//	return _fileStorageService.GetFromIds(fromId, offset, count);
		//}

		public FileLogData GetIdsByDate(DateTime time, int take)
		{
			return _fileStorageService.GetIdsByDate(time, take, null);
		}

		public List<string> GetIds(long offset, int count)
		{
			return _fileStorageService.GetIds(offset, count, null);
		}

		public DateTime? GetLogDateById(string id)
		{
			return _fileStorageService.GetLogDateById(id, null);
		}

		public long GetCount()
		{
			return _fileStorageService.GetCount(null);
		}

		public long GetSize(string id)
		{
			return _fileStorageService.GetSize(id, null);
		}

		public FileInfoResponse SaveByName(string name, byte[] bytes, bool close)
		{
		
			return _fileStorageService.SaveById(new FileBatchRequest()
			{
				Name = name,
				Bytes = bytes,
				Close = close,

			});
		}

		public FileInfoResponse SaveById(string id, byte[] bytes, bool close)
		{
			
			return _fileStorageService.SaveById(new FileBatchRequest()
			{
				Id = id,
				Bytes = bytes,
				Close = close,

			});
		}

		public FileInfoResponse SaveByIdNamed(string id, byte[] bytes, bool close, string storageName)
		{
			return _fileStorageService.SaveById(new FileBatchRequest()
			{
				Id = id,
				Bytes = bytes,
				Close = close,
				StorageName = storageName
			});
		}
		public FileInfoResponse SaveByBatchRequest(FileBatchRequest request)
		{
			return _fileStorageService.SaveById(request);
		}
		public void Delete(string id)
		{
			if (_fileStorageService.Exists(id, null))
				_fileStorageService.Delete(id, null);
		}

		public bool Exists(string hash)
		{
			return _fileStorageService.Exists(hash, null);
		}

		public bool ExistsNamed(string hash, string storageName)
		{
			return _fileStorageService.Exists(hash, storageName);
		}

		public void Move(string fromId, string toId)
		{
			_fileStorageService.Move(fromId, toId, null);
		}

		public void DeleteTemp(string id)
		{
			_fileStorageService.DeleteTemp(id, null);
		}

		public void ClearTtl(DateTime date)
		{
			_fileStorageService.ClearTtl(date, null);
		}

		public byte[] GetBytesOffset(string id, long offset, int size)
		{
			return _fileStorageService.GetBytesOffset(id, offset, size, null);
		}


	}
}
