using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace FileStorage.Contracts
{
	[DataContract(Namespace = NamespaceVer001.Message)]
	public class FileInfoResponse
	{
		public FileInfoResponse(string id, string hash, long length,string path)
		{
			Path = path;
			Id = id;
			Hash = hash;
			Length = length;
		}

		[DataMember]
		public string Id { get; set; }

		[DataMember]
		public string Hash { get; set; }

		[DataMember]
		public long Length { get; set; }

		[DataMember]
		public string Path { get; set; }
	}

	
}
