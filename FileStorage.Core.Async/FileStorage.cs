using FileStorage.Core.Contracts;
using FileStorage.Core.Entities;
using FileStorage.Core.Helpers;
using FileStorage.Core.Interfaces;
using FileStorage.Core.Interfaces.Settings;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using File = System.IO.File;


namespace FileStorage.Core
{
	/// <summary>
	/// Hierarchical file storage stored on disk
	/// </summary>
	public sealed class FileStorage : IFileStorage<string>
	{
		private readonly string _name;
		private readonly IFileStorageSettings _settings;
		private readonly IFileStorageVirtual _fileStorageVirtual;
		private readonly ILocalLogger _localLogger;
		private IFileStorageLog _log;
		private readonly string _rootDirectory;
		/// <summary>
		/// Ctor
		/// </summary>
		/// <param name="name">Name of root directory</param>
		public FileStorage(string name)
			: this(name, new FileStorageSettings(), new FileStorageVirtual(), null)
		{ }

		public FileStorage(string name, IFileStorageSettings settings, IFileStorageVirtual fileStorageVirtual, ILocalLogger localLogger)
		{
			_name = name;
			_settings = settings;
			_fileStorageVirtual = fileStorageVirtual;
			_localLogger = localLogger;
			_rootDirectory = GetRoot(_settings, name);
			_log = new FileStorageLog(_rootDirectory);
			Task.Factory.StartNew(() => _fileStorageVirtual.GetCount(_rootDirectory));
		}

		public static string GetRoot(IFileStorageSettings settings, string name)
		{
			return Path.Combine(settings.RootDirectory, name) + Path.DirectorySeparatorChar;
		}

		/// <summary>
		///  <see cref="IFileStorage{TValue}.Save(FileContainer)"/>
		/// </summary>
		/// <param name="file"></param>
		/// <returns></returns>
		public async Task<string> SaveAsync(FileContainer file)
		{
			SaveAsync(file.Stream, file.Id);
			return file.Id;
		}

		async Task SaveStreamToFileAsync(Stream stream, string fileName)
		{

			var fileInfo = new FileInfo(fileName);
			if (fileInfo?.Directory != null)
			{
				var tempFileName = Path.Combine(fileInfo.Directory.FullName, Guid.NewGuid() + ".tmp");
				try
				{
					using (var outstream = File.Open(tempFileName, FileMode.Create, FileAccess.Write))
					{
						await stream.CopyToAsync(outstream);
						outstream.Close();
					}
					MoveFile(fileName, tempFileName, fileInfo);
				}
				catch
				{
					if (_fileStorageVirtual.Exists(tempFileName))
					{
						_fileStorageVirtual.Delete(tempFileName);
					}
					throw;
				}
			}
		}

		private void MoveFile(string fileNameTo, string filenameFrom, FileInfo fileInfo)
		{
			var tmpFileInfo = new FileInfo(filenameFrom);
			var length = tmpFileInfo.Length;
			if (!_fileStorageVirtual.Exists(fileNameTo))
			{
				_fileStorageVirtual.Move(filenameFrom, fileNameTo);
				_log.Write(EventType.FileSave, fileInfo.Name, length);
			}
			else
			{
				if (fileInfo.Exists && fileInfo.Length != length)
				{
					_fileStorageVirtual.Delete(fileInfo.FullName);
					_fileStorageVirtual.Move(filenameFrom, fileNameTo);
					_log.Write(EventType.FileSave, fileInfo.Name, length);
				}
				else if (_fileStorageVirtual.Exists(filenameFrom))
				{
					_fileStorageVirtual.Delete(filenameFrom);

				}
			}
		}

		/// <summary>
		/// <see cref="IFileStorage{TValue}.SaveToFile(SaveFileRequest)"/>
		/// </summary>
		/// <param name="request"></param> 
		public async Task SaveToFileAsync(SaveFileRequest request)
		{
			var fileName = GetFullFileName(request.Id, true);
			var fileInfo = new FileInfo(fileName);
			//if (fileInfo.Directory.Exists)
			{
				var tempFileName = GetTempFileName(fileName, request.SessionId);
				try
				{
					var bytes = request.Bytes;
					using (var outstream = File.Open(tempFileName, FileMode.Append))
					{
						await outstream.WriteAsync(bytes, (int)0, bytes.Length);
						outstream.Close();
					}
					if (request.Close)
					{
						MoveFile(fileName, tempFileName, fileInfo);
					}
				}
				catch
				{
					if (_fileStorageVirtual.Exists(tempFileName))
					{
						_fileStorageVirtual.Delete(tempFileName);
					}
					throw;
				}
			}
		}

