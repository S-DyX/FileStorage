using Newtonsoft.Json;
using Service.Registry.Common;
using Service.Registry.Utils;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using Service.Registry.Common.Entities;

namespace FileStorage.Contracts.Rest.Impl.FileStorage
{
	public interface ITcpFileStorageService : IFileStorageRestService
	{
		/// <summary>
		/// Is exists on FS
		/// </summary> 
		/// <param name="externalFileId">this is file id from your system</param> 
		/// <param name="storageName"></param>
		/// <returns></returns>
		bool IsExists(string externalFileId, string storageName);

		/// <summary>
		/// Save bytes to FS 
		/// </summary> 
		/// <param name="externalFileId">this is file id from your system</param> 
		/// <param name="storageName"></param>
		/// <param name="bytes"></param>
		/// <param name="sessionId"></param>
		void Write(string externalFileId, string storageName, byte[] bytes, string sessionId);

		/// <summary>
		/// Save stream to FS
		/// </summary>
		/// <param name="externalFolderId">this is folder id from your system</param>
		/// <param name="externalFileId">this is file id from your system</param> 
		/// <param name="storageName"></param>
		/// <param name="stream"></param>
		/// <param name="sessionId"></param>
		void Write(string externalFileId, string storageName, Stream stream, string sessionId);

		/// <summary>
		/// Load data from FS to Stream
		/// </summary>
		/// <param name="externalFileId">this is file id from your system</param> 
		/// <param name="storageName"></param>
		/// <param name="stream"></param>
		void Read(string externalFileId, string storageName, Stream stream);



		/// <summary>
		/// Partial read 
		/// </summary> 
		/// <param name="externalFileId">this is file id from your system</param> 
		/// <param name="offset"></param>
		/// <param name="size"></param>
		/// <param name="storageName"></param>
		/// <returns></returns>
		byte[] Read(string externalFileId, long offset, long size, string storageName);

		/// <summary>
		/// Return memory stream
		/// </summary> 
		/// <param name="externalFileId">this is file id from your system</param> 
		/// <param name="name"></param>
		/// <returns></returns>
		Stream GetStream(string externalFileId, string name);
	}

	/// <summary>
	/// The interface for working with filestorage at REST
	/// </summary>
	public interface IFileStorageRestService
	{
		/// <summary>
		/// Return internal id from FS by externalId
		/// </summary>
		/// <param name="externalId">id from your system</param>
		/// <returns></returns>
		string GetIdByExternal(string externalId);

		/// <summary>
		/// Return count of documents in FS
		/// </summary>
		/// <param name="storageName"></param>
		/// <returns></returns>
		long GetCount(string storageName);

		/// <summary>
		/// Save bytes to FS 
		/// </summary>
		/// <param name="fileId">internal FS id</param>
		/// <param name="bytes"></param>
		/// <param name="storageName"></param>
		/// <param name="sessionId"></param>
		void SaveBytes(string fileId, byte[] bytes, string storageName, string sessionId);

		/// <summary>
		/// Save stream to FS
		/// </summary>
		/// <param name="fileId">internal FS id</param>
		/// <param name="storageName"></param>
		/// <param name="stream"></param>
		/// <param name="sessionId"></param>
		void SaveBytes(string fileId, string storageName, Stream stream, string sessionId);

		/// <summary>
		/// Load data from FS to Stream
		/// </summary>
		/// <param name="fileId">internal FS id</param>
		/// <param name="storageName"></param>
		/// <param name="stream"></param>
		void WriteToStream(string fileId, string storageName, Stream stream);

		/// <summary>
		/// Return file size
		/// </summary>
		/// <param name="fileId">internal FS id</param>
		/// <param name="storageName"></param>
		/// <returns></returns>
		long GetSize(string fileId, string storageName);

		/// <summary>
		/// Is exists on FS
		/// </summary>
		/// <param name="fileId">internal FS id</param>
		/// <param name="storageName"></param>
		/// <returns></returns>
		bool Exists(string fileId, string storageName);

		/// <summary>
		/// Return exist id in FS by externalId
		/// </summary>
		/// <param name="externalId">id from your system</param>
		/// <param name="storageName"></param>
		/// <returns></returns>
		string GetExistIdByExternal(string externalId, string storageName);

