using System;
using System.Collections.Generic;
using System.Text;

namespace FileStorage.Contracts.Rest.Impl.FileStorage
{
	internal sealed class SaveBytesRequest
	{
		public string Id { get; set; }
		public byte[] Bytes { get; set; }
		public bool Close { get; set; }
		public string SessionId { get; set; }

        public string FolderId { get; set; } 
	}
}
