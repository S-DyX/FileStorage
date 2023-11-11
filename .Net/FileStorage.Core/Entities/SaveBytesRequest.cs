using System;
using System.Collections.Generic;
using System.Text;

namespace FileStorage.Core.Entities
{
	public sealed class SaveBytesRequest
	{
		public string Id { get; set; }
		public byte[] Bytes { get; set; }
		public bool Close { get; set; }
	}
}
