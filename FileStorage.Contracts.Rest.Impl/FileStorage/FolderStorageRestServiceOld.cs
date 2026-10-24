using Service.Registry.Common.Entities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;

namespace FileStorage.Contracts.Rest.Impl.FileStorage
{
	/// <summary>
	/// Previous version of <see cref="FolderStorageRestService"/>: <see cref="GetStream"/> reads the whole file
	/// into memory (or a temporary file for large files) instead of reading it on demand.
	/// All other methods are delegated to <see cref="FolderStorageRestService"/>.
	/// </summary>
	/// <remarks>
	/// Раньше класс был полной копией FolderStorageRestService со всеми его ошибками
	/// (двойное хеширование id в Read, Delete всегда false, имя хранилища не передавалось в GetCount и т. д.).
	/// </remarks>
	[Obsolete("Use FolderStorageRestService")]
	public sealed class FolderStorageRestServiceOld : RestClientResponseBase, IFolderStorageRestService
	{
		private readonly FolderStorageRestService _service;

		/// <summary>
		/// Ctor
		/// </summary>
		/// <param name="settings"></param>
		public FolderStorageRestServiceOld(RestClientResponseSettings settings) : base(settings)
		{
			_service = new FolderStorageRestService(settings);
		}

		/// <summary>
		/// Ctor
		/// </summary>
		/// <param name="settings"></param>
		/// <param name="client"></param>
		public FolderStorageRestServiceOld(RestClientResponseSettings settings, IHttpClientFactory client) : base(settings, client)
		{
			_service = new FolderStorageRestService(settings, client);
		}

		/// <inheritdoc />
		public string GetIdByExternal(string externalId) => _service.GetIdByExternal(externalId);

		/// <inheritdoc />
		public long GetCount(string storageName) => _service.GetCount(storageName);

		/// <inheritdoc />
		public void Write(string externalFolderId, string externalFileId, string storageName, byte[] bytes, string sessionId)
			=> _service.Write(externalFolderId, externalFileId, storageName, bytes, sessionId);

		/// <inheritdoc />
		public void Write(string externalFolderId, string externalFileId, string storageName, Stream stream, string sessionId)
			=> _service.Write(externalFolderId, externalFileId, storageName, stream, sessionId);

		/// <inheritdoc />
		public void Read(string externalFolderId, string externalFileId, string storageName, Stream stream)
			=> _service.Read(externalFolderId, externalFileId, storageName, stream);

		/// <inheritdoc />
		public long GetSize(string externalFolderId, string externalFileId, string storageName)
			=> _service.GetSize(externalFolderId, externalFileId, storageName);

		/// <inheritdoc />
		public bool IsExists(string externalFolderId, string externalFileId, string storageName)
			=> _service.IsExists(externalFolderId, externalFileId, storageName);

		/// <inheritdoc />
		public string GetExistIdByExternal(string externalFolderId, string externalFileId, string storageName)
			=> _service.GetExistIdByExternal(externalFolderId, externalFileId, storageName);

		/// <inheritdoc />
		public bool IsExistsByExternalId(string externalFolderId, string externalFileId, string storageName)
			=> _service.IsExistsByExternalId(externalFolderId, externalFileId, storageName);

		/// <inheritdoc />
		public byte[] Read(string externalFolderId, string externalFileId, long offset, long size, string storageName)
			=> _service.Read(externalFolderId, externalFileId, offset, size, storageName);

		/// <inheritdoc />
		public bool Delete(string externalFolderId, string externalFileId, string storageName)
			=> _service.Delete(externalFolderId, externalFileId, storageName);

		/// <summary>
		/// Reads the whole file into memory (or a temporary file for large files) and returns it
		/// </summary>
		public Stream GetStream(string externalFolderId, string externalFileId, string name)
		{
			if (!_service.IsExists(externalFolderId, externalFileId, name))
				return null;

			// Временный файл удаляется при закрытии потока: раньше он оставался в каталоге Temp.
			// Раньше файлы меньше 10 байт возвращались пустыми: буфер размером size / 10 был нулевым.
			var result = new MixedMemoryStream(true);
			_service.Read(externalFolderId, externalFileId, name, result);
			result.Seek(0, SeekOrigin.Begin);
			return result;
		}

		/// <inheritdoc />
		public void Move(string externalFolderId, string toExternalFolderId, string externalFileId, string storageName)
			=> _service.Move(externalFolderId, toExternalFolderId, externalFileId, storageName);

		/// <inheritdoc />
		public List<string> GetStorageNames() => _service.GetStorageNames();

		/// <inheritdoc />
		public Stream OpenRead(string externalFolderId, string externalFileId, string storageName) => _service.OpenRead(externalFolderId, externalFileId, storageName);

		/// <inheritdoc />
		public string GetFullName(string externalFolderId, string externalFileId, string storageName) => _service.GetFullName(externalFolderId, externalFileId, storageName);

		/// <inheritdoc />
		public void ClearTtl(int daysAgo, string storageName) => _service.ClearTtl(daysAgo, storageName);

		/// <inheritdoc />
		public void ChangeDepth(int newDepth, string storageName) => _service.ChangeDepth(newDepth, storageName);

		/// <inheritdoc />
		public string GetVersion() => _service.GetVersion();
	}
}
