using System;
using System.Collections.Generic;
using System.Text;

namespace FileStorage.Contracts.Impl.Connections
{
    public interface IMessageProcessingService
    {
        ProcessingResponse Process(byte[] bytes, IWebSocketContainer from);
    }

    public enum ResponseStatus
    {
        Unknown,
        Ok = 200,
        InternalServerError = 500,
        Handshaking = 200,
        BadRequest = 400,
        NotFound = 404


    }
    public sealed class ProcessingResponse
    {
        public string StorageName { get; set; } 
        public string Id { get; set; }
        public byte[] Bytes { get; set; }
        public ResponseStatus Status { get; set; }

        public TcpMessageType Type { get; set; }

        public ClientInfo ClientInfo { get; set; }
        public TimeSpan? DiffUtcNow { get; set; } 
    }
}
