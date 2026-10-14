using Service.Registry.Common;
using System;
using System.IO;
using System.Linq;
using Service.Registry.Common.Entities;

namespace FileStorage.Contracts.Rest.Impl.FileStorage
{

	/// <summary>
	/// Impl <see cref="IFileStorageRestService"/>
	/// </summary>
	public sealed class TcpFileStorageService : TcpClientResponseBase, ITcpFileStorageService, IDisposable
	{
		private readonly CommandTcpSocketClient _client;

		/// <summary>
		/// Ctor
		/// </summary>
		/// <param name="settings"><see cref="RestClientResponseSettings"/></param>
		public TcpFileStorageService(TcpClientResponseSettings settings) : base(settings)
		{
			// порт по умолчанию — как у сервера (Connection:Tcp:Port = 11581); раньше было 11881
			_client = new CommandTcpSocketClient(settings.Port ?? 11581);
			_client.Connect(settings.Address);
		}


		public string GetIdByExternal(string externalId)
		{
			var folderId = externalId.GetSha1Hash();
			return folderId;

		}

		public long GetCount(string storageName)
		{
			var tcpCommonMessage = new TcpCommonMessage()
			{
				RequestId = Guid.NewGuid().ToString(),
				Type = TcpMessageType.GetCount,
				ReadCommand = new ReadMessage()
				{
					StorageName = storageName
				}
			};
			var data = _client.SendReceive(tcpCommonMessage);
			if (data?.ReadCommand != null)
			{
				return data.ReadCommand.Count;
			}
			return 0;

		}

		public void SaveBytes(string fileId, byte[] bytes, string storageName, string sessionId)
		{
			// fileId — внутренний id; раньше он хешировался ещё раз, и файл сохранялся под другим id
			Write(fileId, storageName, bytes, true, sessionId);
		}

		public void SaveBytes(string fileId, string storageName, Stream stream, string sessionId)
		{
			// fileId — внутренний id, без повторного хеширования
			stream.WriteChunks(64 * 1024, (chunk, close) => Write(fileId, storageName, chunk, close, sessionId));
		}

		public void WriteToStream(string fileId, string storageName, Stream stream)
		{
			long offset = 0;
			var totalLen = GetSize(fileId, storageName);
			while (offset < totalLen)
			{
				var size = Math.Min(TcpResponse.ReadChunkSize, totalLen - offset);
				var read = SendRead(fileId, offset, size, storageName);
				if (read?.Content == null)
					return;
				if (!read.IsPartial)
				{
					// старый сервер возвращает файл целиком — дописываем остаток и заканчиваем
					if (offset < read.Content.Length)
						stream.Write(read.Content, (int)offset, read.Content.Length - (int)offset);
					return;
				}
				if (read.Content.Length == 0)
					return;
				stream.Write(read.Content, 0, read.Content.Length);
				offset += read.Content.Length;
			}
		}


		/// <summary>
		/// <see cref="IFileStorageRestService.Write(string,string,byte[],string)"/>
		/// </summary> 
		/// <param name="externalFileId"></param>
		/// <param name="bytes"></param>
		/// <param name="storageName"></param>
		/// <param name="sessionId"></param>
		public void Write(string externalFileId, string storageName, byte[] bytes, string sessionId)
		{
			var fileId = externalFileId.GetSha1Hash();
			Write(fileId, storageName, bytes, true, sessionId);
		}

		/// <summary>
		/// <see cref="IFolderStorageRestService.Write(string,string,string,System.IO.Stream,string)"/>
		/// </summary> 
		/// <param name="fileId">internal FS id</param>
		/// <param name="storageName"></param>
		/// <param name="bytes"></param>
		/// <param name="close"></param>
		/// <param name="sessionId"></param>
		private void Write(string fileId, string storageName, byte[] bytes, bool close, string sessionId)
		{
			var tcpCommonMessage = new TcpCommonMessage()
			{
				RequestId = Guid.NewGuid().ToString(),
				Type = TcpMessageType.Write,
				WriteCommand = new WriteMessage()
				{
					FileId = fileId,
					StorageName = storageName,
					Content = bytes,
					IsClose = close,
					SessionId = sessionId
				}
			};
			var data = _client.SendReceive(tcpCommonMessage);
			// раньше ответ не проверялся, и ошибка записи терялась
			TcpResponse.EnsureOk(data, "write");
		}


		/// <summary>
		/// 
		/// </summary>
		/// <param name="fileId">internal FS id</param>
		/// <param name="storageName"></param>
		/// <returns></returns>
		public bool Exists(string fileId, string storageName)
		{
			var tcpCommonMessage = new TcpCommonMessage()
			{
				RequestId = Guid.NewGuid().ToString(),
				Type = TcpMessageType.IsExists,
				ReadCommand = new ReadMessage()
				{
					FileId = fileId,
					StorageName = storageName
				}
			};
			var data = _client.SendReceive(tcpCommonMessage);
			if (data?.ReadCommand != null)
			{
				return data.ReadCommand.IsExists;
			}
			return false;
		}

