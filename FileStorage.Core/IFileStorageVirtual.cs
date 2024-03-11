using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks; 

namespace FileStorage.Core
{
	/// <summary>
	/// Virtual file storage directory,reduces the number of disk accesses
	/// </summary>
	public interface IFileStorageVirtual
	{
		/// <summary>
		/// if file not found
		/// </summary>
		/// <param name="fileName"></param>
		void Release(string fileName);
		/// <summary>
		/// Is file exists
		/// </summary>
		/// <param name="fileName"></param>
		/// <returns></returns>
		bool Exists(string fileName);

		/// <summary>
		/// Move file from->to
		/// </summary>
		/// <param name="from"></param>
		/// <param name="to"></param>
		void Move(string from, string to);

		/// <summary>
		/// Delete file
		/// </summary>
		/// <param name="fileName"></param>
		void Delete(string fileName);

		/// <summary>
		/// Add new file
		/// </summary>
		/// <param name="fileName"></param>
		void Add(string fileName);

		/// <summary>
		/// return count of files
		/// </summary>
		/// <returns></returns>
		long GetCount();

		/// <summary>
		/// Get files by offset 
		/// </summary>
		/// <param name="offset"></param>
		/// <param name="count"></param>
		/// <returns></returns>
		IEnumerable<string> GetFileNames(int offset, int count);
		/// <summary>
		/// Add directory
		/// </summary>
		/// <param name="directory"></param>
		/// <param name="createDirectory"></param>
		void AddDirectory(string directory, bool createDirectory);
		/// <summary>
		/// Delete directory
		/// </summary>
		/// <param name="directory"></param>
		void DeleteDirectory(string directory);
		/// <summary>
		/// Count 
		/// </summary>
		/// <param name="rootDirectory"></param>
		/// <returns></returns>
		long GetCount(string rootDirectory);
		
		/// <summary>
		/// Drop files
		/// </summary>
		void Drop();

	}

	/// <summary>
	/// <see cref="IFileStorageVirtual"/>
	/// </summary>
	public sealed class FileStorageVirtual : IFileStorageVirtual
	{
		private readonly Dictionary<string, bool> _dictFiles;
		private readonly List<string> _files;
		private readonly Dictionary<string, bool> _dictDirectories;
		private readonly object _syncDirectory = new object();
		private readonly object _sync = new object();

		/// <summary>
		/// Ctor
		/// </summary>
		public FileStorageVirtual(int capacity = 20000)
		{
			_dictFiles = new Dictionary<string, bool>(capacity);
			_dictDirectories = new Dictionary<string, bool>(capacity);
			_files = new List<string>(capacity);

		}

		/// <summary>
		/// <see cref="IFileStorageVirtual.Exists(string)"/>
		/// </summary>
		/// <param name="fileName"></param>
		/// <returns></returns>
		public bool Exists(string fileName)
		{
			var lower = GetKey(fileName);
			if (_dictFiles.ContainsKey(lower))
				return _dictFiles[lower];

			var exists = File.Exists(lower) || File.Exists(fileName);
			if (exists)
				Add(lower);
			return exists;
		}
		/// <summary>
		/// <see cref="IFileStorageVirtual.Move(string, string)"/>
		/// </summary>
		/// <param name="from">file name</param>
		/// <param name="to">file name</param>
		public void Move(string @from, string to)
		{
			File.Move(@from, to, true);
			Add(to);
			Delete(@from);
		}

		/// <summary>
		/// <see cref="IFileStorageVirtual.Add(string)"/>
		/// </summary>
		/// <param name="fileName"></param>
		public void Add(string fileName)
		{
			lock (_sync)
			{
				var lower = GetKey(fileName);
				if (!_dictFiles.ContainsKey(lower))
				{
					_files.Add(lower);
					_dictFiles.Add(lower, true);
				}
			}
		}
		/// <summary>
		/// <see cref="IFileStorageVirtual.Release(string)"/>
		/// </summary>
		/// <param name="fileName"></param>
		public void Release(string fileName)
		{
			lock (_sync)
			{
				var lower = GetKey(fileName);
				if (_dictFiles.ContainsKey(lower))
				{
					_files.Remove(lower);
					_dictFiles.Remove(lower);
				}
			}
		}