		/// <summary>
		/// Is exists by externalId
		/// </summary>
		/// <param name="externalId">id from your system</param>
		/// <param name="storageName"></param>
		/// <returns></returns>
		bool IsExistsByExternalId(string externalId, string storageName);

		/// <summary>
		/// Partial read 
		/// </summary>
		/// <param name="fileId">internal FS id</param>
		/// <param name="offset"></param>
		/// <param name="size"></param>
		/// <param name="storageName"></param>
		/// <returns></returns>
		byte[] GetBytes(string fileId, long offset, long size, string storageName);

		/// <summary>
		/// Delete file from FS
		/// </summary>
		/// <param name="fileId">internal FS id</param>
		/// <param name="storageName"></param>
		/// <returns></returns>
		bool Delete(string fileId, string storageName);
	}

	/// <summary>
	/// Impl <see cref="IFileStorageRestService"/>
	/// </summary>
	public sealed class FileStorageRestService : RestClientResponseBase, IFileStorageRestService
	{
		private readonly string _apiV1;

		/// <summary>
		/// Ctor
		/// </summary>
		/// <param name="settings"><see cref="RestClientResponseSettings"/></param>
		public FileStorageRestService(RestClientResponseSettings settings) : base(settings)
		{
			if (Settings.Host.EndsWith("/"))
				_apiV1 = $"{Settings.Host}api/v1/filestorage/";
			else
				_apiV1 = $"{Settings.Host}/api/v1/filestorage/";
		}

		/// <summary>
		/// Ctor
		/// </summary>
		/// <param name="settings"><see cref="RestClientResponseSettings"/></param>
		/// <param name="client"><see cref="IHttpClientFactory"/></param>
		public FileStorageRestService(RestClientResponseSettings settings, IHttpClientFactory client) : base(settings, client)
		{
			if (Settings.Host.EndsWith("/"))
				_apiV1 = $"{Settings.Host}api/v1/filestorage/";
			else
				_apiV1 = $"{Settings.Host}/api/v1/filestorage/";
		}

		#region V1

		public string GetIdByExternal(string externalId)
		{
			var client = ClientFactory.CreateClient();
			client.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/json");
			var responseJson = RunSyncUtil.RunSync(() => client.GetStringAsync($"{_apiV1}external?id={externalId}"));

			var response = JsonConvert.DeserializeAnonymousType(responseJson, new { InternalId = default(string) });

			return response.InternalId;

			//using (var client = new WebClient())
			//{
			//	client.Encoding = Encoding.UTF8;

			//	var responseJson = client.DownloadString($"{_apiV1}external?id={externalId}");
			//	var response = JsonConvert.DeserializeAnonymousType(responseJson, new { InternalId = default(string) });

			//	return response.InternalId;
			//}
		}

		public long GetCount(string storageName)
		{
			var client = ClientFactory.CreateClient();
			client.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/json");
			var responseJson = RunSyncUtil.RunSync(() => client.GetStringAsync($"{_apiV1}count"));
			var response = JsonConvert.DeserializeAnonymousType(responseJson, new { Count = default(long) });

			return response.Count;

			//using (var client = new WebClient())
			//{
			//	client.Encoding = Encoding.UTF8;

			//	var responseJson = client.DownloadString($"{_apiV1}count");
			//	var response = JsonConvert.DeserializeAnonymousType(responseJson, new { Count = default(long) });

			//	return response.Count;
			//}
		}

		#endregion

		#region V2

		/// <summary>
		/// <see cref="IFileStorageRestService.SaveBytes(string, byte[], string, string)"/>
		/// </summary>
		/// <param name="id"></param>
		/// <param name="bytes"></param>
		/// <param name="storageName"></param>
		/// <param name="sessionId"></param>
		public void SaveBytes(string id, byte[] bytes, string storageName, string sessionId)
		{
			Write(id, bytes, storageName, true, sessionId);
		}

