using FileStorage.Core;
using FileStorage.Core.Contracts;
using FileStorage.Core.Helpers;
using FileStorage.Core.Interfaces;
using FileStorage.Core.Interfaces.Settings;
using System;
using System.Collections.Generic;
using System.IO;

namespace FileStorage.Contracts.Impl
{
	public class FolderStorageService : IFolderStorageService
	{

		private readonly IFolderStorageFactory _fileStorageFactory;
		private readonly ICache<byte[]> _cache;
		private readonly IFileStorageSettings _settings;
		private readonly ILocalLogger _logger;

		public FolderStorageService(IFolderStorageFactory fileStorageFactory, ICache<byte[]> cache, IFileStorageSettings settings, ILocalLogger logger)
		{
			_fileStorageFactory = fileStorageFactory;
			_cache = cache;
			_settings = settings;
			_logger = logger;
		}

		private IFolderStorage _defaultFileStorage;


		private IFolderStorage GetStorage(string name)
		{
			if (string.IsNullOrEmpty(name))
			{
				if (_defaultFileStorage == null)
					_defaultFileStorage = _fileStorageFactory.Get("Default");
				return _defaultFileStorage;
			}

			return _fileStorageFactory.Get(name);

		}

		private IFolderStorage CreateStorage(string name)
		{
			if (string.IsNullOrEmpty(name))
			{
				if (_defaultFileStorage == null)
					_defaultFileStorage = _fileStorageFactory.Get("Default");
				return _defaultFileStorage;
			}

			return _fileStorageFactory.Create(name);

		}

		public Stream GetStream(FolderStorageInfo info)
		{
			var fileStorage = GetStorage(info.StorageName);
			if (fileStorage.Exists(info))
			{

				return fileStorage.GetStream(info);
			}
			return null;
		}


		public FileInfoResponse SaveStream(Stream input, FolderStorageInfo info)
		{
			var fileStorage = CreateStorage(info.StorageName);
			var result = fileStorage.SaveStream(input, info);
			return new FileInfoResponse(result.StorageId, result.Hash, result.Length, fileStorage.GetRelativeFileName(info));
		}

		public List<string> GetIds(long offset, int count, string storageName)
		{
			var fileStorage = GetStorage(storageName);
			return fileStorage.GetIds(offset, count);
		}

		public DateTime? GetLogDateById(FolderStorageInfo info)
		{
			var fileStorage = GetStorage(info.StorageName);
			return fileStorage.GetLogDateById(info);
		}

		public long GetCount(string storageName)
		{
			var fileStorage = GetStorage(storageName);
			return fileStorage.GetCount();
		}

		public long GetSize(FolderStorageInfo info)
		{
			var fileStorage = GetStorage(info.StorageName);
			return fileStorage.GetSize(info);
		}


		public FileInfoResponse SaveById(FileBatchRequest request)
		{
			var fileStorage = CreateStorage(request.StorageName);
			if (string.IsNullOrEmpty(request.Id))
			{
				request.Id = request.Name.GetSha1Hash();
			}
			var id = request.Id;
			var bytes = request.Bytes;
			fileStorage.SaveToFile(new SaveFileRequest()
			{
				Bytes = bytes,
				Id = id,
				Close = request.Close,
				FolderId = request.FolderId,
				SessionId = request.SessionId
			});
			return new FileInfoResponse(id, id, bytes.Length, id);
		}

		//public Stream GetStream(long offset)
		//{
		//	return _fileStorage.GetStream(offset);
		//}

		public string GetFullName(FolderStorageInfo info)
		{
			var fileStorage = GetStorage(info.StorageName);
			return fileStorage.GetFullFileName(info);
		}

		public void RefreshLog(string storageName)
		{
			var fileStorage = GetStorage(storageName);
			fileStorage.RefreshLog();
		}

		public FileLogData GetIdsByDate(DateTime time, int take, string storageName)
		{
			var fileStorage = GetStorage(storageName);
			var capacity = take;
			var data = new FileLogData()
			{
				Files = new List<FileData>(capacity),
				DeletedIds = new List<string>(capacity)
			};
			var items = fileStorage.GetLog(time, capacity);
			foreach (var item in items)
			{
				if (data.MaxDate < item.Time)
					data.MaxDate = item.Time;
				switch (item.Type)
				{
					case EventType.FileDelete:
						data.DeletedIds.Add(item.Id);
						break;
					case EventType.FileSave:
						data.Files.Add(new FileData()
						{
							Id = item.Id,
							Time = item.Time,
							Size = item.Size

						});
						break;
				}

			}
			return data;
		}


		public byte[] GetBytes(FolderStorageInfo info)
		{
			var fileStorage = GetStorage(info.StorageName);
			if (!fileStorage.Exists(info))
				return null;

			var res = fileStorage.Get(info);
			if (res == null)
				return null;
			using (var stream = res.Stream)
			{
				var length = stream.Length;
				var b = new byte[length];
				stream.Read(b, 0, (int)length);
				return b;
			}
		}

