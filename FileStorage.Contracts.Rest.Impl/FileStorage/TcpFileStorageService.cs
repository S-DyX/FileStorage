﻿using Service.Registry.Common;
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

		public void SaveBytes(string fileId, byte[] bytes, string storageName, string sessionId)
		{
			Write(fileId, storageName, bytes, sessionId);
		}

		public void SaveBytes(string fileId, string storageName, Stream stream, string sessionId)
		{
			Write(fileId, storageName, stream, sessionId);
		}

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

				bytes = Read(fileId, offset, size, storageName);
				if (bytes == null)
					return;
				stream.Write(bytes, 0, bytes.Length);
				offset += bytes.Length;
			} while (offset < totalLen);
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

		public long GetSize(string externalFileId, string storageName)
		{
			var fileId = externalFileId.GetSha1Hash();
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

		public bool IsExists(string externalFileId, string storageName)
		{
			var fileId = externalFileId.GetSha1Hash();
			return Exists(fileId, storageName);

		}



		/// <summary>
		/// 
		/// </summary> 
		/// <param name="externalFileId"></param>
		/// <param name="storageName"></param>
		/// <returns></returns>
		public bool IsExistsByExternalId(string externalFileId, string storageName)
		{
			var fileId = externalFileId.GetSha1Hash();
			var tcpCommonMessage = new TcpCommonMessage()
			{
				RequestId = Guid.NewGuid().ToString(),
				Type = TcpMessageType.IsExistsByExternalId,
				ReadCommand = new ReadMessage()
				{
					FileId = fileId,
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

		public byte[] GetBytes(string id, long offset, long size, string storageName)
		{
			throw new NotImplementedException();
		}

		public void Write(string externalFileId, string storageName, Stream stream, string sessionId)
		{
			var length = 64000;

			var byteCount = 0;
			var buffer = new byte[length];
			var len = stream.Length;
			var close = false;
			var fileId = externalFileId.GetSha1Hash();
			while (!close && (byteCount = stream.Read(buffer, 0, buffer.Length)) > 0)
			{
				close = length > byteCount;
				Write(fileId, storageName, buffer.Take(byteCount).ToArray(), close, sessionId);
			}
		}

		public void Read(string externalFileId, string storageName, Stream stream)
		{
			var fileId = externalFileId.GetSha1Hash();

			Read(fileId, storageName, stream);

		}
		public byte[] Read(string externalFileId, long offset, long size, string storageName)
		{
			if (size == 0)
				return null;

			var fileId = externalFileId.GetSha1Hash();
			var tcpCommonMessage = new TcpCommonMessage()
			{
				RequestId = Guid.NewGuid().ToString(),
				Type = TcpMessageType.Read,
				ReadCommand = new ReadMessage()
				{
					FileId = fileId,
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
		public bool Delete(string externalFileId, string storageName)
		{
			var fileId = externalFileId.GetSha1Hash();
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
		public Stream GetStream(string externalFileId, string name)
		{
			if (IsExists(externalFileId, name))
			{

				var size = GetSize(externalFileId, name);

				var result = new MixedMemoryStream();
				var len = size / 10;
				if (len > 1024 * 1024 * 20)
					len = _maxLen;
				var buffer = new byte[len];
				var offset = 0l;

				while (offset < size)
				{
					buffer = Read(externalFileId, offset, buffer.Length, name);
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

		public void Dispose()
		{
			_client?.Stop();
			_client?.Dispose();
		}
	}
}