		/// <summary>
		/// <see cref="IFileStorageRestService.Write(string, byte[], string, bool, string)"/>
		/// </summary>
		/// <param name="id"></param>
		/// <param name="bytes"></param>
		/// <param name="storageName"></param>
		/// <param name="close"></param>
		/// <param name="sessionId"></param>
		private void Write(string id, byte[] bytes, string storageName, bool close, string sessionId)
		{
			var client = ClientFactory.CreateClient();
			client.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/json");
			var url = $"{_apiV1}savebytes?name={storageName}";
			var request = new SaveBytesRequest
			{
				Id = id,
				Bytes = bytes,
				Close = close,
				SessionId = sessionId
			};
			var json = new StringContent(JsonConvert.SerializeObject(request, Formatting.Indented), Encoding.UTF8,
				"application/json");
			//var obj = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(request, Formatting.Indented));
			RunSyncUtil.RunSync(() => client.PostAsync(url, json));



			//using (var client = new WebClient())
			//{
			//	client.Headers.Add("Content-Type", " application/problem+json");
			//	client.Encoding = Encoding.UTF8;


			//	var url = $"{_apiV1}savebytes?name={storageName}";
			//	var request = new SaveBytesRequest
			//	{
			//		Id = id,
			//		Bytes = bytes,
			//		Close = close
			//	};
			//	var obj = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(request, Formatting.Indented));
			//	client.UploadData(url, "POST", obj);
			//}
		}

		public string GetExistIdByExternal(string externalId, string storageName)
		{
			var client = ClientFactory.CreateClient();
			client.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/json");
			var address = $"{_apiV1}exists/External?id={externalId}";
			var responseJson = RunSyncUtil.RunSync(() =>
				client.GetStringAsync(string.IsNullOrEmpty(storageName) ? address : $"{address}&name={storageName}"));
			var response = JsonConvert.DeserializeAnonymousType(responseJson, new
			{
				Exists = default(bool),
				Id = default(string)
			});

			return response.Exists
				? response.Id
				: null;


			//using (var client = new WebClient())
			//{
			//	client.Headers.Add("Content-Type", " application/problem+json");
			//	client.Encoding = Encoding.UTF8;

			//	var address = $"{_apiV1}exists/External?id={externalId}";
			//	var responseJson = client.DownloadString(string.IsNullOrEmpty(storageName) ? address : $"{address}&name={storageName}");
			//	var response = JsonConvert.DeserializeAnonymousType(responseJson, new
			//	{
			//		Exists = default(bool),
			//		Id = default(string)
			//	});

			//	return response.Exists
			//		? response.Id
			//		: null;
			//}
		}

		public long GetSize(string id, string storageName)
		{
			var client = ClientFactory.CreateClient();
			client.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/json");
			var address = $"{_apiV1}size?Id={id}";
			var responseJson = RunSyncUtil.RunSync(() =>
				client.GetStringAsync(string.IsNullOrEmpty(storageName) ? address : $"{address}&name={storageName}"));
			var response = JsonConvert.DeserializeAnonymousType(responseJson, new { Size = default(long) });

			return response.Size;

			//using (var client = new WebClient())
			//{
			//	client.Headers.Add("Content-Type", " application/problem+json");
			//	client.Encoding = Encoding.UTF8;

			//	var address = $"{_apiV1}size?Id={id}";
			//	var responseJson = client.DownloadString(string.IsNullOrEmpty(storageName) ? address : $"{address}&name={storageName}");
			//	var response = JsonConvert.DeserializeAnonymousType(responseJson, new { Size = default(long) });

			//	return response.Size;
			//}
		}

		public bool Exists(string hash, string storageName)
		{
			var client = ClientFactory.CreateClient();
			client.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/json");
			var address = $"{_apiV1}exists?id={hash}";
			var responseJson = RunSyncUtil.RunSync(() =>
				client.GetStringAsync(string.IsNullOrEmpty(storageName) ? address : $"{address}&name={storageName}"));
			var response = JsonConvert.DeserializeAnonymousType(responseJson, new { Exists = default(bool) });

			return response.Exists;

			//using (var client = new WebClient())
			//{
			//	client.Encoding = Encoding.UTF8;

			//	var address = $"{_apiV1}exists?id={hash}";
			//	var responseJson = client.DownloadString(string.IsNullOrEmpty(storageName) ? address : $"{address}&name={storageName}");
			//	var response = JsonConvert.DeserializeAnonymousType(responseJson, new { Exists = default(bool) });

			//	return response.Exists;
		}