		/// <summary>
		/// <see cref="IFileStorage{TValue}.ClearTtl(DateTime)"/>
		/// </summary>
		/// <param name="date"></param>
		public async Task ClearTtlAsync(DateTime date)
		{

			var files = _log.GetAll().OrderBy(x => x.Time).ToList();
			var save = new List<EventMessage>();
			foreach (var file in files)
			{
				if (file.Time > date)
				{
					//lock (save)
					{

						save.Add(file);
					}
				}
				Delete(file.Id);
			}
			_log.Rewrite(save, DateTime.UtcNow);
			var fs = Directory.GetFiles(_rootDirectory, "*", SearchOption.AllDirectories);
			foreach (var file in fs)
			{
				var fileInfo = new FileInfo(file);
				if (fileInfo.Extension == ".log")
					continue;
				if (fileInfo.CreationTimeUtc < date)
				{
					_fileStorageVirtual.Delete(file);
				}
			}
		}

		public async Task DropAsync()
		{
			_fileStorageVirtual.Drop();
			Directory.Delete(_rootDirectory, true);
			_log.Clear();
			_log = new FileStorageLog(_rootDirectory);


		}

		private static string GetTempFileName(string fileName, string sessionId = "")
		{
			return $"{fileName}_{sessionId ?? string.Empty}.tmp";
		}

		/// <summary>
		/// <see cref="IFileStorage{TValue}.Append(byte[], string)"/>
		/// </summary>
		/// <param name="array"></param>
		/// <param name="id"></param>
		public async Task AppendAsync(byte[] array, string id)
		{
			var fullName = GetFullFileName(id, true);

			using (var stream = File.Open(fullName, FileMode.Append, FileAccess.Write,
				FileShare.ReadWrite | FileShare.Delete | FileShare.Read | FileShare.Write))
			{
				await stream.WriteAsync(array, 0, array.Length);
				await stream.FlushAsync();
				stream.Close();
			}
			if (!_fileStorageVirtual.Exists(fullName))
			{
				_fileStorageVirtual.Add(fullName);
			}
		}


		/// <summary>
		/// <see cref="IFileStorage{TValue}.Save(byte[], string)"/>
		/// </summary>
		/// <param name="array"></param>
		/// <param name="id"></param>
		public async Task SaveAsync(byte[] array, string id)
		{
			using (var stream = new MemoryStream())
			{
				stream.WriteAsync(array, 0, array.Length);
				stream.Flush();
				stream.Position = 0;
				SaveAsync(stream, id);
			}
		}

		/// <summary>
		/// <see cref="IFileStorage{TValue}.Get(string)"/>
		/// </summary>
		/// <param name="id"></param>
		/// <returns></returns>
		public async Task<FileContainer> GetAsync(string id)
		{
			return new FileContainer
			{
				Id = id,
				Stream = await GetStreamAsync(id)
			};
		}

		/// <summary>
		/// <see cref="IFileStorage{TValue}.GetStream(string)"/>
		/// </summary>
		/// <param name="id"></param>
		/// <returns></returns>
		public Task<Stream> GetStreamAsync(string id)
		{
			return GetStreamAsync(id, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete | FileShare.Read | FileShare.Write);
		}
		public async Task<Stream> GetStreamAsync(string id, FileMode mode, FileAccess access, FileShare share)
		{
			var fileName = GetFullFileName(id);
			if (_fileStorageVirtual.Exists(fileName))
			{
				return new FileStream(fileName, mode, access,
					share);
			}

			return null;
		}
		/// <summary>
		/// <see cref="IFileStorage{TValue}.Delete(string)"/>
		/// </summary>
		/// <param name="id"></param>
		/// <returns></returns>
		public bool Delete(string id)
		{
			var directory = GetDirectory(id);
			var fileName = GetFullFileName(id);
			if (_fileStorageVirtual.Exists(fileName))
			{
				_fileStorageVirtual.Delete(fileName);
				if (!Directory.EnumerateFiles(directory).Any() && !Directory.EnumerateDirectories(directory).Any())
				{
					_fileStorageVirtual.DeleteDirectory(directory);
					//Directory.Delete(directory);
				}
				_log.Write(new EventMessage()
				{
					Type = EventType.FileDelete,
					Id = id,
					Time = DateTime.UtcNow
				});
				return true;
			}
			return false;
		}
		/// <summary>
		/// <see cref="IFileStorage{TValue}.DeleteTemp(string)"/>
		/// </summary>
		/// <param name="id"></param>
		/// <returns></returns>
		public bool DeleteTemp(string id)
		{
			var fileName = GetFullFileName(id);
			var fileNameTmp = GetTempFileName(id);
			if (_fileStorageVirtual.Exists(fileNameTmp))
			{
				_fileStorageVirtual.Delete(fileNameTmp);
				return true;
			}
			return false;
		}

