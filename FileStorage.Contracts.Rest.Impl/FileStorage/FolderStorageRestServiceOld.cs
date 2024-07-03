using Newtonsoft.Json;
using Service.Registry.Common.Entities;
using Service.Registry.Utils;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;

namespace FileStorage.Contracts.Rest.Impl.FileStorage
{


	/// <summary>
	/// Impl <see cref="IFolderStorageRestService"/>
	/// </summary>
	public sealed class FolderStorageRestServiceOld : RestClientResponseBase, IFolderStorageRestService
	{
		private readonly string _apiV1;

		/// <summary>
		/// Ctor
		/// </summary>
		/// <param name="settings"><see cref="RestClientResponseSettings"/></param>
		public FolderStorageRestServiceOld(RestClientResponseSettings settings) : base(settings)
		{
			if (Settings.Host.EndsWith("/"))
				_apiV1 = $"{Settings.Host}api/v1/folderStorage/";
			else
				_apiV1 = $"{Settings.Host}/api/v1/folderStorage/";
		}

		/// <summary>
		/// Ctor
		/// </summary>
		/// <param name="settings"><see cref="RestClientResponseSettings"/></param>
		/// <param name="client"><see cref="IHttpClientFactory"/></param>
		public FolderStorageRestServiceOld(RestClientResponseSettings settings, IHttpClientFactory client) : base(settings, client)
		{
			if (Settings.Host.EndsWith("/"))
				_apiV1 = $"{Settings.Host}api/v1/folderStorage/";
			else
				_apiV1 = $"{Settings.Host}/api/v1/folderStorage/";
		}

		#region V1

		public string GetIdByExternal(string externalId)
		{
			var client = ClientFactory.CreateClient();
			client.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/json");
			var responseJson = RunSyncUtil.RunSync(() =>
			{
				var requestUri = $"{_apiV1}external?id={externalId}";
				return client.GetStringAsync(requestUri);
			});

			var response = JsonConvert.DeserializeAnonymousType(responseJson, new { InternalId = default(string) });

			return response.InternalId;
		}

		public long GetCount(string storageName)
		{
			var client = ClientFactory.CreateClient();
			client.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/json");
			var responseJson = RunSyncUtil.RunSync(() => client.GetStringAsync($"{_apiV1}count"));
			var response = JsonConvert.DeserializeAnonymousType(responseJson, new { Count = default(long) });

			return response.Count;

		}

		#endregion

		#region V2

		/// <summary>
		/// <see cref="IFolderStorageRestService.Write(string,string,string,byte[],string)"/>
		/// </summary>
		/// <param name="externalFolderId"></param>
		/// <param name="externalFileId"></param>
		/// <param name="bytes"></param>
		/// <param name="storageName"></param>
		/// <param name="sessionId"></param>
		public void Write(string externalFolderId, string externalFileId, string storageName, byte[] bytes, string sessionId)
		{
			Write(externalFolderId, externalFileId, storageName, bytes, true, sessionId);
		}

		/// <summary>
		/// <see cref="IFolderStorageRestService.Write(string,string,string,System.IO.Stream,string)"/>
		/// </summary>
		/// <param name="externalFolderId"></param>
		/// <param name="externalFileId"></param>
		/// <param name="storageName"></param>
		/// <param name="bytes"></param>
		/// <param name="close"></param>
		/// <param name="sessionId"></param>
		private void Write(string externalFolderId, string externalFileId, string storageName, byte[] bytes, bool close, string sessionId)
		{
			var folderId = externalFolderId.GetSha1Hash();
			var fileId = externalFileId.GetSha1Hash();
			var client = ClientFactory.CreateClient();
			client.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/json");
			var url = $"{_apiV1}savebytes?name={storageName}";
			var request = new SaveBytesRequest
			{
				Id = fileId,
				Bytes = bytes,
				Close = close,
				SessionId = sessionId,
				FolderId = folderId

			};
			var json = new StringContent(JsonConvert.SerializeObject(request, Formatting.Indented), Encoding.UTF8,
				"application/json");
			client.PostAsync(url, json).Wait();
		}

		public string GetExistIdByExternal(string externalFolderId, string externalFileId, string storageName)
		{
			var client = ClientFactory.CreateClient();
			client.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/json");
			var address = $"{_apiV1}exists/External?folderId={externalFolderId}&fileId={externalFileId}";
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
		}

		public long GetSize(string externalFolderId, string externalFileId, string storageName)
		{
			var folderId = externalFolderId.GetSha1Hash();
			var fileId = externalFileId.GetSha1Hash();
			var client = ClientFactory.CreateClient();
			client.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/json");
			var address = $"{_apiV1}size?folderId={folderId}&fileId={fileId}";
			var responseJson = RunSyncUtil.RunSync(() =>
				client.GetStringAsync(string.IsNullOrEmpty(storageName) ? address : $"{address}&name={storageName}"));
			var response = JsonConvert.DeserializeAnonymousType(responseJson, new { Size = default(long) });

			return response.Size;

		}