		/// <summary>
		/// <see cref="IFileStorageVirtual.Delete(string)"/>
		/// </summary>
		/// <param name="fileName"></param>
		public void Delete(string fileName)
		{

			lock (_sync)
			{
				var key = GetKey(fileName);
				if (File.Exists(key))
					File.Delete(key);
				else if (File.Exists(fileName))
					File.Delete(fileName);
				if (_dictFiles.ContainsKey(key))
					_dictFiles.Remove(key);
			}

		}

		private static string GetKey(string fileName)
		{
			//return fileName.ToLower();
			return fileName;
		}

		/// <summary>
		/// <see cref="IFileStorageVirtual.GetCount()"/>
		/// </summary>
		/// <returns></returns>
		public long GetCount()
		{
			lock (_sync)
			{
				return _files.Count;
			}
		}

		/// <summary>
		///  <see cref="IFileStorageVirtual.GetFileNames(int, int)"/>
		/// </summary>
		/// <param name="offset"></param>
		/// <param name="count"></param>
		/// <returns></returns>
		public IEnumerable<string> GetFileNames(int offset, int count)
		{
			lock (_sync)
			{
				return _files.Skip(offset).Take(count).ToList();
			}

		}

		/// <summary>
		/// <see cref="IFileStorageVirtual.AddDirectory(string, bool)"/>
		/// </summary>
		/// <param name="directory"></param>
		/// <param name="createDirectory"></param>
		public void AddDirectory(string directory, bool createDirectory)
		{
			if (createDirectory && !Directory.Exists(directory))
			{
				Directory.CreateDirectory(directory);
			}
			lock (_syncDirectory)
			{
				_dictDirectories[directory] = true;
			}
		}
		/// <summary>
		/// <see cref="IFileStorageVirtual.DeleteDirectory(string)"/>
		/// </summary>
		/// <param name="directory"></param>
		public void DeleteDirectory(string directory)
		{
			if (_dictDirectories.ContainsKey(directory))
			{
				lock (_syncDirectory)
				{
					if (Directory.Exists(directory))
					{
						var files = Directory.GetFiles(directory);
						foreach (var file in files)
						{
							Delete(file);
						}
						Directory.Delete(directory);
					}

					if (_dictFiles.ContainsKey(directory))
						_dictFiles.Remove(directory);
				}
			}
		}

		/// <summary>
		/// <see cref="IFileStorageVirtual.GetCount(string)"/>
		/// </summary>
		/// <param name="rootDirectory"></param>
		/// <returns></returns>
		public long GetCount(string rootDirectory)
		{
			if (_dictDirectories.ContainsKey(rootDirectory))
			{
				var directories = Directory.GetDirectories(rootDirectory);
				GetSubDirectoryCount(directories);
				return GetCount();
			}
			return 0;
		}

		public void Drop()
		{
			lock (_sync)
			{
				_dictDirectories.Clear();
				_files.Clear();
				_dictFiles.Clear();
			}
		}

		private void GetSubDirectoryCount(string[] directories)
		{

			Parallel.ForEach(directories, dir =>
			{
				lock (_syncDirectory)
				{
					if (_dictDirectories.ContainsKey(dir))
						return;

					_dictDirectories[dir] = true;
				}
				var d = Directory.GetDirectories(dir);
				if (!d.Any())
				{
					var files = Directory.GetFiles(dir);
					Parallel.ForEach(files, fileName =>
					{
						if (fileName.EndsWith(".tmp"))
						{
							Delete(fileName);
						}
						else if (fileName.EndsWith(".log"))
						{
						}
						else
						{
							Add(fileName);
						}

					});
				}
				else
				{
					GetSubDirectoryCount(d);
				}
			});

		}
	}


}