		public bool IsExistsByExternalId(string externalId, string storageName)
		{
			var client = ClientFactory.CreateClient();
			client.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/json");
			var address = $"{_apiV1}exists/External?Id={externalId}&name={storageName}";
			var responseJson = RunSyncUtil.RunSync(() =>
				client.GetStringAsync(string.IsNullOrEmpty(storageName) ? address : $"{address}&name={storageName}"));
			var response = JsonConvert.DeserializeAnonymousType(responseJson, new
			{
				Exists = default(bool),
				Id = default(string)
			});

			return response.Exists;

			//using (var client = new WebClient())
			//{
			//	client.Encoding = Encoding.UTF8;

			//	var address = $"{_apiV1}exists/External?Id={externalId}";
			//	var responseJson = client.DownloadString(string.IsNullOrEmpty(storageName) ? address : $"{address}&name={storageName}");
			//	var response = JsonConvert.DeserializeAnonymousType(responseJson, new
			//	{
			//		Exists = default(bool),
			//		Id = default(string)
			//	});

			//	return response.Exists;
			//}
		}

		/// <summary>
		/// <see cref="IFileStorageRestService.SaveBytes(string, string , Stream , string)"/>
		/// </summary>
		/// <param name="id">FS internal id </param>
		/// <param name="storageName"></param>
		/// <param name="stream"></param>
		/// <param name="sessionId"></param>
		public void SaveBytes(string id, string storageName, Stream stream, string sessionId)
		{
			var length = 11000;

			var byteCount = 0;
			var buffer = new byte[length];
			var len = stream.Length;
			var close = false;
			while (!close && (byteCount = stream.Read(buffer, 0, buffer.Length)) > 0)
			{
				close = length > byteCount;
				Write(id, buffer.Take(byteCount).ToArray(), storageName, close, sessionId);
			}
		}

		public void WriteToStream(string id, string storageName, Stream stream)
		{
			const int constSize = 100000;
			long offset = 0;
			long size = constSize;
			var totalLen = GetSize(id, storageName);
			if (totalLen == 0)
				return;
			byte[] bytes;
			do
			{
				if (offset + size > totalLen)
				{
					size = totalLen - offset;
				}

				bytes = GetBytes(id, offset, size, storageName);
				if (bytes == null)
					return;
				stream.Write(bytes, 0, bytes.Length);
				offset += bytes.Length;
			} while (offset < totalLen);
		}
		public byte[] GetBytes(string id, long offset, long size, string storageName)
		{
			if (size == 0)
				return null;

			var client = ClientFactory.CreateClient();
			client.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/json");
			var url = $"{_apiV1}getbytes?id={id}&offset={offset}&count={size}&name={storageName}";
			var str = RunSyncUtil.RunSync(() => client.GetStringAsync(url));
			var data = JsonConvert.DeserializeObject<GetBytesResult>(str);
			return data.Bytes;


			//var url = $"{_apiV1}getbytes?id={id}&offset={offset}&count={size}&name={storageName}";
			//using (var client = new WebClient())
			//{
			//	client.Encoding = Encoding.UTF8;
			//	var str = client.DownloadString(url);
			//	var data = JsonConvert.DeserializeObject<GetBytesResult>(str);
			//	return data.Bytes;
			//}

		}
		public bool Delete(string id, string storageName)
		{
			var client = ClientFactory.CreateClient();
			client.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/json");
			var address = $"{_apiV1}delete?Id={id}";
			var responseJson = RunSyncUtil.RunSync(() =>
				client.GetStringAsync(string.IsNullOrEmpty(storageName) ? address : $"{address}&name={storageName}"));
			var response = JsonConvert.DeserializeAnonymousType(responseJson, new
			{
				Exists = default(bool),
				Id = default(string)
			});

			return response?.Exists ?? false;

			//using (var client = new WebClient())
			//{
			//	client.Encoding = Encoding.UTF8;

			//	var address = $"{_apiV1}delete?Id={id}";
			//	var responseJson = client.DownloadString(string.IsNullOrEmpty(storageName) ? address : $"{address}&name={storageName}");
			//	var response = JsonConvert.DeserializeAnonymousType(responseJson, new
			//	{
			//		Exists = default(bool),
			//		Id = default(string)
			//	});

			//	return response?.Exists ?? false;
			//}
		}

		#endregion
	}
}