using Newtonsoft.Json;
using Service.Registry.Common.Entities;
using Service.Registry.Utils;
using System.Collections.Generic;
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

		/// <summary>
		/// Delete file from FS by id from your system
		/// </summary>
		/// <param name="externalFileId">this is file id from your system</param>
		/// <param name="storageName"></param>
		/// <returns>true, if the file existed and was deleted</returns>
		bool DeleteByExternalId(string externalFileId, string storageName);
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
			var request = new SaveBytesRequest
			{
				Id = fileId,
				Bytes = bytes,
				Close = close,
				SessionId = sessionId
			};
			// ответ сервера проверяется: раньше ошибка записи терялась молча
			RestHelper.PostJson(client, RestHelper.WithName($"{_apiV1}savebytes", storageName), request);
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
			var address = RestHelper.WithName($"{_apiV1}exists/External?id={RestHelper.Encode(externalId)}", storageName);
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
		/// <summary>
		/// Return file size
		/// </summary>
		/// <param name="fileId">internal FS id</param>
		/// <param name="storageName"></param>
		/// <returns></returns>
		public long GetSize(string fileId, string storageName)
		{
			var client = ClientFactory.CreateClient();
			var address = RestHelper.WithName($"{_apiV1}size?id={RestHelper.Encode(fileId)}", storageName);
			var responseJson = RestHelper.GetString(client, address);
			var response = JsonConvert.DeserializeAnonymousType(responseJson, new { Size = default(long) });
			return response?.Size ?? 0;
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
			var address = RestHelper.WithName($"{_apiV1}exists?id={RestHelper.Encode(fileId)}", storageName);
			var responseJson = RestHelper.GetString(client, address);
			var response = JsonConvert.DeserializeAnonymousType(responseJson, new { Exists = default(bool) });
			return response?.Exists ?? false;
		}
		/// <summary>
		/// Is exists by externalId
		/// </summary>
		/// <param name="externalId">id from your system</param>
		/// <param name="storageName"></param>
		/// <returns></returns>
		public bool IsExistsByExternalId(string externalId, string storageName)
		{
			// раньше параметр name попадал в запрос дважды, и сервер получал имя хранилища вида "name,name"
			return !string.IsNullOrEmpty(GetExistIdByExternal(externalId, storageName));
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
			// Блоки по 64 КБ, у последнего — признак закрытия. Раньше при размере потока, кратном блоку,
			// файл не закрывался, а каждый блок копировался через Take().ToArray().
			stream.WriteChunks(64 * 1024, (chunk, close) => Write(fileId, chunk, storageName, close, sessionId));
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
			if (size <= 0)
				return null;

			var client = ClientFactory.CreateClient();
			var url = RestHelper.WithName($"{_apiV1}getbytes?id={RestHelper.Encode(fileId)}&offset={offset}&count={size}", storageName);
			var str = RestHelper.GetString(client, url);
			var data = JsonConvert.DeserializeObject<GetBytesResult>(str);
			return data?.Bytes;
		}
		/// <summary>
		/// Delete file from FS
		/// </summary>
		/// <param name="fileId">internal FS id</param>
		/// <param name="storageName"></param>
		/// <returns></returns>
		public bool Delete(string fileId, string storageName)
		{
			// Сервер отвечает пустым 200 OK, поэтому разбор тела ответа всегда давал false.
			// Теперь true — если файл был и запрос на удаление выполнен успешно.
			if (!Exists(fileId, storageName))
				return false;
			var client = ClientFactory.CreateClient();
			return RestHelper.GetOk(client, RestHelper.WithName($"{_apiV1}delete?id={RestHelper.Encode(fileId)}", storageName));
		}

		/// <summary>
		/// <see cref="IFileStorageRestService.DeleteByExternalId(string, string)"/>
		/// </summary>
		public bool DeleteByExternalId(string externalFileId, string storageName)
		{
			if (!IsExistsByExternalId(externalFileId, storageName))
				return false;
			var client = ClientFactory.CreateClient();
			return RestHelper.GetOk(client, RestHelper.WithName($"{_apiV1}delete/external?id={RestHelper.Encode(externalFileId)}", storageName));
		}

		/// <summary>
		/// Names of the storages created on the server
		/// </summary>
		public List<string> GetStorageNames()
		{
			var client = ClientFactory.CreateClient();
			var responseJson = RestHelper.GetString(client, $"{_apiV1}list");
			return JsonConvert.DeserializeObject<List<string>>(responseJson) ?? new List<string>();
		}

		/// <summary>
		/// Opens the file for sequential reading directly from the HTTP response, without base64 and chunks
		/// (unlike <see cref="GetStream"/>, the stream is not seekable). Returns null if the file does not exist.
		/// </summary>
		/// <param name="fileId">FS internal id</param>
		/// <param name="storageName"></param>
		public Stream OpenRead(string fileId, string storageName)
		{
			var client = ClientFactory.CreateClient();
			return RestHelper.OpenRead(client, RestHelper.WithName($"{_apiV1}stream?id={RestHelper.Encode(fileId)}", storageName));
		}

		/// <summary>
		/// Same as <see cref="OpenRead"/>, by id from your system
		/// </summary>
		/// <param name="externalFileId">this is file id from your system</param>
		/// <param name="storageName"></param>
		public Stream OpenReadByExternalId(string externalFileId, string storageName)
		{
			var client = ClientFactory.CreateClient();
			return RestHelper.OpenRead(client, RestHelper.WithName($"{_apiV1}stream/external?id={RestHelper.Encode(externalFileId)}", storageName));
		}

		#endregion
	}
}