		public string GetExistIdByExternal(string externalFileId, string storageName)
		{
			var tcpCommonMessage = new TcpCommonMessage()
			{
				RequestId = Guid.NewGuid().ToString(),
				Type = TcpMessageType.IsExistsByExternalId,
				ReadCommand = new ReadMessage()
				{
					FileId = externalFileId,
					StorageName = storageName
				}
			};
			var data = _client.SendReceive(tcpCommonMessage);
			if (data?.ReadCommand != null)
			{
				return data.ReadCommand.FileId;
			}
			return "";
		}
		/// <summary>
		/// Return file size
		/// </summary>
		/// <param name="fileId">internal FS id</param>
		/// <param name="storageName"></param>
		/// <returns></returns>
		public long GetSize(string fileId, string storageName)
		{ 
			var tcpCommonMessage = new TcpCommonMessage()
			{
				RequestId = Guid.NewGuid().ToString(),
				Type = TcpMessageType.GetSize,
				ReadCommand = new ReadMessage()
				{
					FileId = fileId,
					StorageName = storageName
				}
			};
			var data = _client.SendReceive(tcpCommonMessage);
			if (data?.ReadCommand != null)
			{
				return data.ReadCommand.Size;
			}
			return 0;
		}
		/// <summary>
		/// Is exists on FS
		/// </summary> 
		/// <param name="externalFileId">this is file id from your system</param> 
		/// <param name="storageName"></param>
		/// <returns></returns>
		public bool IsExists(string externalFileId, string storageName)
		{
			var fileId = externalFileId.GetSha1Hash();
			return Exists(fileId, storageName);

		}


		/// <summary>
		/// Is exists by externalId
		/// </summary>
		/// <param name="externalFileId">id from your system</param>
		/// <param name="storageName"></param>
		/// <returns></returns>
		public bool IsExistsByExternalId(string externalFileId, string storageName)
		{
			// Сервер сам хеширует внешний id и возвращает в ответе FileId, а не IsExists.
			// Раньше id хешировался дважды, а результат читался из IsExists, поэтому всегда был false.
			return !string.IsNullOrEmpty(GetExistIdByExternal(externalFileId, storageName));
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
			// раньше метод не был реализован (NotImplementedException)
			if (size <= 0)
				return null;
			var read = SendRead(fileId, offset, size, storageName);
			return TcpResponse.GetRange(read, offset, size);
		}

		/// <summary>
		/// Команда Read по внутреннему id: просит у сервера только часть файла (Offset/Length)
		/// </summary>
		private ReadMessage SendRead(string fileId, long offset, long size, string storageName)
		{
			var tcpCommonMessage = new TcpCommonMessage()
			{
				RequestId = Guid.NewGuid().ToString(),
				Type = TcpMessageType.Read,
				ReadCommand = new ReadMessage()
				{
					FileId = fileId,
					StorageName = storageName,
					Offset = offset,
					Length = size
				}
			};
			var data = _client.SendReceive(tcpCommonMessage);
			return TcpResponse.IsOk(data) ? data.ReadCommand : null;
		}
		/// <summary>
		/// Save stream to FS
		/// </summary> 
		/// <param name="externalFileId">this is file id from your system</param> 
		/// <param name="storageName"></param>
		/// <param name="stream"></param>
		/// <param name="sessionId"></param>
		public void Write(string externalFileId, string storageName, Stream stream, string sessionId)
		{
			var fileId = externalFileId.GetSha1Hash();
			// Блоки по 64 КБ, у последнего — признак закрытия. Раньше при размере потока, кратном блоку,
			// файл не закрывался, а каждый блок копировался через Take().ToArray().
			stream.WriteChunks(64 * 1024, (chunk, close) => Write(fileId, storageName, chunk, close, sessionId));
		}
		
		/// <summary>
		/// Partial read 
		/// </summary> 
		/// <param name="externalFileId">this is file id from your system</param> 
		/// <param name="offset"></param>
		/// <param name="size"></param>
		/// <param name="storageName"></param>
		/// <returns></returns>
		public byte[] Read(string externalFileId, long offset, long size, string storageName)
		{
			// Раньше смещение и размер не передавались серверу: каждый вызов возвращал файл целиком,
			// и поток из GetStream собирал из этих ответов повреждённые данные.
			return GetBytes(externalFileId.GetSha1Hash(), offset, size, storageName);
		}/// <summary>
		/// Delete file from FS
		/// </summary>
		/// <param name="fileId">internal FS id</param>
		/// <param name="storageName"></param>
		/// <returns></returns>
		public bool Delete(string fileId, string storageName)
		{
			var tcpCommonMessage = new TcpCommonMessage()
			{
				RequestId = Guid.NewGuid().ToString(),
				Type = TcpMessageType.Delete,
				ReadCommand = new ReadMessage()
				{
					FileId = fileId,
					StorageName = storageName
				}
			};
			var data = _client.SendReceive(tcpCommonMessage);
			// раньше любой ответ, в том числе с ошибкой, считался успешным удалением
			return TcpResponse.IsOk(data);
		}

		/// <summary>
		/// <see cref="IFileStorageRestService.DeleteByExternalId(string, string)"/>
		/// </summary>
		public bool DeleteByExternalId(string externalFileId, string storageName)
		{
			return IsExists(externalFileId, storageName) && Delete(externalFileId.GetSha1Hash(), storageName);
		} 

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

				var result = new InternalFileStream(Read, size, externalFileId, storageName);


				return result;
			}
			return null;
		}

		public void Dispose()
		{
			_client?.Stop();
			_client?.Dispose();
		}
	}
}