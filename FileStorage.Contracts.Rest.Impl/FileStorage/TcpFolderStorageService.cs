using Service.Registry.Common.Entities;
using System;
using System.IO;
using System.Linq;

namespace FileStorage.Contracts.Rest.Impl.FileStorage
{

	/// <summary>
	/// Impl <see cref="IFolderStorageRestService"/>
	/// </summary>
	public sealed class TcpFolderStorageService : TcpClientResponseBase, ITcpFolderStorageService, IDisposable
	{
		private readonly CommandTcpSocketClient _client;

		/// <summary>
		/// Ctor
		/// </summary>
		/// <param name="settings"><see cref="RestClientResponseSettings"/></param>
		public TcpFolderStorageService(TcpClientResponseSettings settings) : base(settings)
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


		/// <summary>
		/// <see cref="IFolderStorageRestService.Write(string,string,string,byte[],string)"/>
		/// </summary>
		/// <param name="externalFolderId">folder id from your system</param>
		/// <param name="externalFileId">file id from your system</param>
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
		/// <param name="externalFolderId">folder id from your system</param>
		/// <param name="externalFileId">file id from your system</param>
		/// <param name="storageName"></param>
		/// <param name="bytes"></param>
		/// <param name="close"></param>
		/// <param name="sessionId"></param>
		private void Write(string externalFolderId, string externalFileId, string storageName, byte[] bytes, bool close, string sessionId)
		{
			var tcpCommonMessage = new TcpCommonMessage()
			{
				RequestId = Guid.NewGuid().ToString(),
				Type = TcpMessageType.Write,
				WriteCommand = new WriteMessage()
				{
					FileId = externalFileId.GetSha1Hash(),
					FolderId = externalFolderId.GetSha1Hash(),
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
		/// <param name="externalFolderId">folder id from your system</param>
		/// <param name="externalFileId">file id from your system</param>
		/// <param name="storageName"></param>
		/// <returns></returns>
		public string GetExistIdByExternal(string externalFolderId, string externalFileId, string storageName)
		{
			var tcpCommonMessage = new TcpCommonMessage()
			{
				RequestId = Guid.NewGuid().ToString(),
				Type = TcpMessageType.IsExistsByExternalId,
				ReadCommand = new ReadMessage()
				{
					// раньше id папки и файла были перепутаны местами
					FileId = externalFileId,
					FolderId = externalFolderId,
					StorageName = storageName
				}
			};
			var data = _client.SendReceive(tcpCommonMessage);
			if (TcpResponse.IsOk(data) && data.ReadCommand != null)
			{
				return data.ReadCommand.FileId;
			}
			return "";
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="externalFolderId">folder id from your system</param>
		/// <param name="externalFileId">file id from your system</param>
		/// <param name="storageName"></param>
		/// <returns></returns>
		public long GetSize(string externalFolderId, string externalFileId, string storageName)
		{
			var folderId = externalFolderId.GetSha1Hash();
			var fileId = externalFileId.GetSha1Hash();
			var tcpCommonMessage = new TcpCommonMessage()
			{
				RequestId = Guid.NewGuid().ToString(),
				Type = TcpMessageType.GetSize,
				ReadCommand = new ReadMessage()
				{
					FileId = fileId,
					FolderId = folderId,
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
		/// 
		/// </summary>
		/// <param name="externalFolderId">folder id from your system</param>
		/// <param name="externalFileId">file id from your system</param>
		/// <param name="storageName"></param>
		/// <returns></returns>
		public bool IsExists(string externalFolderId, string externalFileId, string storageName)
		{
			var folderId = externalFolderId.GetSha1Hash();
			var fileId = externalFileId.GetSha1Hash();
			var tcpCommonMessage = new TcpCommonMessage()
			{
				RequestId = Guid.NewGuid().ToString(),
				Type = TcpMessageType.IsExists,
				ReadCommand = new ReadMessage()
				{
					FileId = fileId,
					FolderId = folderId,
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



		/// <summary>
		/// 
		/// </summary>
		/// <param name="externalFolderId">folder id from your system</param>
		/// <param name="externalFileId">file id from your system</param>
		/// <param name="storageName"></param>
		/// <returns></returns>
		public bool IsExistsByExternalId(string externalFolderId, string externalFileId, string storageName)
		{
			// Сервер сам хеширует внешние id и возвращает в ответе FileId, а не IsExists.
			// Раньше id хешировались дважды, а результат читался из IsExists, поэтому всегда был false.
			return !string.IsNullOrEmpty(GetExistIdByExternal(externalFolderId, externalFileId, storageName));
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="externalFolderId">folder id from your system</param>
		/// <param name="externalFileId">file id from your system</param>
		/// <param name="storageName"></param>
		/// <param name="stream"></param>
		/// <param name="sessionId"></param>
		public void Write(string externalFolderId, string externalFileId, string storageName, Stream stream, string sessionId)
		{
			// Блоки по 64 КБ, у последнего — признак закрытия. Раньше при размере потока, кратном блоку,
			// файл не закрывался, а каждый блок копировался через Take().ToArray().
			stream.WriteChunks(64 * 1024, (chunk, close) => Write(externalFolderId, externalFileId, storageName, chunk, close, sessionId));
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="externalFolderId">folder id from your system</param>
		/// <param name="externalFileId">file id from your system</param>
		/// <param name="storageName"></param>
		/// <param name="stream"></param>
		public void Read(string externalFolderId, string externalFileId, string storageName, Stream stream)
		{
			// Раньше сюда передавались уже захешированные id, а GetSize и Read хешировали их ещё раз:
			// запрашивался несуществующий файл, и в поток ничего не записывалось.
			var folderId = externalFolderId.GetSha1Hash();
			var fileId = externalFileId.GetSha1Hash();
			long offset = 0;
			var totalLen = GetSize(externalFolderId, externalFileId, storageName);
			while (offset < totalLen)
			{
				var size = Math.Min(TcpResponse.ReadChunkSize, totalLen - offset);
				var read = SendRead(folderId, fileId, offset, size, storageName);
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
		/// 
		/// </summary>
		/// <param name="externalFolderId">folder id from your system</param>
		/// <param name="externalFileId">file id from your system</param>
		/// <param name="offset"></param>
		/// <param name="size"></param>
		/// <param name="storageName"></param>
		/// <returns></returns>
		public byte[] Read(string externalFolderId, string externalFileId, long offset, long size, string storageName)
		{
			if (size <= 0)
				return null;
			// Раньше смещение и размер не передавались серверу, и каждый вызов возвращал файл целиком.
			var read = SendRead(externalFolderId.GetSha1Hash(), externalFileId.GetSha1Hash(), offset, size, storageName);
			return TcpResponse.GetRange(read, offset, size);
		}

		/// <summary>
		/// Команда Read по внутренним id: просит у сервера только часть файла (Offset/Length)
		/// </summary>
		private ReadMessage SendRead(string folderId, string fileId, long offset, long size, string storageName)
		{
			var tcpCommonMessage = new TcpCommonMessage()
			{
				RequestId = Guid.NewGuid().ToString(),
				Type = TcpMessageType.Read,
				ReadCommand = new ReadMessage()
				{
					FileId = fileId,
					FolderId = folderId,
					StorageName = storageName,
					Offset = offset,
					Length = size
				}
			};
			var data = _client.SendReceive(tcpCommonMessage);
			return TcpResponse.IsOk(data) ? data.ReadCommand : null;
		}
		/// <summary>
		/// 
		/// </summary>
		/// <param name="externalFolderId">folder id from your system</param>
		/// <param name="externalFileId">file id from your system</param>
		/// <param name="storageName"></param>
		/// <returns></returns>
		public bool Delete(string externalFolderId, string externalFileId, string storageName)
		{
			var tcpCommonMessage = new TcpCommonMessage()
			{
				RequestId = Guid.NewGuid().ToString(),
				Type = TcpMessageType.Delete,
				ReadCommand = new ReadMessage()
				{
					FileId = externalFileId.GetSha1Hash(),
					FolderId = externalFolderId.GetSha1Hash(),
					StorageName = storageName
				}
			};
			var data = _client.SendReceive(tcpCommonMessage);
			// раньше любой ответ, в том числе с ошибкой, считался успешным удалением
			return TcpResponse.IsOk(data);
		}

		/// <summary>
		/// Reads the whole file into memory (or a temporary file for large files) and returns it
		/// </summary>
		/// <param name="externalFolderId">folder id from your system</param>
		/// <param name="externalFileId">file id from your system</param>
		/// <param name="name"></param>
		/// <returns></returns>
		public Stream GetStream(string externalFolderId, string externalFileId, string name)
		{
			if (!IsExists(externalFolderId, externalFileId, name))
				return null;

			// Раньше файлы меньше 10 байт возвращались пустыми (буфер размером size / 10 был нулевым),
			// а временный файл для больших файлов не удалялся.
			var result = new MixedMemoryStream(true);
			Read(externalFolderId, externalFileId, name, result);
			result.Seek(0, SeekOrigin.Begin);
			return result;
		}

		public void Move(string externalFolderId, string toExternalFolderId, string externalFileId, string storageName)
		{
			var fileId = externalFileId.GetSha1Hash();
			var tcpCommonMessage = new TcpCommonMessage()
			{
				RequestId = Guid.NewGuid().ToString(),
				Type = TcpMessageType.Move,
				MoveCommand = new MoveMessage()
				{
					FromFileId = fileId,
					FromFolderId = externalFolderId.GetSha1Hash(),
					ToFolderId = toExternalFolderId.GetSha1Hash(),
					ToFileId = fileId,
					StorageName = storageName
				}
			};
			var data = _client.SendReceive(tcpCommonMessage);
			// раньше ответ не проверялся
			TcpResponse.EnsureOk(data, "move");
		}

		public void Dispose()
		{
			_client?.Stop();
			_client?.Dispose();
		}
	}
}