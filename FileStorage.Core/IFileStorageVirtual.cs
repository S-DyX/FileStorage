using FileStorage.Core.Interfaces;
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

		/// <summary>
		/// Убрать из кеша все файлы и каталоги, путь которых начинается с directory (файлы на диске не трогаются)
		/// </summary>
		/// <param name="directory"></param>
		void ReleaseDirectory(string directory);

	}

	/// <summary>
	/// Список файлов с добавлением, проверкой и удалением за O(1).
	/// При удалении на место удалённого элемента ставится последний, поэтому порядок элементов не сохраняется.
	/// </summary>
	internal sealed class IndexedFileList
	{
		private readonly List<string> _items;
		private readonly Dictionary<string, int> _index;

		public IndexedFileList(int capacity)
		{
			_items = new List<string>(capacity);
			_index = new Dictionary<string, int>(capacity);
		}

		public int Count
		{
			get { return _items.Count; }
		}

		public bool Contains(string item)
		{
			return _index.ContainsKey(item);
		}

		public bool Add(string item)
		{
			if (_index.ContainsKey(item))
				return false;
			_index.Add(item, _items.Count);
			_items.Add(item);
			return true;
		}

		public bool Remove(string item)
		{
			int position;
			if (!_index.TryGetValue(item, out position))
				return false;
			var lastPosition = _items.Count - 1;
			var last = _items[lastPosition];
			_items[position] = last;
			_index[last] = position;
			_items.RemoveAt(lastPosition);
			_index.Remove(item);
			return true;
		}

		public List<string> RemoveAll(Func<string, bool> predicate)
		{
			var removed = _items.Where(predicate).ToList();
			if (removed.Count == 0)
				return removed;
			_items.RemoveAll(x => predicate(x));
			_index.Clear();
			for (var i = 0; i < _items.Count; i++)
			{
				_index.Add(_items[i], i);
			}
			return removed;
		}

		public List<string> GetRange(int offset, int count)
		{
			if (offset < 0)
				offset = 0;
			if (count <= 0 || offset >= _items.Count)
				return new List<string>();
			return _items.GetRange(offset, Math.Min(count, _items.Count - offset));
		}

		public void Clear()
		{
			_items.Clear();
			_index.Clear();
		}
	}

	/// <summary>
	/// <see cref="IFileStorageVirtual"/>
	/// </summary>
	public sealed class FileStorageVirtual : IFileStorageVirtual
	{
		// кеш «файл существует»: истекает через сутки, после чего наличие файла перепроверяется на диске
		private readonly Cache<string, string> _dictFiles;
		// все известные файлы (GetCount/GetFileNames), без срока жизни
		private readonly IndexedFileList _files;
		private readonly Cache<string, string> _dictDirectories;
		// каталоги, уже обойдённые при подсчёте файлов (GetCount(rootDirectory))
		private readonly HashSet<string> _countedDirectories = new HashSet<string>();
		private readonly object _syncDirectory = new object();
		private readonly object _sync = new object();

		/// <summary>
		/// Ctor
		/// </summary>
		public FileStorageVirtual(int capacity = 20000)
		{
			_dictFiles = new Cache<string, string>(60 * 1440);
			_dictDirectories = new Cache<string, string>(60 * 1440);
			_files = new IndexedFileList(capacity);
		}

		/// <summary>
		/// <see cref="IFileStorageVirtual.Exists(string)"/>
		/// </summary>
		/// <param name="fileName"></param>
		/// <returns></returns>
		public bool Exists(string fileName)
		{
			var key = GetKey(fileName);
			if (_dictFiles.IsExists(key))
				return true;

			var exists = File.Exists(key) || (key != fileName && File.Exists(fileName));
			if (exists)
				Add(key);
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
				var key = GetKey(fileName);
				_files.Add(key);
				_dictFiles.SetValue(key, key);
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
				var key = GetKey(fileName);
				_files.Remove(key);
				_dictFiles.Delete(key);
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
				// удаление из списка за O(1); раньше List.Remove проходил весь список
				_files.Remove(key);
				_dictFiles.Delete(key);
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
				return _files.GetRange(offset, count);
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
				_dictDirectories[directory] = directory;
			}
		}
		/// <summary>
		/// <see cref="IFileStorageVirtual.DeleteDirectory(string)"/>
		/// </summary>
		/// <param name="directory"></param>
		public void DeleteDirectory(string directory)
		{
			lock (_syncDirectory)
			{
				if (Directory.Exists(directory))
				{
					foreach (var file in Directory.GetFiles(directory))
					{
						Delete(file);
					}
					// Подкаталоги не трогаем: каталог удаляем, только если он опустел.
					// Раньше Directory.Delete без recursive падал на каталоге с подкаталогами.
					if (!Directory.EnumerateFileSystemEntries(directory).Any())
					{
						Directory.Delete(directory);
					}
				}
				_dictDirectories.Delete(directory);
				_countedDirectories.Remove(directory);
			}
		}

		/// <summary>
		/// <see cref="IFileStorageVirtual.GetCount(string)"/>
		/// </summary>
		/// <param name="rootDirectory"></param>
		/// <returns></returns>
		public long GetCount(string rootDirectory)
		{
			bool counted;
			lock (_syncDirectory)
			{
				counted = _countedDirectories.Contains(rootDirectory);
			}
			// раньше для уже известного корня возвращался 0
			if (!counted && Directory.Exists(rootDirectory))
			{
				GetSubDirectoryCount(Directory.GetDirectories(rootDirectory));
				lock (_syncDirectory)
				{
					_countedDirectories.Add(rootDirectory);
				}
			}
			return GetCount();
		}

		public void Drop()
		{
			// блокировки берём по очереди, а не вложенно: DeleteDirectory берёт их в обратном порядке
			lock (_syncDirectory)
			{
				_dictDirectories.Clear();
				_countedDirectories.Clear();
			}
			lock (_sync)
			{
				_files.Clear();
				_dictFiles.Clear();
			}
		}

		/// <summary>
		/// <see cref="IFileStorageVirtual.ReleaseDirectory(string)"/>
		/// </summary>
		/// <param name="directory"></param>
		public void ReleaseDirectory(string directory)
		{
			lock (_sync)
			{
				var released = _files.RemoveAll(f => f.StartsWith(directory, StringComparison.Ordinal));
				foreach (var file in released)
				{
					_dictFiles.Delete(file);
				}
			}
			lock (_syncDirectory)
			{
				_countedDirectories.RemoveWhere(d => d.StartsWith(directory, StringComparison.Ordinal));
			}
			// записи кеша каталогов (MemoryCache) перебрать нельзя; лишняя запись безвредна:
			// AddDirectory всё равно проверяет наличие каталога на диске
		}

		private void GetSubDirectoryCount(string[] directories)
		{
			Parallel.ForEach(directories, dir =>
			{
				// Отмечаем каталоги, уже обойдённые подсчётом. Раньше проверялся общий кеш каталогов,
				// куда каталог попадает и при обычной работе, и такие каталоги не подсчитывались.
				lock (_syncDirectory)
				{
					if (!_countedDirectories.Add(dir))
						return;
				}
				var d = Directory.GetDirectories(dir);
				if (!d.Any())
				{
					AddDirectory(dir, false);
					var files = Directory.GetFiles(dir);
					Parallel.ForEach(files, fileName =>
					{
						if (fileName.EndsWith(".tmp"))
						{
							Delete(fileName);
						}
						else if (fileName.EndsWith(".log") || fileName.EndsWith(".old"))
						{
							// файлы журнала и его архивы не являются файлами хранилища
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