		/// <summary>
		/// <see cref="IFileStorage{TValue}.Exists(string)"/>
		/// </summary>
		/// <param name="id"></param>
		/// <returns></returns>
		public bool Exists(string id)
		{
			var fileName = GetFullFileName(id);
			return _fileStorageVirtual.Exists(fileName);
		}

		private string GetDirectory(string id)
		{
			var depth = _settings.Depth;

			return GetDirectory(id, depth);
		}

		private string GetDirectory(string id, int depth)
		{
			if (id == null) throw new ArgumentNullException("id");
			var length = _settings.ElementsCount > id.Length ? id.Length : _settings.ElementsCount;
			var pathSb = new StringBuilder(_rootDirectory.Length + id.Length * 2);
			//var pathSb = new StringBuilder();
			pathSb.Append(_rootDirectory);

			if (depth > 1)
			{
				var array = new char[depth];
				for (int i = 0; i < depth; i++)
				{
					if (i < id.Length)
					{
						array[i] = id[i];
						//pathSb.Append($"{Path.DirectorySeparatorChar}{id[i]}");
						//path = Path.Combine(path, .ToString());
					}
					else
					{
						break;
					}
				}
				var length1 = length - depth;
				if (length1 > 0)
				{
					var directorySeparatorChar = Path.DirectorySeparatorChar.ToString();
					pathSb.Append(string.Join(directorySeparatorChar, array));
					pathSb.Append(directorySeparatorChar);
					pathSb.Append(id.Substring(depth, length1));
				}
			}
			else
			{
				pathSb.Append(id.Substring(depth, length));
			}


			//var directoryName = Path.Combine(_rootDirectory, pathSb.ToString());
			var directoryName = pathSb.ToString();
			return directoryName;
		}

		/// <summary>
		/// <see cref="IFileStorage{TValue}.ChangeDepthDirectory(int, int)"/>
		/// </summary>
		/// <param name="oldDepth"></param>
		/// <param name="newDepth"></param>
		public async Task ChangeDepthDirectoryAsync(int oldDepth, int newDepth)
		{
			var directories = Directory.GetDirectories(_rootDirectory, "*", SearchOption.TopDirectoryOnly);
			foreach (var diretory in directories)
			{
				var directoryInfo = new DirectoryInfo(diretory);
				if (directoryInfo.Name.Length > newDepth)
				{
					var files = directoryInfo.GetFiles();
					foreach (var file in files)
					{
						var newDirectoryName = GetDirectory(file.Name, newDepth);
						_fileStorageVirtual.AddDirectory(newDirectoryName, true);
						var destFileName = Path.Combine(newDirectoryName, file.Name);
						_fileStorageVirtual.Move(file.FullName, destFileName);

					}

				}
				directoryInfo.Delete(true);
			}
		}

		/// <summary>
		/// <see cref="IFileStorage{TValue}.GetCount()"/>
		/// </summary>
		/// <returns></returns>
		public long GetCount()
		{
			return _fileStorageVirtual.GetCount();
		}


