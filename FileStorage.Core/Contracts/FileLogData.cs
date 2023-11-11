using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace FileStorage.Contracts
{
	[DataContract(Namespace = NamespaceVer001.Message)]
	public class FileData
	{
		[DataMember]
		public string Id { get; set; }

		[DataMember]
		public DateTime Time  { get; set; }
		[DataMember]
		public long Size{ get; set; }
	}

	[DataContract(Namespace = NamespaceVer001.Message)]
	public class FileLogData
	{
		[DataMember]
		public List<FileData> Files{ get; set; }

		[DataMember]
		public List<string> DeletedIds { get; set; }

		[DataMember]
		public DateTime MaxDate { get; set; }
	}
}
