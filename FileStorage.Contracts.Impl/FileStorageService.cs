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
    public class FileStorageService : IFileStorageService
    {

        private readonly IFileStorageFactory<string> _fileStorageFactory;
        private readonly ICache<byte[]> _cache;
        private readonly IFileStorageSettings _settings;
        private readonly ILocalLogger _logger;

        //public FileStorageService(IFileStorage<string> fileStorage, ICache<byte[]> cache, Castle.Core.Logging.ILogger logger)
        public FileStorageService(IFileStorageFactory<string> fileStorageFactory, ICache<byte[]> cache, IFileStorageSettings settings, ILocalLogger logger)
        {
            _fileStorageFactory = fileStorageFactory;
            _cache = cache;
            _settings = settings;
            _logger = logger;
        }

        private IFileStorage<string> _defaultFileStorage;


        private IFileStorage<string> GetStorage(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                if (_defaultFileStorage == null)
                    _defaultFileStorage = _fileStorageFactory.Get("Default");
                return _defaultFileStorage;
            }

            return _fileStorageFactory.Get(name);

        }

        private IFileStorage<string> CreateStorage(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                if (_defaultFileStorage == null)
                    _defaultFileStorage = _fileStorageFactory.Get("Default");
                return _defaultFileStorage;
            }

            return _fileStorageFactory.Create(name);

        }

        public Stream GetStream(string id, string storageName)
        {
            var fileStorage = GetStorage(storageName);
            if (fileStorage.Exists(id))
            {

                return fileStorage.GetStream(id);
            }
            return null;
        }



        public FileInfoResponse SaveStream(Stream stream, string storageName)
        {
            var fileStorage = CreateStorage(storageName);
            var result = fileStorage.SaveAsHash(stream);
            return new FileInfoResponse(result.StorageId, result.Hash, result.Length, fileStorage.GetRelativeFileName(result.StorageId));
        }
        public FileInfoResponse SaveStream(Stream input, string id, string storageName)
        {
            var fileStorage = CreateStorage(storageName);
            var result = fileStorage.SaveStream(input, id);
            return new FileInfoResponse(result.StorageId, result.Hash, result.Length, fileStorage.GetRelativeFileName(result.StorageId));
        }

        public List<string> GetIds(long offset, int count, string storageName)
        {
            var fileStorage = GetStorage(storageName);
            return fileStorage.GetIds(offset, count);
        }

        public DateTime? GetLogDateById(string id, string storageName)
        {
            var fileStorage = GetStorage(storageName);
            return fileStorage.GetLogDateById(id);
        }

        public long GetCount(string storageName)
        {
            var fileStorage = GetStorage(storageName);
            return fileStorage.GetCount();
        }

        public long GetSize(string id, string storageName)
        {
            var fileStorage = GetStorage(storageName);
            return fileStorage.GetSize(id);
        }



        //private void ClearCache()
        //{
        //	_cache.Clear();
        //}

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
                SessionId = request.SessionId
            });
            return new FileInfoResponse(id, id, bytes.Length, id);
        }

        //public Stream GetStream(long offset)
        //{
        //	return _fileStorage.GetStream(offset);
        //}

        public string GetFullName(string id, string storageName)
        {
            var fileStorage = GetStorage(storageName);
            return fileStorage.GetFullFileName(id);
        }

        public void RefreshLog(string storageName)
        {
            var fileStorage = GetStorage(storageName);
            fileStorage.RefreshLog();
        }

        //public List<string> GetIds(long offset, int count)
        //{
        //	return _fileStorage.GetIds(offset, count);
        //}

        //public List<string> GetFromIds(string fromId, long offset, int count)
        //{
        //	return _fileStorage.GetIds(fromId, offset, count);
        //}

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


        public byte[] GetBytes(string id, string storageName)
        {
            var fileStorage = GetStorage(storageName);
            if (!fileStorage.Exists(id))
                return null;

            var res = fileStorage.Get(id);
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

        public byte[] GetBytesOffset(string id, long offset, int size, string storageName)
        {
            try
            {
                var fileStorage = GetStorage(storageName);
                if (!fileStorage.Exists(id))
                    return null;
                var key = $"{id}_{offset}_{size}";
                var data = _cache[key];
                if (data != null)
                    return data;

                var res = fileStorage.Get(id);
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
                            key = $"{id}_{offset}_{size}";
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

        public void SaveBytes(string id, byte[] bytes, int offset, int size, string storageName)
        {
            throw new NotImplementedException();
        }

        public FileInfoResponse SaveBytes(byte[] bytes, string storageName)
        {
            var fileStorage = CreateStorage(storageName);
            using (var stream = new MemoryStream())
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Position = 0;
                var res = fileStorage.SaveAsHash(stream);
                return new FileInfoResponse(res.StorageId, res.Hash, res.Length, fileStorage.GetRelativeFileName(res.StorageId));
            }
        }

        public FileInfoResponse SaveBytesByExternal(byte[] bytes, string externalId, string storageName)
        { 
            using (var stream = new MemoryStream())
            { 
                stream.Write(bytes, 0, bytes.Length);
                stream.Position = 0;
                var hash = externalId.GetSha1Hash();
                return SaveStream(stream, hash, storageName);
            }
        }


        public Stream GetStreamExternalId(string externalId, string storageName)
        {
            var hash = externalId.GetSha1Hash();
            return GetStream(hash, storageName);
        }

        public byte[] GetBytesByExternal(string externalId, string storageName)
        {
            return GetBytes(externalId.GetSha1Hash(), storageName);
        }

        public string GetExistIdByExternal(string externalId, string storageName)
        {
            if (IsExistsByExternalId(externalId, storageName))
                return externalId.GetSha1Hash();
            return null;
        }

        public string GetIdByExternal(string externalId)
        {
            return externalId.GetSha1Hash();
        }

        public string GetFullNameByExternal(string externalId, string storageName)
        {
            var fileStorage = GetStorage(storageName);
            return fileStorage.GetFullFileName(externalId.GetSha1Hash());
        }

        public void ChangeDepthDirectory(int newDepth, string storageName)
        {
            var fileStorage = GetStorage(storageName);
            fileStorage.ChangeDepthDirectory(_settings.Depth, newDepth);
        }
        public bool IsExistsByExternalId(string externalId, string storageName)
        {
            var hash = externalId.GetSha1Hash();
            return Exists(hash, storageName);
        }

        public bool Exists(string hash, string storageName)
        {
            var fileStorage = GetStorage(storageName);
            return fileStorage.Exists(hash);
        }

        public void Move(string fromId, string toId, string storageName)
        {
            var fileStorage = GetStorage(storageName);
            fileStorage.Move(fromId, toId);
        }

        public void DeleteTemp(string id, string storageName)
        {
            var fileStorage = GetStorage(storageName);
            fileStorage.DeleteTemp(id);
        }

        public void ClearTtl(DateTime date, string storageName)
        {
            var fileStorage = GetStorage(storageName);
            fileStorage.ClearTtl(date);
        }

        public void Delete(string id, string storageName)
        {
            var fileStorage = GetStorage(storageName);
            fileStorage.Delete(id);
        }

        //public string GetLatestId()
        //{
        //	return _fileStorage.GetLatestId();
        //}
    }
}