		/// <summary>
		/// <see cref="IFileStorage{TValue}.GetIds(long, int)"/>
		/// </summary>
		/// <param name="offset"></param>
		/// <param name="count"></param>
		/// <returns></returns>
		public async Task<List<string>> GetIdsAsync(long offset, int count)
		{
			var result = new List<string>();

			var files = _fileStorageVirtual.GetFileNames((int)offset, count);
			foreach (var file in files)
			{
				var id = file.Split(Path.DirectorySeparatorChar).LastOrDefault();

				if (!_log.IsExists(id))
				{
					var fileInfo = new FileInfo(file);
					if (!fileInfo.Exists)
					{
						_fileStorageVirtual.Delete(file);
					}
					else
					{

						_log.Write(new EventMessage()
						{
							Type = EventType.FileSave,
							Size = fileInfo.Length,
							Id = id,
							Time = DateTime.UtcNow,
						});
					}
				}
				result.Add(id.ToUpper());

			}
			return result;
		}



		//private Dictionary<string, FileInfo> _files = new Dictionary<string, FileInfo>();
		//private SortedList<DateTime, List<string>> _fileIdsByDate = new SortedList<DateTime, List<string>>();
		private readonly object _sync = new object();
		//private DateTime _lastRefreshTime = DateTime.UtcNow.AddDays(-1);

		/// <summary>
		///  <see cref="IFileStorage{TValue}.RefreshLog()"/>
		/// </summary>
		public async Task RefreshLogAsync()
		{
			var directories = Directory.GetDirectories(_rootDirectory);
			RefreshSub(directories);

		}

		//public string GetLatestId()
		//{
		//	Refresh();
		//	return _fileIdsByDate.LastOrDefault().Value.LastOrDefault();
		//}

		/// <summary>
		///  <see cref="IFileStorage{TValue}.GetLog(DateTime, int)"/>
		/// </summary>
		/// <param name="time"></param>
		/// <param name="take"></param>
		/// <returns></returns>
		public List<EventMessage> GetLog(DateTime time, int take)
		{
			return _log.GetChangedIds(time, take);
		}

		/// <summary>
		/// <see cref="IFileStorage{TValue}.GetLogDateById(string)"/>
		/// </summary>
		/// <param name="id"></param>
		/// <returns></returns>
		public DateTime? GetLogDateById(string id)
		{
			return _log.GetDateById(id);
		}

		private void RefreshSub(string[] directories)
		{
			//foreach (var dir in directories)
			Parallel.ForEach(directories, dir =>
			{
				var d = Directory.GetDirectories(dir);
				if (!d.Any())
				{
					_fileStorageVirtual.AddDirectory(dir, false);
					var files = Directory.GetFiles(dir, "*", SearchOption.TopDirectoryOnly);
					Parallel.ForEach(files, f =>
					//foreach (var f in files)
					{
						var fileInfo = new FileInfo(f);
						AddFile(fileInfo);
						_fileStorageVirtual.Add(fileInfo.Name);
					}
					);
				}
				else
				{
					RefreshSub(d);
				}
			}
			);
		}

		private void AddFile(FileInfo fileInfo)
		{
			_log.Write(new EventMessage()
			{
				Type = EventType.FileSave,
				Id = fileInfo.Name,
				Time = fileInfo.CreationTimeUtc,
				Size = fileInfo.Length
			});
			//if (!_files.ContainsKey(fileInfo.Name))
			//{
			//	lock (_sync)
			//	{
			//		if (!_files.ContainsKey(fileInfo.Name))
			//		{
			//			_files.Add(fileInfo.Name, fileInfo);
			//			var value = new List<string>();
			//			if (!_fileIdsByDate.ContainsKey(fileInfo.CreationTimeUtc))
			//			{
			//				_fileIdsByDate.Add(fileInfo.CreationTimeUtc, value);
			//			}
			//			else
			//			{
			//				value = _fileIdsByDate[fileInfo.CreationTimeUtc];
			//			}
			//			value.Add(fileInfo.Name);



			//		}
			//	}
			//}
		}

		/// <summary>
		/// <see cref="IFileStorage{TValue}.GetFullFileName(string)"/>
		/// </summary>
		/// <param name="id"></param>
		/// <returns></returns>
		public string GetFullFileName(string id)
		{
			return GetFullFileName(id, false);
		}

