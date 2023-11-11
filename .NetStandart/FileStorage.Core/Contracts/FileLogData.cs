using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace FileStorage.Contracts
{ 
	public class FileData
	{
		public string Id { get; set; }

		public DateTime Time  { get; set; }
		
		public long Size{ get; set; }
	}

	public class FileLogData
	{
		public List<FileData> Files{ get; set; }

		public List<string> DeletedIds { get; set; }

		public DateTime MaxDate { get; set; }
	}
}
