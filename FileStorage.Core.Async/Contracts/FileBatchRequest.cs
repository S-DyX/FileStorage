using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace FileStorage.Contracts
{
	[DataContract(Namespace = NamespaceVer001.Message)]
	public class FileBatchRequest
	{
		[DataMember]
		public string Id { get; set; }

        [DataMember]
        public string FolderId { get; set; }

		[DataMember]
		public string Name { get; set; }

		
		[DataMember]
		public byte[] Bytes { get; set; }

		[DataMember]
		public bool Close { get; set; }

		[DataMember]
		public string StorageName { get; set; }


		[DataMember]
		public string SessionId { get; set; }
	}
}
