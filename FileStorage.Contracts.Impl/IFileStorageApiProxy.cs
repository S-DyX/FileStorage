using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using FileStorage.Core.Interfaces;
using Newtonsoft.Json;
using Service.Registry.Common;

namespace FileStorage.Contracts.Impl
{
	public interface IFileStorageApiProxy
	{
		byte[] GetBytes(string id, long offset, int size);
		bool IsExists(string id);
	}

	public class FileStorageApiProxy : IFileStorageApiProxy
	{
		private readonly ILocalLogger _logger;
		private readonly IServiceRegistryFactory _serviceRegistryFactory;
		private string _host;

		private string Host
		{
			get
			{
				if (_host == null)
				{
					var value = _serviceRegistryFactory.GetCommonClient("IFileStorageApi");
					var element = XElement.Parse(value);
					var hostAtt = element.Attribute("host");
					_host = hostAtt.Value;
				}

				return _host;
			}
		}

		public FileStorageApiProxy(ILocalLogger logger, IServiceRegistryFactory serviceRegistryFactory)
		{
			_logger = logger;
			_serviceRegistryFactory = serviceRegistryFactory;
		}

		public byte[] GetBytes(string id, long offset, int size)
		{
			var url = $"{Host}/api/v2/filestorage/getbytes?id={id}&offset={offset}&count={size}";
			try
			{
				var str = WebHelper.GetContent(url, Encoding.UTF8);
				var data = JsonConvert.DeserializeObject<dynamic>(str);
				return data.Bytes;
			}
			catch (Exception ex)
			{
				_logger?.Error($"Url:{url}. {ex}", ex);
				throw;
			}
		}

		public bool IsExists(string id)
		{
			var url = $"{Host}/api/v2/filestorage/exists/{id}";
			try
			{
				var str = WebHelper.GetContent(url, Encoding.UTF8);
				var data = JsonConvert.DeserializeObject<dynamic>(str);
				return data.Exists;
			}
			catch (Exception ex)
			{
				_logger?.Error($"Url:{url}. {ex}", ex);
				throw;
			}
		}
	}
}