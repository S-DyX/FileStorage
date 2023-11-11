using System;
using System.Collections.Generic;
using System.IO;
using System.Text; 
using FileStorage.Contracts.Rest.Impl.FileStorage;
using Service.Registry.Common;

namespace FileStorage.Api.Host.Test
{
	public class FileStorageApiProxy : IFileStorageApiProxy
	{

		private readonly IServiceRegistryFactory _serviceRegistryFactory;
		private IFileStorageRestService _host;

		private IFileStorageRestService Host
		{
			get
			{
				if (_host == null)
				{
					_host = _serviceRegistryFactory.CreateRest<IFileStorageRestService, FileStorageRestService>();

				}

				return _host;
			}
		}

		public FileStorageApiProxy(IServiceRegistryFactory serviceRegistryFactory)
		{
			_serviceRegistryFactory = serviceRegistryFactory;

		}

		public byte[] GetBytes(string id, long offset, int size, string storageName)
		{

			try
			{
				return Host.GetBytes(id, offset, size, storageName);
			}
			catch (Exception ex)
			{
				//_logger?.Error($"Url:{url}. {ex}", ex);
				throw;
			}
		}

		public byte[] GetBytes(string id, long offset, int size)
		{

			try
			{
				return Host.GetBytes(id, offset, size, null);
			}
			catch (Exception ex)
			{
				//_logger?.Error($"Url:{url}. {ex}", ex);
				throw;
			}
		}

        public bool IsExists(string id)
        {
			return Host.Exists(id,  string.Empty);
		}

        public string GetId(string internalId)
		{
			try
			{
				return Host.GetIdByExternal(internalId);

			}
			catch (Exception ex)
			{
				//_logger?.Error($"Url:{url}. {ex}", ex);
				throw;
			}
		}

		public int GetSize(string id, string storageName)
		{
			try
			{

				return (int)Host.GetSize(id, storageName);
			}
			catch (Exception ex)
			{
				//_logger?.Error($"Url:{url}. {ex}", ex);
				throw;
			}
		}

		public bool IsExists(string id, string name)
		{
			try
			{
				return Host.Exists(id, name);
			}
			catch (Exception ex)
			{
				//_logger?.Error($"Url:{url}. {ex}", ex);
				throw;
			}
		}

		public string GetExternalId(string id, string storageName)
		{
			try
			{
				return Host.GetExistIdByExternal(id, storageName);
			}
			catch (Exception ex)
			{
				//_logger?.Error($"Url:{url}. {ex}", ex);
				throw;
			}
			return null;
		}

		public string Save(string externalId, Stream file, string storageName)
		{

			try
			{

				var id = GetId(externalId);
				var bytes = new byte[file.Length];
				file.Read(bytes, 0, bytes.Length);
				Host.SaveBytes(id, bytes, storageName, Guid.NewGuid().ToString());
				return id;
			}
			catch (Exception ex)
			{
				//_logger?.Error($"Url:{url}. {ex}", ex);
				throw;
			}
		}
	}
}
