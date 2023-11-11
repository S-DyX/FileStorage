using System;
using System.Collections.Generic;
using System.Text;

namespace FileStorage.Contracts.Rest.Impl.FileStorage
{
	internal class GetBytesResult
	{
		public int Offset { get; set; }
		public int Size { get; set; }
		public byte[] Bytes { get; set; }

	}
}