		public byte[] GetBytesOffset(FolderStorageInfo info, long offset, int size)
		{
			try
			{
				var fileStorage = GetStorage(info.StorageName);
				if (!fileStorage.Exists(info))
					return null;
				var key = $"{info.FolderId}_{info.FileId}_{offset}_{size}";
				var data = _cache[key];
				if (data != null)
					return data;

				var res = fileStorage.Get(info);
				if (res == null)
					return null;
				using (var stream = res.Stream)
				{
					var b = GetBytes(offset, size, stream);
					_cache[key] = b;
					if (b.Length == size)
					{
						for (int i = 0; i < 5; i++)
						{
							offset += size;
							var nextPage = GetBytes(offset, size, stream);
							key = $"{info.FolderId}_{info.FileId}_{offset}_{size}";
							_cache[key] = nextPage;
						}
					}

					return b;
				}
			}
			catch (Exception ex)
			{
				if (_logger != null)
					_logger.Error(ex.Message, ex);
				throw;
			}
		}

		private static byte[] GetBytes(long offset, int size, Stream stream)
		{
			var length = stream.Length;
			if (offset >= stream.Length)
				return new byte[0];
			var l = size + offset;

			if (l > length)
			{
				size = (int)(length - offset);
			}
			var b = new byte[size];
			stream.Position = offset;
			stream.Read(b, 0, size);
			return b;
		}


		public FileInfoResponse SaveBytesByExternal(byte[] bytes, string folderExternalId, string fileExternalId, string storageName)
		{
			var fileStorage = CreateStorage(storageName);
			var info = GetFolderStorageInfo(folderExternalId, fileExternalId, storageName);
			using (var stream = new MemoryStream())
			{
				stream.Write(bytes, 0, bytes.Length);
				stream.Position = 0;
				fileStorage.Save(stream, info);
				return new FileInfoResponse(info.FileId, info.FileId, bytes.Length, fileStorage.GetRelativeFileName(info));
			}
		}


		public Stream GetStreamExternalId(string folderExternalId, string fileExternalId, string storageName)
		{
			var info = GetFolderStorageInfo(folderExternalId, fileExternalId, storageName);
			return GetStream(info);
		}

		private static FolderStorageInfo GetFolderStorageInfo(string folderExternalId, string fileExternalId,
			string storageName)
		{
			var info = new FolderStorageInfo()
			{
				StorageName = storageName,
				FileId = fileExternalId.GetSha1Hash(),
				FolderId = folderExternalId.GetSha1Hash()
			};
			return info;
		}

		public byte[] GetBytesByExternal(string folderExternalId, string fileExternalId, string storageName)
		{
			var info = GetFolderStorageInfo(folderExternalId, fileExternalId, storageName);
			return GetBytes(info);
		}

		public string GetExistIdByExternal(string folderExternalId, string fileExternalId, string storageName)
		{
			if (IsExistsByExternalId(folderExternalId, fileExternalId, storageName))
				return fileExternalId.GetSha1Hash();
			return null;
		}

		public string GetIdByExternal(string externalId)
		{
			return externalId.GetSha1Hash();
		}

		public string GetFullNameByExternal(string folderExternalId, string fileExternalId, string storageName)
		{
			var info = GetFolderStorageInfo(folderExternalId, fileExternalId, storageName);
			var fileStorage = GetStorage(storageName);
			return fileStorage.GetFullFileName(info);
		}

		public void ChangeDepthDirectory(int newDepth, string storageName)
		{
			var fileStorage = GetStorage(storageName);
			fileStorage.ChangeDepthDirectory(_settings.Depth, newDepth);
		}
		public bool IsExistsByExternalId(string folderExternalId, string fileExternalId, string storageName)
		{
			var info = GetFolderStorageInfo(folderExternalId, fileExternalId, storageName);
			return Exists(info);
		}

		public bool Exists(FolderStorageInfo info)
		{
			var fileStorage = GetStorage(info.StorageName);
			return fileStorage.Exists(info);
		}

		public void Move(string toFolderId, string fromFolderId, string toFileId, string storageName)
		{
			if (toFolderId == fromFolderId)
				return;
			var fileStorage = GetStorage(storageName);
			var from = new FolderStorageInfo()
			{
				FileId = toFileId,
				FolderId = fromFolderId,
				StorageName = storageName,
			};
			var to = new FolderStorageInfo()
			{
				FileId = toFileId,
				FolderId = toFolderId,
				StorageName = storageName,
			};
			fileStorage.Move(from, to);
		}

		public void DeleteTemp(FolderStorageInfo info)
		{
			var fileStorage = GetStorage(info.StorageName);
			fileStorage.DeleteTemp(info);
		}

		public void ClearTtl(DateTime date, string storageName)
		{
			var fileStorage = GetStorage(storageName);
			fileStorage.ClearTtl(date);
		}

		public void Delete(FolderStorageInfo info)
		{
			var fileStorage = GetStorage(info.StorageName);
			fileStorage.Delete(info);
		}

	}
}
