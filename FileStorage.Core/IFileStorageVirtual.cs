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
		// Все известные файлы: служит и проверкой «файл существует», и источником для GetCount/GetFileNames.
		// Раньше в Core каждый файл дополнительно лежал в MemoryCache (сотни байт на запись), а все каталоги —
		// в отдельном кеше, который ни для чего не использовался.
		private readonly IndexedFileList _files;
		// корни хранилищ, уже обойдённые при подсчёте файлов (GetCount(rootDirectory))
		private readonly HashSet<string> _countedDirectories = new HashSet<string>();
		private readonly object _syncDirectory = new object();
		private readonly object _sync = new object();
		private static readonly int MaxIoParallelism = Math.Max(2, Math.Min(Environment.ProcessorCount, 8));

		/// <summary>
		/// Ctor
		/// </summary>
		public FileStorageVirtual(int capacity = 20000)
		{
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
			lock (_sync)
			{
				if (_files.Contains(key))
					return true;
			}

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
				_files.Add(GetKey(fileName));
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
				_files.Remove(GetKey(fileName));
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
				_files.Remove(key);
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
			// Каталоги больше не запоминаются: этот кеш никто не читал, а запись в него шла
			// при каждом обращении к файлу. CreateDirectory ничего не делает, если каталог уже есть.
			if (createDirectory)
			{
				Directory.CreateDirectory(directory);
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
					if (!Directory.EnumerateFileSystemEntries(directory).Any())
					{
						Directory.Delete(directory);
					}
				}
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
			if (!counted && Directory.Exists(rootDirectory))
			{
				CountFiles(Directory.GetDirectories(rootDirectory));
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
				_countedDirectories.Clear();
			}
			lock (_sync)
			{
				_files.Clear();
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
				_files.RemoveAll(f => f.StartsWith(directory, StringComparison.Ordinal));
			}
			lock (_syncDirectory)
			{
				_countedDirectories.RemoveWhere(d => d.StartsWith(directory, StringComparison.Ordinal));
			}
		}

		private void CountFiles(string[] directories)
		{
			// Параллельно обходим только каталоги верхнего уровня, а внутри — последовательно.
			// Раньше вложенные Parallel.ForEach с рекурсией перегружали пул потоков дисковыми операциями.
			// Повторно добавленный файл не дублируется: список хранит каждый путь один раз.
			Parallel.ForEach(directories, new ParallelOptions { MaxDegreeOfParallelism = MaxIoParallelism }, dir => CountDirectory(dir));
		}

		private void CountDirectory(string dir)
		{
			var subDirectories = Directory.GetDirectories(dir);
			if (subDirectories.Length > 0)
			{
				foreach (var subDirectory in subDirectories)
				{
					CountDirectory(subDirectory);
				}
				return;
			}

			foreach (var fileName in Directory.EnumerateFiles(dir))
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
			}
		}
	}
}
