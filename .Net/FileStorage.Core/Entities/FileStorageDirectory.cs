using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FileStorage.Core.Entities
{
	public sealed class FileStorageDirectory
	{
		public FileStorageDirectory(string directory, FileStorageDirectory parent)
		{
			FullName = directory;
			Parent = parent;
			_children = new List<FileStorageDirectory>(21);
		}

		public string FullName { get; private set; }

		public FileStorageDirectory Parent { get; private set; }

		public IEnumerable<FileStorageDirectory> Children => _children;

		private readonly List<FileStorageDirectory> _children;

		public void AddChildren(FileStorageDirectory child)
		{
			_children.Add(child);
		}
	}
}