		public bool IsExists(string externalFolderId, string externalFileId, string storageName)
		{
			var folderId = externalFolderId.GetSha1Hash();
			var fileId = externalFileId.GetSha1Hash();
			var client = ClientFactory.CreateClient();
			client.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/json");
			var address = $"{_apiV1}exists?folderId={folderId}&fileId={fileId}";
			var responseJson = RunSyncUtil.RunSync(() =>
				client.GetStringAsync(string.IsNullOrEmpty(storageName) ? address : $"{address}&name={storageName}"));
			var response = JsonConvert.DeserializeAnonymousType(responseJson, new { Exists = default(bool) });

			return response.Exists;

		}
		/// <summary>
		/// 
		/// </summary>
		/// <param name="externalFolderId"></param>
		/// <param name="externalFileId"></param>
		/// <param name="storageName"></param>
		/// <returns></returns>
		public bool IsExistsByExternalId(string externalFolderId, string externalFileId, string storageName)
		{
			var client = ClientFactory.CreateClient();
			client.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/json");
			var address = $"{_apiV1}exists/External?folderId={externalFolderId}&fileId={externalFileId}";
			var responseJson = RunSyncUtil.RunSync(() =>
				client.GetStringAsync(string.IsNullOrEmpty(storageName) ? address : $"{address}&name={storageName}"));
			var response = JsonConvert.DeserializeAnonymousType(responseJson, new
			{
				Exists = default(bool),
				Id = default(string)
			});

			return response.Exists;

		}

		public void Write(string externalFolderId, string externalFileId, string storageName, Stream stream, string sessionId)
		{
			var len = stream.Length;
			if (len == 0)
				return;
			var length = 64000;

			var byteCount = 0;
			var buffer = new byte[length];
			var close = false;
			while (!close && (byteCount = stream.Read(buffer, 0, buffer.Length)) > 0)
			{
				close = length > byteCount;
				Write(externalFolderId, externalFileId, storageName, buffer.Take(byteCount).ToArray(), close, sessionId);
			}
		}

		public void Read(string externalFolderId, string externalFileId, string storageName, Stream stream)
		{
			var folderId = externalFolderId.GetSha1Hash();
			var fileId = externalFileId.GetSha1Hash();
			const int constSize = 100000;
			long offset = 0;
			long size = constSize;
			var totalLen = GetSize(folderId, fileId, storageName);
			if (totalLen == 0)
				return;
			byte[] bytes;
			do
			{
				if (offset + size > totalLen)
				{
					size = totalLen - offset;
				}

				bytes = Read(folderId, fileId, offset, size, storageName);
				if (bytes == null)
					return;
				stream.Write(bytes, 0, bytes.Length);
				offset += bytes.Length;
			} while (offset < totalLen);
		}
		public byte[] Read(string externalFolderId, string externalFileId, long offset, long size, string storageName)
		{
			if (size == 0)
				return null;

			var folderId = externalFolderId.GetSha1Hash();
			var fileId = externalFileId.GetSha1Hash();
			var client = ClientFactory.CreateClient();
			client.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/json");
			var url = $"{_apiV1}getbytes?folderId={folderId}&fileId={fileId}&offset={offset}&count={size}&name={storageName}";
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
		public bool Delete(string externalFolderId, string externalFileId, string storageName)
		{
			var folderId = externalFolderId.GetSha1Hash();
			var fileId = externalFileId.GetSha1Hash();
			var client = ClientFactory.CreateClient();
			client.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/json");
			var address = $"{_apiV1}delete?folderId={folderId}&fileId={fileId}";
			var responseJson = RunSyncUtil.RunSync(() =>
				client.GetStringAsync(string.IsNullOrEmpty(storageName) ? address : $"{address}&name={storageName}"));
			var response = JsonConvert.DeserializeAnonymousType(responseJson, new
			{
				Exists = default(bool),
				Id = default(string)
			});

			return response?.Exists ?? false;
		}

		/// <summary>
		/// 50 Мб
		/// </summary>
		private static int _maxLen = 1024 * 1024 * 20;
		public Stream GetStream(string externalFolderId, string externalFileId, string name)
		{
			if (IsExists(externalFolderId, externalFileId, name))
			{

				var size = GetSize(externalFolderId, externalFileId, name);

				var result = new MixedMemoryStream();

				var len = size / 10;
				if (len > 1024 * 1024 * 20)
					len = _maxLen;
				var buffer = new byte[len];
				var offset = 0l;

				while (offset < size)
				{
					buffer = Read(externalFolderId, externalFileId, offset, buffer.Length, name);
					if (buffer == null)
						break;
					offset += buffer.Length;
					result.Write(buffer, 0, buffer.Length);
				}

				result.Seek(0, SeekOrigin.Begin);
				//Save to cache
				//var bytes = new byte[result.Length];
				///result.Read(bytes, 0, bytes.Length);
				//_cache[internalId] = bytes;

				result.Seek(0, SeekOrigin.Begin);
				return result;
			}
			return null;
		}

		public void Move(string externalFolderId, string toExternalFolderId, string externalFileId, string storageName)
		{
			var toFolderId = toExternalFolderId.GetSha1Hash();
			var fromFolderId = externalFolderId.GetSha1Hash();
			var fileId = externalFileId.GetSha1Hash();
			var client = ClientFactory.CreateClient();
			client.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/json");
			var address = $"{_apiV1}move?toFolderId={toFolderId}&folderId={fromFolderId}&fileId={fileId}";
			var responseJson = RunSyncUtil.RunSync(() =>
				client.GetStringAsync(string.IsNullOrEmpty(storageName) ? address : $"{address}&name={storageName}"));

		}

		#endregion
	}
}