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
			_client = new CommandTcpSocketClient(settings.Port ?? 11881);
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
			var folderId = externalFolderId.GetSha1Hash();
			var fileId = externalFileId.GetSha1Hash();
			var tcpCommonMessage = new TcpCommonMessage()
			{
				RequestId = Guid.NewGuid().ToString(),
				Type = TcpMessageType.Write,
				WriteCommand = new WriteMessage()
				{
					FileId = fileId,
					FolderId = folderId,
					StorageName = storageName,
					Content = bytes,
					IsClose = close,
					SessionId = sessionId

				}
			};
			var data = _client.SendReceive(tcpCommonMessage);

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
					FileId = externalFolderId,
					FolderId = externalFileId,
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
			var folderId = externalFolderId.GetSha1Hash();
			var fileId = externalFileId.GetSha1Hash();
			var tcpCommonMessage = new TcpCommonMessage()
			{
				RequestId = Guid.NewGuid().ToString(),
				Type = TcpMessageType.IsExistsByExternalId,
				ReadCommand = new ReadMessage()
				{
					FileId = fileId,
					FolderId = folderId,
					StorageName = storageName
				}
			};
			var data = _client.SendReceive(tcpCommonMessage);
			if (data != null)
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
		/// <param name="stream"></param>
		/// <param name="sessionId"></param>
		public void Write(string externalFolderId, string externalFileId, string storageName, Stream stream, string sessionId)
		{
			var length = 64001;

			var byteCount = 0;
			var buffer = new byte[length];
			var len = stream.Length;
			var close = false;
			while (!close && (byteCount = stream.Read(buffer, 0, buffer.Length)) > 0)
			{
				close = length > byteCount;
				if (len == stream.Position)
					close = length > byteCount;
				Write(externalFolderId, externalFileId, storageName, buffer.Take(byteCount).ToArray(), close, sessionId);
			}
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
			if (size == 0)
				return null;
			var folderId = externalFolderId.GetSha1Hash();
			var fileId = externalFileId.GetSha1Hash();
			var tcpCommonMessage = new TcpCommonMessage()
			{
				RequestId = Guid.NewGuid().ToString(),
				Type = TcpMessageType.Read,
				ReadCommand = new ReadMessage()
				{
					FileId = fileId,
					FolderId = folderId,
					StorageName = storageName
				}
			};
			var data = _client.SendReceive(tcpCommonMessage);
			if (data != null)
			{
				return data.ReadCommand.Content;
			}
			return null;



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
			var folderId = externalFolderId.GetSha1Hash();
			var fileId = externalFileId.GetSha1Hash();
			var tcpCommonMessage = new TcpCommonMessage()
			{
				RequestId = Guid.NewGuid().ToString(),
				Type = TcpMessageType.Delete,
				ReadCommand = new ReadMessage()
				{
					FileId = fileId,
					FolderId = folderId,
					StorageName = storageName
				}
			};
			var data = _client.SendReceive(tcpCommonMessage);
			if (data != null)
			{
				return true;
			}
			return false;
		}
		/// <summary>
		/// 50 Мб
		/// </summary>
		private static int _maxLen = 1024 * 1024 * 20;
		/// <summary>
		/// 
		/// </summary>
		/// <param name="externalFolderId">folder id from your system</param>
		/// <param name="externalFileId">file id from your system</param>
		/// <param name="name"></param>
		/// <returns></returns>
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
				var offset = 0;

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
				//result.Read(bytes, 0, bytes.Length);
				//_cache[internalId] = bytes;

				result.Seek(0, SeekOrigin.Begin);
				return result;
			}
			return null;
		}

		public void Move(string externalFolderId, string toExternalFolderId, string externalFileId, string storageName)
		{
			var toFolderId = toExternalFolderId.GetSha1Hash();
			var folderId = externalFolderId.GetSha1Hash();
			var fileId = externalFileId.GetSha1Hash();
			var tcpCommonMessage = new TcpCommonMessage()
			{
				RequestId = Guid.NewGuid().ToString(),
				Type = TcpMessageType.Move,

				MoveCommand = new MoveMessage()
				{
					FromFileId = fileId,
					FromFolderId = folderId,
					ToFolderId = toFolderId,
					ToFileId = fileId,
					StorageName = storageName
				}
			};
			var data = _client.SendReceive(tcpCommonMessage);
		}

		public void Dispose()
		{
			_client?.Stop();
			_client?.Dispose();
		}
	}
}