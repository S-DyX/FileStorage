using Newtonsoft.Json;
using Service.Registry.Common.Entities;
using Service.Registry.Utils;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;

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
		/// <param name="externalFileId">this is file id from your system</param> 
		/// <param name="storageName"></param>
		/// <param name="stream"></param>
		/// <param name="sessionId"></param>
		void Write(string externalFileId, string storageName, Stream stream, string sessionId);

	

		/// <summary>
		/// Partial read 
		/// </summary> 
		/// <param name="externalFileId">this is file id from your system</param> 
		/// <param name="offset"></param>
		/// <param name="size"></param>
		/// <param name="storageName"></param>
		/// <returns></returns>
		byte[] Read(string externalFileId, long offset, long size, string storageName);

	}

	/// <summary>
	/// The interface for working with filestorage at REST
	/// </summary>
	public interface IFileStorageRestService
	{
		/// <summary>
		/// Method return read only stream
		/// </summary>
		/// <param name="externalFileId">this is file id from your system</param> 
		/// <param name="storageName">Storage name</param>
		/// <returns></returns>
		Stream GetStream(string externalFileId, string storageName);
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
		/// <param name="externalFileId">id from your system</param>
		/// <param name="storageName"></param>
		/// <returns></returns>
		bool IsExistsByExternalId(string externalFileId, string storageName);

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

		/// <summary>
		/// Method return read only stream
		/// </summary>
		/// <param name="externalFileId">this is file id from your system</param> 
		/// <param name="storageName">Storage name</param>
		/// <returns></returns>
		public Stream GetStream(string externalFileId, string storageName)
		{
			var fileId = GetIdByExternal(externalFileId);
			if (Exists(fileId, storageName))
			{
				var size = GetSize(fileId, storageName);

				var result = new InternalFileStream(GetBytes, size, fileId, storageName);


				return result;
			}
			return null;
		}
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
		/// /// </summary>
		/// <param name="fileId">internal FS id</param>
		/// <param name="bytes"></param>
		/// <param name="storageName"></param>
		/// <param name="sessionId"></param>
		public void SaveBytes(string fileId, byte[] bytes, string storageName, string sessionId)
		{
			Write(fileId, bytes, storageName, true, sessionId);
		}

		/// <summary>
		/// <see cref="IFileStorageRestService.Write(string, byte[], string, bool, string)"/>
		/// </summary>
		/// <param name="fileId">internal FS id</param>
		/// <param name="bytes"></param>
		/// <param name="storageName"></param>
		/// <param name="close"></param>
		/// <param name="sessionId"></param>
		private void Write(string fileId, byte[] bytes, string storageName, bool close, string sessionId)
		{
			var client = ClientFactory.CreateClient();
			client.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/json");
			var url = $"{_apiV1}savebytes?name={storageName}";
			var request = new SaveBytesRequest
			{
				Id = fileId,
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
		/// <summary>
		/// Return exist id in FS by externalId
		/// </summary>
		/// <param name="externalId">id from your system</param>
		/// <param name="storageName"></param>
		/// <returns></returns>
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
		/// <summary>
		/// Return file size
		/// </summary>
		/// <param name="fileId">internal FS id</param>
		/// <param name="storageName"></param>
		/// <returns></returns>
		public long GetSize(string fileId, string storageName)
		{
			var client = ClientFactory.CreateClient();
			client.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/json");
			var address = $"{_apiV1}size?Id={fileId}";
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
		/// <summary>
		/// Is exists on FS
		/// </summary>
		/// <param name="fileId">internal FS id</param>
		/// <param name="storageName"></param>
		/// <returns></returns>
		public bool Exists(string fileId, string storageName)
		{
			var client = ClientFactory.CreateClient();
			client.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/json");
			var address = $"{_apiV1}exists?id={fileId}";
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
		/// <summary>
		/// Is exists by externalId
		/// </summary>
		/// <param name="externalId">id from your system</param>
		/// <param name="storageName"></param>
		/// <returns></returns>
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
		/// <param name="fileId">FS internal id </param>
		/// <param name="storageName"></param>
		/// <param name="stream"></param>
		/// <param name="sessionId"></param>
		public void SaveBytes(string fileId, string storageName, Stream stream, string sessionId)
		{
			var length = 11000;

			var byteCount = 0;
			var buffer = new byte[length];
			var len = stream.Length;
			var close = false;
			while (!close && (byteCount = stream.Read(buffer, 0, buffer.Length)) > 0)
			{
				close = length >= byteCount;
				Write(fileId, buffer.Take(byteCount).ToArray(), storageName, close, sessionId);
			}
		}
		/// <summary>
		/// Load data from FS to Stream
		/// </summary>
		/// <param name="fileId">internal FS id</param>
		/// <param name="storageName"></param>
		/// <param name="stream"></param>
		public void WriteToStream(string fileId, string storageName, Stream stream)
		{
			const int constSize = 100000;
			long offset = 0;
			long size = constSize;
			var totalLen = GetSize(fileId, storageName);
			if (totalLen == 0)
				return;
			byte[] bytes;
			do
			{
				if (offset + size > totalLen)
				{
					size = totalLen - offset;
				}

				bytes = GetBytes(fileId, offset, size, storageName);
				if (bytes == null)
					return;
				stream.Write(bytes, 0, bytes.Length);
				offset += bytes.Length;
			} while (offset < totalLen);
		}

		/// <summary>
		/// Partial read 
		/// </summary>
		/// <param name="fileId">internal FS id</param>
		/// <param name="offset"></param>
		/// <param name="size"></param>
		/// <param name="storageName"></param>
		/// <returns></returns>
		public byte[] GetBytes(string fileId, long offset, long size, string storageName)
		{
			if (size == 0)
				return null;

			var client = ClientFactory.CreateClient();
			client.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/json");
			var url = $"{_apiV1}getbytes?id={fileId}&offset={offset}&count={size}&name={storageName}";
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
		/// <summary>
		/// Delete file from FS
		/// </summary>
		/// <param name="fileId">internal FS id</param>
		/// <param name="storageName"></param>
		/// <returns></returns>
		public bool Delete(string fileId, string storageName)
		{
			var client = ClientFactory.CreateClient();
			client.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/json");
			var address = $"{_apiV1}delete?Id={fileId}";
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