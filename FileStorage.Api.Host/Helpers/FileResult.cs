using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace FileStorage.Api.Host.Helpers
{
	public sealed class FileResult
	{
		public Stream Stream { get; set; }
		public FileDataModel File { get; set; }
	}
	public sealed class FileDataModel
	{
		public long Id { get; set; }
		public string Name { get; set; } 
		public string MimeType { get; set; }
		public long Size { get; set; } 
	}
}
