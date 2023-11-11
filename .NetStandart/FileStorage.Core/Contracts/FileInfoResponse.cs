using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace FileStorage.Contracts
{
	public class FileInfoResponse
	{
		public FileInfoResponse(string id, string hash, long length,string path)
		{
			Path = path;
			Id = id;
			Hash = hash;
			Length = length;
		}

		public string Id { get; set; }

		public string Hash { get; set; }

		public long Length { get; set; }

		public string Path { get; set; }
	}

	
}
