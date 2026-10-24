using Newtonsoft.Json;
using Service.Registry.Common.Entities;
using Service.Registry.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;

namespace FileStorage.Contracts.Rest.Impl.FileStorage
{


	/// <summary>
	/// Impl <see cref="IFolderStorageRestService"/>
	/// </summary>
	public sealed class FolderStorageRestService : RestClientResponseBase, IFolderStorageRestService
	{
		private readonly string _apiV1;

		/// <summary>
		/// Ctor
		/// </summary>
		/// <param name="settings"><see cref="RestClientResponseSettings"/></param>
		public FolderStorageRestService(RestClientResponseSettings settings) : base(settings)
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
		public FolderStorageRestService(RestClientResponseSettings settings, IHttpClientFactory client) : base(settings, client)
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
			var responseJson = RestHelper.GetString(client, $"{_apiV1}external?id={RestHelper.Encode(externalId)}");
			var response = JsonConvert.DeserializeAnonymousType(responseJson, new { InternalId = default(string) });
			return response?.InternalId;
		}

		public long GetCount(string storageName)
		{
			var client = ClientFactory.CreateClient();
			// раньше имя хранилища не передавалось, и возвращалось число файлов хранилища по умолчанию
			var responseJson = RestHelper.GetString(client, RestHelper.WithName($"{_apiV1}count", storageName));
			var response = JsonConvert.DeserializeAnonymousType(responseJson, new { Count = default(long) });
			return response?.Count ?? 0;
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
			var client = ClientFactory.CreateClient();
			var request = new SaveBytesRequest
			{
				Id = externalFileId.GetSha1Hash(),
				Bytes = bytes,
				Close = close,
				SessionId = sessionId,
				FolderId = externalFolderId.GetSha1Hash()
			};
			// ответ сервера проверяется: раньше ошибка записи терялась молча
			RestHelper.PostJson(client, RestHelper.WithName($"{_apiV1}savebytes", storageName), request);
		}

		public string GetExistIdByExternal(string externalFolderId, string externalFileId, string storageName)
		{
			var client = ClientFactory.CreateClient();
			var address = RestHelper.WithName($"{_apiV1}exists/External?folderId={RestHelper.Encode(externalFolderId)}&fileId={RestHelper.Encode(externalFileId)}", storageName);
			var responseJson = RestHelper.GetString(client, address);
			var response = JsonConvert.DeserializeAnonymousType(responseJson, new
			{
				Exists = default(bool),
				Id = default(string)
			});

			return response != null && response.Exists
				? response.Id
				: null;
		}

		public long GetSize(string externalFolderId, string externalFileId, string storageName)
		{
			var client = ClientFactory.CreateClient();
			var address = RestHelper.WithName($"{_apiV1}size?folderId={externalFolderId.GetSha1Hash()}&fileId={externalFileId.GetSha1Hash()}", storageName);
			var responseJson = RestHelper.GetString(client, address);
			var response = JsonConvert.DeserializeAnonymousType(responseJson, new { Size = default(long) });
			return response?.Size ?? 0;
		}

		public bool IsExists(string externalFolderId, string externalFileId, string storageName)
		{
			var client = ClientFactory.CreateClient();
			var address = RestHelper.WithName($"{_apiV1}exists?folderId={externalFolderId.GetSha1Hash()}&fileId={externalFileId.GetSha1Hash()}", storageName);
			var responseJson = RestHelper.GetString(client, address);
			var response = JsonConvert.DeserializeAnonymousType(responseJson, new { Exists = default(bool) });
			return response?.Exists ?? false;
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
			return !string.IsNullOrEmpty(GetExistIdByExternal(externalFolderId, externalFileId, storageName));
		}

		public void Write(string externalFolderId, string externalFileId, string storageName, Stream stream, string sessionId)
		{
			// stream.Length есть только у потоков с перемоткой; пустой поток, как и раньше, не сохраняется
			if (stream.CanSeek && stream.Length - stream.Position == 0)
				return;
			// Блоки по 64 КБ, у последнего — признак закрытия.
			stream.WriteChunks(64 * 1024, (chunk, close) => Write(externalFolderId, externalFileId, storageName, chunk, close, sessionId));
		}

