using System;

namespace FileStorage.Contracts.Rest.Impl.FileStorage
{
	public enum TcpMessageType
	{
		OpenConnection = 8,
		Write = 16,
		Read = 32,
		IsExists = 33,
		IsExistsByExternalId = 34,
		GetSize = 35,
		GetCount = 36,
		Delete = 37,
		Move = 38,
		Ping = 199,
		Ok = 200,
		BadRequest = 400,
		Unauthorized = 401,
		InternalServerError = 500,

	}
    public sealed class TcpCommonMessage
    {
        public string RequestId { get; set; }
        public TcpMessageType Type { get; set; } 

        public ReadMessage ReadCommand { get; set; }
        public WriteMessage WriteCommand { get; set; }

        public DateTime UtcNow { get; set; }

        public MoveMessage MoveCommand { get; set; }
        public ErrorChatMessage Error { get; set; }


    }
    public sealed class MoveMessage
    {
	    public string FromFileId { get; set; }
	    public string FromFolderId { get; set; }
	    public string ToFolderId { get; set; }
	    public string ToFileId { get; set; }
	    public string StorageName { get; set; }
    }
    public sealed class ReadMessage
    {
        public string FileId { get; set; }
        public string FolderId { get; set; }

        public string StorageName { get; set; }

        public byte[] Content { get; set; }
        public long Count { get; set; }
        public long Size { get; set; }
        public string SessionId { get; set; }

        public bool IsExists { get; set; }
    }

    public sealed class WriteMessage
    {
        public string FileId { get; set; }
        public string FolderId { get; set; }

        public string StorageName { get; set; }
        public byte[] Content { get; set; }
        public string SessionId { get; set; }
        public bool IsClose { get; set; }

    }
    public sealed class ErrorChatMessage
    {
        public TcpMessageType Type => TcpMessageType.BadRequest;
        public string Error { get; set; }
    }  
}