		/// <summary>
		/// <see cref="IFileStorage{TValue}.Move(string, string)"/>
		/// </summary>
		/// <param name="fromId"></param>
		/// <param name="toId"></param>
		public async Task MoveAsync(string fromId, string toId)
		{
			if (Exists(fromId))
			{
				var fileNameTo = GetFullFileName(toId, true);
				var fileNameFrom = GetFullFileName(fromId);
				var fileInfo = new FileInfo(fileNameTo);
				var fileInfoFrom = new FileInfo(fileNameTo);


				if (!_fileStorageVirtual.Exists(fileNameTo))
				{

					_fileStorageVirtual.Move(fileNameFrom, fileNameTo);
				}
				else
				{
					_fileStorageVirtual.Delete(fileNameTo);
					_fileStorageVirtual.Move(fileNameFrom, fileNameTo);
				}
				_fileStorageVirtual.Delete(fileNameFrom);


				_log.Write(EventType.FileSave, fileInfo.Name, fileInfo.Length);
				_log.Write(EventType.FileDelete, fileInfoFrom.Name, fileInfoFrom.Length);
			}

		}
		/// <summary>
		/// <see cref="IFileStorage{TValue}.GetSize(string)"/>
		/// </summary>
		/// <param name="id"></param>
		/// <returns></returns>
		public long GetSize(string id)
		{
			var fullName = GetFullFileName(id, false);
			try
			{

				if (_fileStorageVirtual.Exists(fullName))
				{
					var fileInfo = new FileInfo(fullName);
					return fileInfo.Length;
				}
			}
			catch (FileNotFoundException e)
			{
				_localLogger?.Error($"{fullName};{e.Message}", e);
				_fileStorageVirtual.Release(fullName);
			}
			return 0;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="id"></param>
		/// <param name="createDirectory"></param>
		/// <returns></returns>
		public string GetFullFileName(string id, bool createDirectory)
		{
			var directory = GetDirectory(id);
			_fileStorageVirtual.AddDirectory(directory, createDirectory);

			//var fileNameTo = Path.Combine(directory, id);
			var fileName = directory + Path.DirectorySeparatorChar + id;
			return fileName;
		}

		/// <summary>
		/// <see cref="IFileStorage{TValue}.Save(Stream, string)"/>
		/// </summary>
		/// <param name="stream"></param>
		/// <param name="id"></param>
		public async Task SaveAsync(Stream stream, string id)
		{
			var fullName = GetFullFileName(id, true);
			SaveStreamToFileAsync(stream, fullName);
		}

		public string GetHash(Stream stream)
		{
			var hash = string.Empty;
			using (var output = new MemoryStream())
			{
				using (var sha1 = SHA1.Create())
				{
					using (var crypto = new CryptoStream(output, sha1, CryptoStreamMode.Write))
					{
						stream.CopyTo(crypto);
						crypto.FlushFinalBlock();
						hash = sha1.Hash.GetHashString();
					}
				}
			}
			if (stream.CanRead)
			{
				stream.Position = 0;
			}
			return hash;
		}
		/// <summary>
		/// <see cref="IFileStorage{TValue}.SaveAsHash(Stream)"/>
		/// </summary>
		/// <param name="input"></param>
		/// <returns></returns>
		public async Task<FileToken> SaveAsHashAsync(Stream input)
		{
			var id = GetHash(input);
			return await SaveStreamAsync(input, id);
		}

		/// <summary>
		/// <see cref="IFileStorage{TValue}.SaveStream(Stream, string)"/>
		/// </summary>
		/// <param name="input"></param>
		/// <param name="id"></param>
		/// <returns></returns>
		public async Task<FileToken> SaveStreamAsync(Stream input, string id)
		{
			await SaveAsync(input, id);
			return new FileToken(id, id, 0);
		}

		/// <summary>
		/// <see cref="IFileStorage{TValue}.DeleteAll()"/>
		/// </summary>
		public async Task DeleteAllAsync()
		{
			Directory.Delete(_rootDirectory);
		}

		/// <summary>
		///  <see cref="IFileStorage{TValue}.GetRelativeFileName(string)"/>
		/// </summary>
		/// <param name="id"></param>
		/// <returns></returns>
		public string GetRelativeFileName(string id)
		{
			var directory = GetDirectory(id);
			var directoryInfo = new DirectoryInfo(directory);
			if (directoryInfo.Exists)
			{
				return Path.Combine(directoryInfo.Name, id);
			}

			return null;
		}
	}
}
