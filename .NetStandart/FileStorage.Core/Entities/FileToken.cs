using System;

namespace FileStorage.Core.Entities
{
	public class FileToken
	{
		public FileToken(string storageId, string hash, long length)
		{
			StorageId = storageId;
			Hash = hash;
			Length = length;
		}
		public string StorageId { get; private set; }

		public string Hash { get; private set; }

		public long Length { get; private set; }
	}
}