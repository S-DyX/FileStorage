using FileStorage.Core.Contracts;
using FileStorage.Core.Entities;
using FileStorage.Core.Helpers;
using FileStorage.Core.Interfaces;
using FileStorage.Core.Interfaces.Settings;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using File = System.IO.File;


namespace FileStorage.Core
{
	/// <summary>
	/// Hierarchical file storage stored on disk
	/// </summary>
	public sealed class FolderStorage : IFolderStorage
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
		public FolderStorage(string name)
			: this(name, new FileStorageSettings(), new FileStorageVirtual(), null)
		{ }

		public FolderStorage(string name, IFileStorageSettings settings, IFileStorageVirtual fileStorageVirtual, ILocalLogger localLogger)
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
		public string Save(FileContainer file)
		{
			var info = new FolderStorageInfo() { FileId = file.Id, FolderId = file.Id };
			Save(file.Stream, info);
			return file.Id;
		}

		void SaveStreamToFile(Stream stream, FolderStorageInfo info)
		{
			var fileName = GetFullFileName(info, true);
			var fileInfo = new FileInfo(fileName);
			if (fileInfo?.Directory != null)
			{
				var tempFileName = Path.Combine(fileInfo.Directory.FullName, Guid.NewGuid() + ".tmp");
				try
				{
					using (var outstream = File.Open(tempFileName, FileMode.Create, FileAccess.Write))
					{
						StreamHelper.CopyStream(stream, outstream);
						outstream.Close();
					}
					MoveFile(fileName, tempFileName, fileInfo, info);
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

		private void MoveFile(string fileNameTo, string filenameFrom, FileInfo fileInfo, FolderStorageInfo info)
		{
			var tmpFileInfo = new FileInfo(filenameFrom);
			var length = tmpFileInfo.Length;
			if (!_fileStorageVirtual.Exists(fileNameTo))
			{
				_fileStorageVirtual.Move(filenameFrom, fileNameTo);
				_log.Write(EventType.FileSave, fileInfo.Name, length, info.FolderId);
			}
			else
			{
				if (fileInfo.Length != length)
				{
					_fileStorageVirtual.Delete(fileInfo.FullName);
					_fileStorageVirtual.Move(filenameFrom, fileNameTo);
					_log.Write(EventType.FileSave, fileInfo.Name, length, info.FolderId);
				}
				else if (_fileStorageVirtual.Exists(filenameFrom))
				{
					_fileStorageVirtual.Delete(filenameFrom);

				}
			}
		}


		/// <summary>
		/// <see cref="IFolderStorage.SaveToFile(SaveFileRequest)"/>
		/// </summary>
		/// <param name="request"></param> 
		public void SaveToFile(SaveFileRequest request)
		{
			var info = new FolderStorageInfo()
			{
				FileId = request.Id,
				FolderId = request.FolderId
			};
			var fileName = GetFullFileName(info, true);
			var fileInfo = new FileInfo(fileName);
			//if (fileInfo.Directory.Exists)
			{
				var tempFileName = GetTempFileName(fileName, request.SessionId);
				try
				{
					var bytes = request.Bytes;

					using (var outstream = File.Open(tempFileName, FileMode.Append))
					{
						outstream.Write(bytes, (int)0, bytes.Length);
						outstream.Close();
					}
					if (request.Close)
					{
						MoveFile(fileName, tempFileName, fileInfo, info);
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
		public void ClearTtl(DateTime date)
		{

			; _localLogger?.Info($"TTL Start:{date}");
			var files = _log.GetChangesToIds(date).OrderBy(x => x.Time).ToList();
			var deleted = files.Where(x => x.Type == EventType.FileDelete).ToList();
			foreach (var file in files)
			{
				switch (file.Type)
				{
					case EventType.FileDelete:
					case EventType.Unknown:
						continue;
						break;
				}

				if (deleted.FirstOrDefault(x => x.Id == file.Id) != null)
				{
					continue;
				}

				if (string.IsNullOrEmpty(file.FolderId))
				{
					var fileFound = Directory.GetFiles(_rootDirectory, file.Id, SearchOption.AllDirectories)
						.FirstOrDefault();

					if (fileFound != null)
					{
						var path = fileFound.Replace(_rootDirectory, "").Replace(file.Id, "")
							.Replace(Path.DirectorySeparatorChar.ToString(), "");
						file.FolderId = path;
					}
				}

				if (string.IsNullOrEmpty(file.FolderId))
				{
					_localLogger?.Info($"File not found:{file.Id}");
					continue;
				}

				Delete(new FolderStorageInfo()
				{
					FileId = file.Id,
					FolderId = file.FolderId,
					StorageName = _name
				});
				_localLogger?.Info("TTL:" + file.Id);
			}
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

		public void Drop()
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

		public Stream GetWriteStream(FolderStorageInfo info)
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// <see cref="IFolderStorage.Append(byte[], FolderStorageInfo)"/>
		/// </summary>
		/// <param name="array"></param>
		/// <param name="id"></param>
		public void Append(byte[] array, FolderStorageInfo info)
		{
			var fullName = GetFullFileName(info, true);

			using (var stream = File.Open(fullName, FileMode.Append, FileAccess.Write,
				FileShare.ReadWrite | FileShare.Delete | FileShare.Read | FileShare.Write))
			{
				stream.Write(array, 0, array.Length);
				stream.Flush(true);
				stream.Close();
			}
			if (!_fileStorageVirtual.Exists(fullName))
			{
				_fileStorageVirtual.Add(fullName);
			}
		}


		/// <summary>
		/// <see cref="IFolderStorage.Save(byte[], FolderStorageInfo)"/>
		/// </summary>
		/// <param name="array"></param>
		/// <param name="id"></param>
		public void Save(byte[] array, FolderStorageInfo info)
		{
			using (var stream = new MemoryStream())
			{
				stream.Write(array, 0, array.Length);
				stream.Flush();
				stream.Position = 0;
				Save(stream, info);
			}
		}

		/// <summary>
		/// <see cref="IFolderStorage.Get(string)"/>
		/// </summary>
		/// <param name="id"></param>
		/// <returns></returns>
		public FileContainer Get(FolderStorageInfo info)
		{
			return new FileContainer
			{
				Id = info.FileId,
				Stream = GetStream(info)
			};
		}

		/// <summary>
		/// <see cref="IFolderStorage.GetStream(FolderStorageInfo)"/>
		/// </summary>
		/// <param name="info"></param>
		/// <returns></returns>
		public Stream GetStream(FolderStorageInfo info)
		{
			return GetStream(info, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete | FileShare.Read | FileShare.Write);
		}

		public Stream GetStream(FolderStorageInfo info, FileMode mode, FileAccess access, FileShare share)
		{
			var fileName = GetFullFileName(info);
			if (_fileStorageVirtual.Exists(fileName))
			{
				return new FileStream(fileName, mode, access,
					share);
			}

			return null;
		}

		/// <summary>
		/// <see cref="IFolderStorage.Delete(string)"/>
		/// </summary>
		/// <param name="info"></param>
		/// <returns></returns>
		public bool Delete(FolderStorageInfo info)
		{
			var directory = GetDirectory(info);
			if (string.IsNullOrWhiteSpace(info.FileId))
			{
				_fileStorageVirtual.DeleteDirectory(directory);
				return true;
			}
			else
			{
				var fileName = GetFullFileName(info);
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
						Id = info.FileId,
						Time = DateTime.UtcNow
					});
					return true;
				}
			}

			return false;
		}
		/// <summary>
		/// <see cref="IFolderStorage.DeleteTemp(FolderStorageInfo)"/>
		/// </summary>
		/// <param name="info"></param>
		/// <returns></returns>
		public bool DeleteTemp(FolderStorageInfo info)
		{
			var fileName = GetFullFileName(info);
			var fileNameTmp = GetTempFileName(fileName);
			if (_fileStorageVirtual.Exists(fileNameTmp))
			{
				_fileStorageVirtual.Delete(fileNameTmp);
				return true;
			}
			return false;
		}

		/// <summary>
		/// <see cref="IFolderStorage.Exists(FolderStorageInfo)"/>
		/// </summary>
		/// <param name="info"></param>
		/// <returns></returns>
		public bool Exists(FolderStorageInfo info)
		{
			var fileName = GetFullFileName(info);
			return _fileStorageVirtual.Exists(fileName);
		}

		private string GetDirectory(FolderStorageInfo info)
		{
			var depth = _settings.Depth;

			return GetDirectory(info, depth);
		}

		private string GetDirectory(FolderStorageInfo info, int depth)
		{
			var id = info.FolderId;
			if (id == null)
				throw new ArgumentNullException("FolderId");
			if (info.FileId == null)
				throw new ArgumentNullException("FileId");
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
				var length1 = id.Length - depth;
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
		public void ChangeDepthDirectory(int oldDepth, int newDepth)
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
						var newDirectoryName = GetDirectory(new FolderStorageInfo()
						{
							FolderId = file.Directory.Name,
							FileId = file.Name
						}, newDepth);
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
		public List<string> GetIds(long offset, int count)
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
		///  <see cref="IFolderStorage.RefreshLog()"/>
		/// </summary>
		public void RefreshLog()
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
		///  <see cref="IFolderStorage.GetLog(DateTime, int)"/>
		/// </summary>
		/// <param name="time"></param>
		/// <param name="take"></param>
		/// <returns></returns>
		public List<EventMessage> GetLog(DateTime time, int take)
		{
			return _log.GetChangedIds(time, take);
		}

		/// <summary>
		/// <see cref="IFolderStorage.GetLogDateById(string)"/>
		/// </summary>
		/// <param name="id"></param>
		/// <returns></returns>
		public DateTime? GetLogDateById(FolderStorageInfo info)
		{
			return _log.GetDateById(info.FileId);
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
		/// <see cref="IFolderStorage.GetFullFileName(FolderStorageInfo)"/>
		/// </summary>
		/// <param name="info"></param>
		/// <returns></returns>
		public string GetFullFileName(FolderStorageInfo info)
		{
			return GetFullFileName(info, false);
		}

		/// <summary>
		/// <see cref="IFolderStorage.Move(FolderStorageInfo, FolderStorageInfo)"/>
		/// </summary>
		/// <param name="fromId"></param>
		/// <param name="toId"></param>
		public void Move(FolderStorageInfo fromId, FolderStorageInfo toId)
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


				_log.Write(EventType.FileSave, fileInfo.Name, fileInfo.Length, toId.FolderId);
				_log.Write(EventType.FileDelete, fileInfoFrom.Name, fileInfoFrom.Length, fromId.FolderId);
			}

		}
		/// <summary>
		/// <see cref="IFolderStorage.GetSize(FolderStorageInfo)"/>
		/// </summary>
		/// <param name="info"></param>
		/// <returns></returns>
		public long GetSize(FolderStorageInfo info)
		{
			var fullName = GetFullFileName(info, false);
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
		/// <param name="info"></param> 
		/// <param name="createDirectory"></param>
		/// <returns></returns>
		public string GetFullFileName(FolderStorageInfo info, bool createDirectory)
		{
			var directory = GetDirectory(info);
			_fileStorageVirtual.AddDirectory(directory, createDirectory);

			//var fileNameTo = Path.Combine(directory, id);
			var fileName = directory + Path.DirectorySeparatorChar + info.FileId;
			return fileName;
		}

		/// <summary>
		/// <see cref="IFolderStorage.Save(Stream, FolderStorageInfo)"/>
		/// </summary>
		/// <param name="stream"></param>
		/// <param name="id"></param>
		public void Save(Stream stream, FolderStorageInfo info)
		{

			SaveStreamToFile(stream, info);
		}


		/// <summary>
		/// <see cref="IFolderStorage.SaveStream(Stream, FolderStorageInfo)"/>
		/// </summary>
		/// <param name="input"></param>
		/// <param name="info"></param>
		/// <returns></returns>
		public FileToken SaveStream(Stream input, FolderStorageInfo info)
		{
			Save(input, info);
			return new FileToken(info.FileId, info.FileId, 0);
		}

		/// <summary>
		/// <see cref="IFolderStorage.DeleteAll()"/>
		/// </summary>
		public void DeleteAll()
		{
			Directory.Delete(_rootDirectory);
		}

		/// <summary>
		///  <see cref="IFolderStorage.GetRelativeFileName(string)"/>
		/// </summary>
		/// <param name="id"></param>
		/// <returns></returns>
		public string GetRelativeFileName(FolderStorageInfo info)
		{
			var directory = GetDirectory(info);
			var directoryInfo = new DirectoryInfo(directory);
			if (directoryInfo.Exists)
			{
				return Path.Combine(directoryInfo.Name, info.FileId);
			}

			return null;
		}
	}
}