		public void Read(string externalFolderId, string externalFileId, string storageName, Stream stream)
		{
			// Раньше сюда передавались уже захешированные id, а GetSize и Read хешировали их ещё раз:
			// запрашивался несуществующий файл, и в поток ничего не записывалось.
			const int constSize = 100000;
			long offset = 0;
			var totalLen = GetSize(externalFolderId, externalFileId, storageName);
			while (offset < totalLen)
			{
				var size = Math.Min(constSize, totalLen - offset);
				var bytes = Read(externalFolderId, externalFileId, offset, size, storageName);
				if (bytes == null || bytes.Length == 0)
					return;
				stream.Write(bytes, 0, bytes.Length);
				offset += bytes.Length;
			}
		}
		public byte[] Read(string externalFolderId, string externalFileId, long offset, long size, string storageName)
		{
			if (size <= 0)
				return null;

			var client = ClientFactory.CreateClient();
			var url = RestHelper.WithName($"{_apiV1}getbytes?folderId={externalFolderId.GetSha1Hash()}&fileId={externalFileId.GetSha1Hash()}&offset={offset}&count={size}", storageName);
			var str = RestHelper.GetString(client, url);
			var data = JsonConvert.DeserializeObject<GetBytesResult>(str);
			return data?.Bytes;
		}
		public bool Delete(string externalFolderId, string externalFileId, string storageName)
		{
			// Сервер отвечает пустым 200 OK, поэтому разбор тела ответа всегда давал false.
			// Теперь true — если файл был и запрос на удаление выполнен успешно.
			if (!IsExists(externalFolderId, externalFileId, storageName))
				return false;
			var client = ClientFactory.CreateClient();
			var address = RestHelper.WithName($"{_apiV1}delete?folderId={externalFolderId.GetSha1Hash()}&fileId={externalFileId.GetSha1Hash()}", storageName);
			return RestHelper.GetOk(client, address);
		}

		public Stream GetStream(string externalFolderId, string externalFileId, string name)
		{
			if (IsExists(externalFolderId, externalFileId, name))
			{ 
				var size = GetSize(externalFolderId, externalFileId, name);

				var result = new InternalFolderStream(Read, size, externalFolderId, externalFileId, name);

				 
				return result;
			}
			return null;
		}

		public void Move(string externalFolderId, string toExternalFolderId, string externalFileId, string storageName)
		{
			var client = ClientFactory.CreateClient();
			var address = RestHelper.WithName($"{_apiV1}move?toFolderId={toExternalFolderId.GetSha1Hash()}&folderId={externalFolderId.GetSha1Hash()}&fileId={externalFileId.GetSha1Hash()}", storageName);
			// при ошибке сервера GetStringAsync бросает исключение
			RestHelper.GetString(client, address);
		}

		/// <inheritdoc />
		public List<string> GetStorageNames()
		{
			var client = ClientFactory.CreateClient();
			var responseJson = RestHelper.GetString(client, $"{_apiV1}list");
			return JsonConvert.DeserializeObject<List<string>>(responseJson) ?? new List<string>();
		}

		/// <inheritdoc />
		public Stream OpenRead(string externalFolderId, string externalFileId, string storageName)
		{
			var client = ClientFactory.CreateClient();
			var address = RestHelper.WithName($"{_apiV1}stream/external?folderId={RestHelper.Encode(externalFolderId)}&fileId={RestHelper.Encode(externalFileId)}", storageName);
			return RestHelper.OpenRead(client, address);
		}

		/// <inheritdoc />
		public string GetFullName(string externalFolderId, string externalFileId, string storageName)
		{
			var client = ClientFactory.CreateClient();
			var address = RestHelper.WithName($"{_apiV1}path?folderId={externalFolderId.GetSha1Hash()}&fileId={externalFileId.GetSha1Hash()}", storageName);
			return RestHelper.ParseString(RestHelper.GetString(client, address));
		}

		/// <inheritdoc />
		public void ClearTtl(int daysAgo, string storageName)
		{
			var client = ClientFactory.CreateClient();
			RestHelper.GetEnsureSuccess(client, RestHelper.WithName($"{_apiV1}clearTtl?daysAgo={daysAgo}", storageName));
		}

		/// <inheritdoc />
		public void ChangeDepth(int newDepth, string storageName)
		{
			var client = ClientFactory.CreateClient();
			RestHelper.GetEnsureSuccess(client, RestHelper.WithName($"{_apiV1}changeDepth?newDepth={newDepth}", storageName));
		}

		/// <inheritdoc />
		public string GetVersion()
		{
			var client = ClientFactory.CreateClient();
			var responseJson = RestHelper.GetString(client, $"{_apiV1}../version");
			var response = JsonConvert.DeserializeAnonymousType(responseJson, new { Version = default(string), Name = default(string) });
			return response?.Version;
		}

		#endregion
	}
}