using FileStorage.Core;
using FileStorage.Core.Contracts;
using FileStorage.Core.Interfaces;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace FileStorage.Contracts.Impl.Connections
{
	public sealed class MessageProcessingService : IMessageProcessingService
	{
		private readonly ITcpClientRegistry _tcpClientRegistry;
		private readonly ILocalLogger _localLogger;
		private readonly Encoding _encoding;
		private readonly IFolderStorageService _folderStorageService;
		private readonly IFolderStorageFactory _folderStorageFactory;
		private readonly IFileStorageService _fileStorageService;
		private readonly IFileStorageFactory<string> _fileStorageFactory;
		private readonly object _lockLast = new object();

		public MessageProcessingService(
			ITcpClientRegistry tcpClientRegistry,
			IFolderStorageFactory folderStorageService,
			IFolderStorageService folderStorageFactory,
			ILocalLogger localLogger,
			IFileStorageFactory<string> fileStorageFactory,
			IFileStorageService fileStorageService)
		{
			_tcpClientRegistry = tcpClientRegistry;
			_folderStorageService = folderStorageFactory;
			_folderStorageFactory = folderStorageService;

			_localLogger = localLogger;
			_fileStorageFactory = fileStorageFactory;
			_fileStorageService = fileStorageService;
			_encoding = Encoding.UTF8;

		}


		public ProcessingResponse Process(byte[] bytes, IWebSocketContainer from)
		{
			_localLogger.Info($"Receive: {bytes.Length}");
			return Process(bytes.GetCommands(), from);
		}

		/// <summary>
		/// Обрабатывает все команды пачки. Раньше обрабатывалась только первая, остальные терялись.
		/// Возвращает первый непустой ответ; если его нет, а при обработке была ошибка, — ответ с ошибкой.
		/// </summary>
		public ProcessingResponse Process(IReadOnlyList<string> commands, IWebSocketContainer from)
		{
			ProcessingResponse response = null;
			TcpCommonMessage error = null;
			var processedAny = false;
			foreach (var command in commands)
			{
				if (string.IsNullOrWhiteSpace(command))
					continue;
				processedAny = true;
				TcpCommonMessage message = null;
				try
				{
					message = MapData(command);
					var commandResponse = ProcessSingleCommand(@from, message);
					if (response == null)
						response = commandResponse;
				}
				catch (Exception e)
				{
					error = message ?? new TcpCommonMessage();
					// содержимое файла из запроса обратно не отправляем
					if (error.ReadCommand != null)
						error.ReadCommand.Content = null;
					if (error.WriteCommand != null)
						error.WriteCommand.Content = null;
					error.Error = new ErrorChatMessage() { Error = e.Message };
					error.Type = TcpMessageType.InternalServerError;
					// саму команду (в ней может быть содержимое файла) в лог не пишем
					_localLogger?.Error($"{error.RequestId};{e.Message}", e);
				}
			}

			if (response != null || (processedAny && error == null))
				return response;

			// как и раньше: без команд или при ошибке — ответ с сериализованным сообщением
			return new ProcessingResponse()
			{
				Bytes = GetBytes(error)
			};
		}

		private ProcessingResponse ProcessSingleCommand(IWebSocketContainer @from,
			TcpCommonMessage message)
		{
			var messageReadCommand = message.ReadCommand;
			var tcpCommonMessage = new TcpCommonMessage()
			{
				RequestId = message.RequestId,
				Type = message.Type,
				ReadCommand = messageReadCommand,
				WriteCommand = message.WriteCommand,
			};

			switch (message?.Type)
			{
				case TcpMessageType.Ping:
					//return new ProcessingResponse()
					//{
					//    Id = message.RequestId,
					//    Type = message.Type,
					//    Status = ResponseStatus.Ok
					//}; 
					break;
				case TcpMessageType.OpenConnection:
					{
						return new ProcessingResponse()
						{
							Id = message.RequestId,
							Type = message.Type,
							Status = ResponseStatus.Ok
						};
					}
				case TcpMessageType.IsExists:
					if (IsValidMessage(@from, message))
					{
						return null;
					}

					if (!string.IsNullOrEmpty(messageReadCommand.FolderId))
					{
						var info = GetFolderStorageInfo(messageReadCommand);
						tcpCommonMessage.ReadCommand.IsExists = _folderStorageService.Exists(info);
						@from.Broadcast(tcpCommonMessage);
					}
					else if (!string.IsNullOrEmpty(messageReadCommand.FileId))
					{
						tcpCommonMessage.ReadCommand.IsExists = _fileStorageService.Exists(messageReadCommand.FileId, messageReadCommand.StorageName);
						@from.Broadcast(tcpCommonMessage);
					}

					break;
				case TcpMessageType.Read:
					if (IsValidMessage(@from, message))
					{
						return null;
					}

					{
						// Length > 0 — чтение части файла (Offset/Length); иначе, как раньше, файл целиком.
						// Признак IsPartial сообщает клиенту, что вернулась только запрошенная часть.
						var partial = messageReadCommand.Length > 0;
						var length = (int)Math.Min(messageReadCommand.Length, int.MaxValue);
						if (!string.IsNullOrEmpty(messageReadCommand.FolderId))
						{
							var info = GetFolderStorageInfo(messageReadCommand);
							tcpCommonMessage.ReadCommand.Content = partial
								? _folderStorageService.GetBytesOffset(info, messageReadCommand.Offset, length)
								: _folderStorageService.GetBytes(info);
							tcpCommonMessage.ReadCommand.IsPartial = partial;
							@from.Broadcast(tcpCommonMessage);
						}
						else if (!string.IsNullOrEmpty(messageReadCommand.FileId))
						{
							tcpCommonMessage.ReadCommand.Content = partial
								? _fileStorageService.GetBytesOffset(messageReadCommand.FileId, messageReadCommand.Offset, length, messageReadCommand.StorageName)
								: _fileStorageService.GetBytes(messageReadCommand.FileId, messageReadCommand.StorageName);
							tcpCommonMessage.ReadCommand.IsPartial = partial;
							@from.Broadcast(tcpCommonMessage);
						}
					}
					break;
				case TcpMessageType.IsExistsByExternalId:
					if (IsValidMessage(@from, message))
					{
						return null;
					}

					if (!string.IsNullOrEmpty(messageReadCommand.FolderId))
					{
						var info = GetFolderStorageInfo(messageReadCommand);
						tcpCommonMessage.ReadCommand.FileId = _folderStorageService.GetExistIdByExternal(info.FolderId, info.FileId, messageReadCommand.StorageName);
						@from.Broadcast(tcpCommonMessage);
					}
					else if (!string.IsNullOrEmpty(messageReadCommand.FileId))
					{
						tcpCommonMessage.ReadCommand.FileId = _fileStorageService.GetExistIdByExternal(messageReadCommand.FileId, messageReadCommand.StorageName);
						@from.Broadcast(tcpCommonMessage);
					}
					break;
				case TcpMessageType.GetSize:
					if (IsValidMessage(@from, message))
					{
						return null;
					}

					if (!string.IsNullOrEmpty(messageReadCommand.FolderId))
					{
						var info = GetFolderStorageInfo(messageReadCommand);
						tcpCommonMessage.ReadCommand.Size = _folderStorageService.GetSize(info);
						@from.Broadcast(tcpCommonMessage);
					}
					else if (!string.IsNullOrEmpty(messageReadCommand.FileId))
					{
						tcpCommonMessage.ReadCommand.Size = _fileStorageService.GetSize(messageReadCommand.FileId, messageReadCommand.StorageName);
						@from.Broadcast(tcpCommonMessage);
					}
					break;
				case TcpMessageType.GetCount:
					if (IsValidMessage(@from, message))
					{
						return null;
					}

					if (!string.IsNullOrEmpty(messageReadCommand.FolderId))
					{
						tcpCommonMessage.ReadCommand.Count = _folderStorageService.GetCount(messageReadCommand.StorageName);
						@from.Broadcast(tcpCommonMessage);
					}
					else if (!string.IsNullOrEmpty(messageReadCommand.FileId))
					{
						tcpCommonMessage.ReadCommand.Count = _fileStorageService.GetCount(messageReadCommand.StorageName);
						@from.Broadcast(tcpCommonMessage);
					}
					break;

				case TcpMessageType.Move:
					var moveCommand = message.MoveCommand;
					if (IsValidMessage(@from, moveCommand, message.RequestId))
					{
						return null;
					}

					if (!string.IsNullOrEmpty(moveCommand.FromFolderId))
					{ 
						_folderStorageService.Move(moveCommand.ToFolderId, moveCommand.FromFolderId, moveCommand.FromFileId, moveCommand.StorageName);
						@from.Broadcast(tcpCommonMessage);
					}
					else if (!string.IsNullOrEmpty(moveCommand.FromFileId))
					{
						_fileStorageService.Move(moveCommand.FromFileId, moveCommand.ToFileId, moveCommand.StorageName);
						@from.Broadcast(tcpCommonMessage);
					}
					break;
				case TcpMessageType.Delete:
					if (IsValidMessage(@from, message))
					{
						return null;
					}

					if (!string.IsNullOrEmpty(messageReadCommand.FolderId))
					{
						var info = GetFolderStorageInfo(messageReadCommand);
						_folderStorageService.Delete(info);
						@from.Broadcast(tcpCommonMessage);
					}
					else if (!string.IsNullOrEmpty(messageReadCommand.FileId))
					{
						_fileStorageService.Delete(messageReadCommand.FileId, messageReadCommand.StorageName);
						@from.Broadcast(tcpCommonMessage);
					}
					break;
				case TcpMessageType.Write:
					var messageWriteCommand = message.WriteCommand;
					if (messageWriteCommand == null)
					{
						@from.Broadcast(new TcpCommonMessage()
						{
							RequestId = message.RequestId,
							Type = TcpMessageType.BadRequest,
							Error = new ErrorChatMessage() { Error = "ReadCommand is empty" }
						});
						{
							return null;
						}
					}

					if (!string.IsNullOrEmpty(messageWriteCommand.FolderId))
					{
						_folderStorageService.SaveById(new FileBatchRequest()
						{
							StorageName = messageWriteCommand.StorageName,
							FolderId = messageWriteCommand.FolderId,
							SessionId = messageWriteCommand.SessionId,
							Bytes = messageWriteCommand.Content,
							Close = messageWriteCommand.IsClose,
							Id = messageWriteCommand.FileId,
						});
						tcpCommonMessage.WriteCommand.Content = null;
						@from.Broadcast(new TcpCommonMessage()
						{
							RequestId = message.RequestId,
							Type = message.Type,
							WriteCommand = message.WriteCommand
						});
					}
					else if (!string.IsNullOrEmpty(messageWriteCommand.FileId))
					{
						_fileStorageService.SaveById(new FileBatchRequest()
						{
							StorageName = messageWriteCommand.StorageName,
							FolderId = messageWriteCommand.FolderId,
							SessionId = messageWriteCommand.SessionId,
							Bytes = messageWriteCommand.Content,
							Close = messageWriteCommand.IsClose,
							Id = messageWriteCommand.FileId,
						});
						tcpCommonMessage.WriteCommand.Content = null;
						@from.Broadcast(new TcpCommonMessage()
						{
							RequestId = message.RequestId,
							Type = message.Type,
							WriteCommand = message.WriteCommand
						});
					}

					break;
			}

			return null;
		}

		private static bool IsValidMessage(IWebSocketContainer @from, TcpCommonMessage message)
		{
			if (message.ReadCommand == null)
			{
				@from.Broadcast(new TcpCommonMessage()
				{
					RequestId = message.RequestId,
					Type = TcpMessageType.BadRequest,
					Error = new ErrorChatMessage() { Error = "ReadCommand is empty" }
				});
				return true;
			}

			return false;
		}
		private static bool IsValidMessage(IWebSocketContainer @from, MoveMessage message, string requestId)
		{
			if (message == null)
			{
				@from.Broadcast(new TcpCommonMessage()
				{
					RequestId = requestId,
					Type = TcpMessageType.BadRequest,
					Error = new ErrorChatMessage() { Error = "ReadCommand is empty" }
				});
				return true;
			}

			return false;
		}
		private FolderStorageInfo GetFolderStorageInfo(WriteMessage message)
		{
			var folderStorageInfo = new FolderStorageInfo()
			{
				FileId = message.FileId,
				FolderId = message.FolderId,
				StorageName = message.StorageName
			};
			return folderStorageInfo;
		}
		private FolderStorageInfo GetFolderStorageInfo(ReadMessage message)
		{
			var folderStorageInfo = new FolderStorageInfo()
			{
				FileId = message.FileId,
				FolderId = message.FolderId,
				StorageName = message.StorageName
			};
			return folderStorageInfo;
		}
		private TcpCommonMessage MapData(string data)
		{
			//_localLogger?.Info(data); 
			var message = JsonConvert.DeserializeObject<TcpCommonMessage>(data);
			_localLogger.Info($"Start: {message.RequestId}");


			return message;
		}


		private byte[] GetBytes(TcpCommonMessage response)
		{

			var serializeObject = response.SerializeObject();
			var resultBytes = _encoding.GetBytes(serializeObject);

			// Раньше в лог писался весь ответ, включая содержимое файла в base64: огромные строки на каждый ответ.
			_localLogger.Info($"Processed: {response?.RequestId}, Bytes:{resultBytes.Length}");
			return resultBytes;
		}






	}
}
