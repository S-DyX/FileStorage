using FileStorage.Core.Contracts;
using FileStorage.Core.Entities;
using FileStorage.Core.Helpers;
using FileStorage.Core.Interfaces;
using FileStorage.Core.Interfaces.Settings;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace FileStorage.Core
{
	/// <summary>
	/// Hierarchical file storage stored on disk
	/// </summary>
	public sealed class FolderStorage : IFolderStorage, IDisposable
	{
		private readonly string _name;
		private readonly IFileStorageSettings _settings;
		private readonly IFileStorageVirtual _fileStorageVirtual;
		private readonly ILocalLogger _localLogger;
		private IFileStorageLog _log;
		private readonly string _rootDirectory;
		// Глубина дерева каталогов этого хранилища. Хранится отдельно от общих настроек,
		// чтобы ChangeDepthDirectory не влиял на другие хранилища с теми же настройками.
		private int _depth;
		private static readonly StringComparison PathComparison =
			OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
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
			_depth = LoadDepth();
			_log = new FileStorageLog(_rootDirectory, _localLogger);
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
			var length = new FileInfo(filenameFrom).Length;
			var target = new FileInfo(fileNameTo);
			if (target.Exists && target.Length == length && StreamHelper.FilesAreEqual(fileNameTo, filenameFrom))
			{
				// такой же файл уже сохранён (например, повторный SaveAsHash) — перезаписывать не нужно
				_fileStorageVirtual.Delete(filenameFrom);
				_fileStorageVirtual.Add(fileNameTo);
				return;
			}
			// File.Move перезаписывает существующий файл. Раньше новое содержимое того же размера молча
			// выбрасывалось, а если кеш считал файл существующим, а на диске его не было, данные терялись.
			_fileStorageVirtual.Move(filenameFrom, fileNameTo);
			_log.Write(EventType.FileSave, fileInfo.Name, length, info.FolderId);
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
					// запрос может только закрывать файл, без данных
					var bytes = request.Bytes ?? Array.Empty<byte>();

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
			_localLogger?.Info($"TTL Start:{date}");
			var files = _log.GetAll().OrderBy(x => x.Time).ToList();
			// удаления: обычные и удаление из старой папки при переносе
			var deletedById = files
				.Where(x => x.Type == EventType.FileDelete || x.Type == EventType.FileMoveDelete)
				.ToLookup(x => x.Id);
			var save = new List<EventMessage>();
			var foundAny = false;
			Parallel.ForEach(files, file =>
			{
				if (file.Time > date)
				{
					lock (save)
					{
						save.Add(file);
					}
					return;
				}

				foundAny = true;
				switch (file.Type)
				{
					case EventType.FileDelete:
					case EventType.FileMoveDelete:
					case EventType.Unknown:
						return;
				}

				// файл позже удалён из этой папки (событие без папки — из старых журналов — относится к любой папке)
				if (deletedById[file.Id].Any(x => x.Time > file.Time && (string.IsNullOrEmpty(x.FolderId) || x.FolderId == file.FolderId)))
				{
					return;
				}

				if (string.IsNullOrEmpty(file.FolderId))
				{
					_localLogger?.Info($"File not found:{file.Id}");
					return;
				}

				Delete(new FolderStorageInfo()
				{
					FileId = file.Id,
					FolderId = file.FolderId,
					StorageName = _name
				});
				_localLogger?.Info("TTL:" + file.Id);
			});
			if (foundAny)
				_log.Rewrite(save, DateTime.UtcNow);
			DeleteExpiredFiles(date, save);
		}

		public void Drop()
		{
			ResetStorage();
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
			// раньше дописанный файл не попадал в журнал и не был виден синхронизации
			_log.Write(EventType.FileSave, info.FileId, new FileInfo(fullName).Length, info.FolderId);
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
				// Короткий FolderId (не длиннее глубины) даёт корень хранилища — удалять его файлы нельзя.
				if (IsRootDirectory(directory))
					return false;
				_fileStorageVirtual.DeleteDirectory(directory);
				return true;
			}

			var fileName = GetFullFileName(info);
			if (!_fileStorageVirtual.Exists(fileName))
				return false;

			_fileStorageVirtual.Delete(fileName);
			// каталог мог исчезнуть раньше, чем обновился кеш; корень хранилища не удаляем
			if (!IsRootDirectory(directory) && Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any())
			{
				_fileStorageVirtual.DeleteDirectory(directory);
			}

			if (!info.DoNotNeedLog)
			{
				_log.Write(new EventMessage()
				{
					Type = EventType.FileDelete,
					Id = info.FileId,
					// папка нужна, чтобы удаление не затронуло файл с тем же id в других папках
					FolderId = info.FolderId,
					Time = DateTime.UtcNow
				});
			}

			return true;
		}
		/// <summary>
		/// <see cref="IFolderStorage.DeleteTemp(FolderStorageInfo)"/>
		/// </summary>
		/// <param name="info"></param>
		/// <returns></returns>
		public bool DeleteTemp(FolderStorageInfo info)
		{
			var fileName = GetFullFileName(info);
			var directory = Path.GetDirectoryName(fileName);
			if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
				return false;

			// Временные файлы пишутся как "<файл>_<sessionId>.tmp" (см. SaveToFile).
			// sessionId сюда не передаётся, поэтому удаляем временные файлы всех сессий этого файла.
			var tempFiles = Directory.GetFiles(directory, Path.GetFileName(fileName) + "_*.tmp", SearchOption.TopDirectoryOnly);
			foreach (var tempFile in tempFiles)
			{
				_fileStorageVirtual.Delete(tempFile);
			}
			return tempFiles.Length > 0;
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
			var depth = _depth;

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
				// При глубине 1 первый символ id в имя каталога не входит — так файлы уже разложены на диске.
				// Раньше для коротких id здесь было ArgumentOutOfRangeException.
				var start = Math.Min(depth, id.Length);
				pathSb.Append(id.Substring(start, Math.Min(length, id.Length - start)));
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
			// oldDepth не используется: текущая глубина хранится в _depth.
			if (newDepth < 0)
				throw new ArgumentOutOfRangeException(nameof(newDepth));
			if (!Directory.Exists(_rootDirectory))
			{
				_depth = newDepth;
				SaveDepth(newDepth);
				return;
			}
			// При глубине 0 и 1 FolderId попадает в путь не целиком (обрезается по ElementsCount
			// или теряется первый символ), поэтому восстановить его из пути и перенести файлы нельзя.
			if (_depth <= 1)
				throw new NotSupportedException($"Changing depth is not supported when current depth is {_depth}: FolderId can not be restored from the path");

			var logDirectory = GetLogDirectory();
			var files = Directory.GetFiles(_rootDirectory, "*", SearchOption.AllDirectories)
				.Where(f => !IsServiceFile(f))
				.ToList();

			// Сначала вычисляем все новые пути: если для какого-то файла путь построить нельзя,
			// исключение возникнет до того, как будет перенесён хотя бы один файл.
			var moves = new List<KeyValuePair<string, string>>(files.Count);
			foreach (var file in files)
			{
				var folderId = GetFolderIdFromPath(file);
				// файлы в корне (FolderId не длиннее глубины) лежат там при любой глубине больше 1
				if (folderId == null)
					continue;
				var fileId = Path.GetFileName(file);
				var newDirectoryName = GetDirectory(new FolderStorageInfo()
				{
					FolderId = folderId,
					FileId = fileId
				}, newDepth);
				var destFileName = newDirectoryName + Path.DirectorySeparatorChar + fileId;
				if (!string.Equals(file, destFileName, PathComparison))
					moves.Add(new KeyValuePair<string, string>(file, destFileName));
			}

			foreach (var move in moves)
			{
				_fileStorageVirtual.AddDirectory(Path.GetDirectoryName(move.Value), true);
				_fileStorageVirtual.Move(move.Key, move.Value);
			}

			_depth = newDepth;
			// сохраняем глубину, чтобы после перезапуска файлы искались по новой схеме
			SaveDepth(newDepth);
			DeleteEmptyDirectories(_rootDirectory, logDirectory);
		}

		private string GetLogDirectory()
		{
			return Path.Combine(_rootDirectory, "Log") + Path.DirectorySeparatorChar;
		}

		private const string DepthFileName = "depth.config";

		private string GetDepthFileName()
		{
			return Path.Combine(_rootDirectory, DepthFileName);
		}

		/// <summary>
		/// Глубина дерева этого хранилища. После ChangeDepthDirectory она сохраняется в каталоге хранилища,
		/// чтобы после перезапуска файлы искались по фактической схеме, а не по TreeDepth из настроек.
		/// </summary>
		private int LoadDepth()
		{
			var fileName = GetDepthFileName();
			try
			{
				int depth;
				if (File.Exists(fileName)
					&& int.TryParse(File.ReadAllText(fileName).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out depth)
					&& depth >= 0)
				{
					return depth;
				}
			}
			catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
			{
				_localLogger?.Error($"Can not read {fileName}", e);
			}
			return _settings.Depth;
		}

		private void SaveDepth(int depth)
		{
			Directory.CreateDirectory(_rootDirectory);
			File.WriteAllText(GetDepthFileName(), depth.ToString(CultureInfo.InvariantCulture));
		}

		/// <summary>
		/// Служебные файлы в каталоге хранилища: журнал, временные файлы, файл глубины дерева
		/// </summary>
		private bool IsServiceFile(string fileName)
		{
			return fileName.StartsWith(GetLogDirectory(), PathComparison)
				|| fileName.EndsWith(".tmp", PathComparison)
				|| string.Equals(fileName, GetDepthFileName(), PathComparison);
		}

		private bool IsRootDirectory(string directory)
		{
			return string.Equals(directory.TrimEnd(Path.DirectorySeparatorChar), _rootDirectory.TrimEnd(Path.DirectorySeparatorChar), PathComparison);
		}

		/// <summary>
		/// FolderId по пути файла. При глубине больше 1 путь — это символы FolderId по уровням плюс остаток,
		/// поэтому FolderId восстанавливается склейкой каталогов. При глубине 0–1 он хранится в пути не полностью,
		/// а у файлов в корне его нет — тогда возвращается null.
		/// </summary>
		private string GetFolderIdFromPath(string fileName)
		{
			if (_depth <= 1)
				return null;
			var fileDirectory = Path.GetDirectoryName(fileName) + Path.DirectorySeparatorChar;
			if (fileDirectory.Length <= _rootDirectory.Length || !fileDirectory.StartsWith(_rootDirectory, PathComparison))
				return null;
			var relativeDirectory = fileDirectory.Substring(_rootDirectory.Length, fileDirectory.Length - _rootDirectory.Length - 1);
			return relativeDirectory.Replace(Path.DirectorySeparatorChar.ToString(), string.Empty);
		}

		/// <summary>
		/// Удаляет с диска файлы старше date, о которых нет свежих записей в журнале (например, записанные
		/// до появления журнала), а также временные файлы и архивы журнала старше date.
		/// </summary>
		private void DeleteExpiredFiles(DateTime date, List<EventMessage> freshEvents)
		{
			// Файлы со свежими записями не трогаем: при повторном сохранении того же содержимого
			// файл не перезаписывается и сохраняет старую дату на диске.
			var freshIds = new HashSet<string>(freshEvents.Where(x => x.Type == EventType.FileSave).Select(x => x.Id));
			var logDirectory = GetLogDirectory();
			var depthFileName = GetDepthFileName();
			foreach (var file in Directory.GetFiles(_rootDirectory, "*", SearchOption.AllDirectories))
			{
				if (string.Equals(file, depthFileName, PathComparison))
					continue;
				var fileInfo = new FileInfo(file);
				if (fileInfo.Extension == ".log")
					continue;
				var lastChange = fileInfo.CreationTimeUtc > fileInfo.LastWriteTimeUtc ? fileInfo.CreationTimeUtc : fileInfo.LastWriteTimeUtc;
				if (lastChange >= date)
					continue;
				var isStorageFile = !file.StartsWith(logDirectory, PathComparison) && !file.EndsWith(".tmp", PathComparison);
				if (isStorageFile && freshIds.Contains(fileInfo.Name))
					continue;
				try
				{
					var length = fileInfo.Length;
					var folderId = isStorageFile ? GetFolderIdFromPath(file) : null;
					_fileStorageVirtual.Delete(file);
					// Об удалении пишем в журнал, чтобы его увидела синхронизация. Без папки событие
					// относилось бы к файлу с этим id во всех папках, поэтому пишем только если папка известна.
					if (folderId != null)
					{
						_log.Write(EventType.FileDelete, fileInfo.Name, length, folderId);
					}
				}
				catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
				{
					// файл может быть занят (например, идёт загрузка) — пропускаем его, а не прерываем очистку
					_localLogger?.Error($"TTL: can not delete {file}", e);
				}
			}
		}

		private static void DeleteEmptyDirectories(string directory, string logDirectory)
		{
			foreach (var subDirectory in Directory.GetDirectories(directory))
			{
				if ((subDirectory + Path.DirectorySeparatorChar).StartsWith(logDirectory, PathComparison))
					continue;
				DeleteEmptyDirectories(subDirectory, logDirectory);
				if (!Directory.EnumerateFileSystemEntries(subDirectory).Any())
				{
					Directory.Delete(subDirectory);
				}
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
				// id возвращаем как есть: на Linux регистр в именах файлов важен
				result.Add(id);

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
			var logDirectory = GetLogDirectory();
			Parallel.ForEach(directories, dir =>
			{
				// каталог журнала событий не содержит файлов хранилища
				if ((dir + Path.DirectorySeparatorChar).StartsWith(logDirectory, PathComparison))
					return;

				var d = Directory.GetDirectories(dir);
				if (!d.Any())
				{
					_fileStorageVirtual.AddDirectory(dir, false);
					var files = Directory.GetFiles(dir, "*", SearchOption.TopDirectoryOnly);
					Parallel.ForEach(files, f =>
					{
						if (f.EndsWith(".tmp", PathComparison))
							return;
						var fileInfo = new FileInfo(f);
						AddFile(fileInfo);
						// в кеш кладём полный путь — в том же виде, в каком его ищут Exists/Delete
						_fileStorageVirtual.Add(f);
					});
				}
				else
				{
					RefreshSub(d);
				}
			});
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
			if (!Exists(fromId))
				return;

			var fileNameFrom = GetFullFileName(fromId);
			var fileNameTo = GetFullFileName(toId, true);
			if (string.Equals(fileNameFrom, fileNameTo, PathComparison))
				return;

			// размер берём до переноса: после него исходного файла уже нет
			var length = new FileInfo(fileNameFrom).Length;
			// File.Move перезаписывает существующий файл, удалять его отдельно не нужно
			_fileStorageVirtual.Move(fileNameFrom, fileNameTo);

			// Журнал хранит события по паре «папка + id», поэтому удаление из старой папки
			// и сохранение в новой не перекрывают друг друга, даже если имя файла не меняется.
			_log.Write(EventType.FileMoveDelete, fromId.FileId, length, fromId.FolderId);
			_log.Write(EventType.FileSave, toId.FileId, length, toId.FolderId);
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
			return new FileToken(info.FileId, info.FileId, GetSize(info));
		}

		/// <summary>
		/// <see cref="IFolderStorage.DeleteAll()"/>
		/// </summary>
		public void DeleteAll()
		{
			ResetStorage();
		}

		/// <summary>
		/// Полностью очищает хранилище: убирает его файлы из кеша, останавливает журнал,
		/// удаляет каталог вместе с содержимым и создаёт пустой журнал.
		/// </summary>
		private void ResetStorage()
		{
			// кеш общий для всех хранилищ фабрики, поэтому убираем из него только файлы этого хранилища
			_fileStorageVirtual.ReleaseDirectory(_rootDirectory);
			// новые события не сохраняем, а таймеры старого журнала останавливаем
			_log.Clear();
			_log.Dispose();
			if (Directory.Exists(_rootDirectory))
			{
				Directory.Delete(_rootDirectory, true);
			}
			_log = new FileStorageLog(_rootDirectory, _localLogger);
			// файл глубины удалён вместе с каталогом; если глубина отличается от настроек, сохраняем её снова
			if (_depth != _settings.Depth)
			{
				SaveDepth(_depth);
			}
		}

		/// <summary>
		///  <see cref="IFolderStorage.GetRelativeFileName(string)"/>
		/// </summary>
		/// <param name="id"></param>
		/// <returns></returns>
		public string GetRelativeFileName(FolderStorageInfo info)
		{
			var directory = GetDirectory(info);
			if (!Directory.Exists(directory))
				return null;
			// путь от корня хранилища; раньше возвращался только последний каталог
			var relativeDirectory = directory.Length > _rootDirectory.Length ? directory.Substring(_rootDirectory.Length) : string.Empty;
			return Path.Combine(relativeDirectory, info.FileId);
		}

		/// <summary>
		/// Останавливает таймеры журнала и сохраняет ещё не записанные события
		/// </summary>
		public void Dispose()
		{
			_log.Dispose();
		}
	}
}
