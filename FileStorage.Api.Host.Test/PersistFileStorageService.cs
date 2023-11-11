using System;
using System.Collections.Generic;
using System.IO;
using System.Text; 
using FileStorage.Contracts.Rest.Impl.FileStorage;
using Service.Registry.Common;

namespace FileStorage.Api.Host.Test
{
    public interface IPersistFileStorageService
    {
        string Save(string externalId, Stream file);
        string Save(string externalId, Stream file, string storageName);

        byte[] Get(string externalId, string storageName);

        byte[] Get(string externalId);

        Stream GetStream(string externalId);
        Stream GetStream(string externalId, string storageName);
        string GetId(string internalId);

        bool Delete(string externalId);
    }
	public sealed class PersistFileStorageService : IPersistFileStorageService
	{
		private readonly IServiceRegistryFactory _serviceRegistryFactory;
		private readonly IFileStorageApiProxy _fileStorageApiProxy;
		public PersistFileStorageService(IServiceRegistryFactory serviceRegistryFactory, IFileStorageApiProxy fileStorageApiProxy)
		{
			_serviceRegistryFactory = serviceRegistryFactory;
			_fileStorageApiProxy = fileStorageApiProxy;
		}


		public string GetId(string externalId)
		{
			return _fileStorageApiProxy.GetId(externalId);
		}

		public bool Delete(string externalId)
		{
			var proxy = GetProxy();
			var id = proxy.GetIdByExternal(externalId);
			proxy.Delete(id, string.Empty);
			return true;
		}

		public string Save(string externalId, Stream file)
		{
			return Save(externalId, file, null);
		}


		public string Save(string externalId, Stream file, string storageName)
		{
			var proxy = GetProxy();
			var id = proxy.GetIdByExternal(externalId);
			proxy.SaveBytes(id, storageName, file, Guid.NewGuid().ToString());
			return id;
			//return _fileStorageApiProxy.Save(externalId, file, storageName);
		}

		public byte[] Get(string externalId, string storageName)
		{
			var proxy = GetProxy();
			var id = proxy.GetIdByExternal(externalId);
			using (var stream = new MemoryStream())
			{
				proxy.WriteToStream(id, storageName, stream);
				stream.Position = 0;
				return stream.ToArray();
			}
		}


		public byte[] Get(string externalId)
		{
			return Get(externalId, null);
		}

		public Stream GetStream(string externalId)
		{
			//var proxy = GetProxy();
			//if (!proxy.Exists(externalId))
			//{
			//	return null;
			//}
			//var stream = proxy.GetStream(externalId);

			var stream = GetContentStream(externalId, null);
			if (stream == null)
				return null;

			var result = new MemoryStream();
			stream.CopyTo(result);
			result.Seek(0, SeekOrigin.Begin);
			return result;
		}

		public Stream GetStream(string id, string storageName)
		{
			var memory = GetContentStream(id, storageName);
			if (memory != null)
				return memory;

			if (_fileStorageApiProxy.IsExists(id, storageName))
			{

				memory = GetContentStream(id, storageName);
				return memory;
			}
			//else if (wcfFileStorageService.ExistsNamed(id, null))
			//{
			//	memory = GetContentStream(id, null);
			//	return memory;
			//}
			return null;

		}

		private Stream GetContentStream(string internalId, string name)
		{
			//var bytes = _cache[internalId];
			//if (bytes != null)
			//{
			//	return new MemoryStream(bytes);
			//}
			if (_fileStorageApiProxy.IsExists(internalId, name))
			{

				var size = _fileStorageApiProxy.GetSize(internalId, name);

				var result = new MemoryStream();

				var buffer = new byte[size / 10];
				var offset = 0;

				while (offset < size)
				{
					buffer = _fileStorageApiProxy.GetBytes(internalId, offset, buffer.Length, name);
					if (buffer == null)
						break;
					offset += buffer.Length;
					result.Write(buffer, 0, buffer.Length);
				}

				result.Seek(0, SeekOrigin.Begin);
				//Save to cache
				var bytes = new byte[result.Length];
				result.Read(bytes, 0, bytes.Length);
				//_cache[internalId] = bytes;

				result.Seek(0, SeekOrigin.Begin);
				return result;
			}
			return null;
		}

		private IFileStorageRestService GetProxy()
		{
			return _serviceRegistryFactory.CreateRest<IFileStorageRestService, FileStorageRestService>();
		}




	}
}
