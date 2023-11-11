using System;
using System.IO;

namespace FileStorage.Core
{
	/// <summary>
	/// contains file details 
	/// </summary>
	public sealed class FileContainer
	{
		/// <summary>
		/// Ctor
		/// </summary>
		public FileContainer()
		{
			Id = Guid.NewGuid().ToString();
		}
		/// <summary>
		/// Can be Md5, sha1 or Guid
		/// </summary>
		public string Id { get; set; }

        /// <summary>
        /// Can be Md5, sha1 or Guid
        /// </summary>
        public string FolderId { get; set; }

		public string Hash { get; set; }
		
        /// <summary>
		/// File name
		/// </summary>
		public string Name { get; set; }

		/// <summary>
		/// File stream
		/// </summary>
		public Stream Stream { get; set; }
	}
}
