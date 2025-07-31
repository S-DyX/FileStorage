using System;
using System.Collections.Generic;
using System.Text;

namespace FileStorage.Core.Contracts
{
    public sealed class SaveFileRequest
    {
        public byte[] Bytes;
        public string Id;
        public string FolderId;
        public bool Close;
        public string SessionId;
    }
}
