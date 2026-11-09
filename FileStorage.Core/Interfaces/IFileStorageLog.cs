using FileStorage.Core.Entities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using File = System.IO.File;

namespace FileStorage.Core.Interfaces
{
	public enum EventType
	{
		Unknown = 0,
		FileSave = 1,
		FileLoad = 2,
		FileDelete = 3,
		FileMoveDelete = 4,
	}



	public interface IFileStorageLog : IDisposable
	{
		List<EventMessage> GetAll();
		void Rewrite(List<EventMessage> eventMessages, DateTime time);
		void Write(EventType type, string fileId, long size, string folderId = null);

		void Write(EventMessage eventMessage);

		void Flush();

		List<EventMessage> GetChangedIds(DateTime time, int take);

		EventMessage GetLast();

		bool IsExists(string id);

		DateTime? GetDateById(string id);

		void Refresh(DateTime time);

		List<EventMessage> GetChangesToIds(DateTime time);

		void Clear();
	}

	internal class FileInfoLocal
	{
		public string FileName { get; set; }

		public DateTime LastWriteTimeUtc { get; set; }

		/// <summary>
		/// Позиция в байтах после последней прочитанной полной строки
		/// </summary>
		public long Position { get; set; }
	}

	public class FileStorageLog : IFileStorageLog
	{
		private readonly List<EventMessage> _newEvents;

		private EventMessageScope _scope;

		private readonly int _maxEventBufSize = 100;

		private readonly object _sync = new object();

		private readonly object _syncSave = new object();
		private readonly string _rootDirectory;

		private readonly ILocalLogger _logger;

		private DisallowConcurrentModeRepeater _repeater = new DisallowConcurrentModeRepeater(new TimeSpan(), new TimeSpan(0, 0, 0, 60));

		private DisallowConcurrentModeRepeater _repeaterFileCheck = new DisallowConcurrentModeRepeater(new TimeSpan(), new TimeSpan(0, 0, 0, 60));

		private List<FileInfoLocal> _files = new List<FileInfoLocal>();

		public FileStorageLog(string rootDirectory)
			: this(rootDirectory, null)
		{
		}

		public FileStorageLog(string rootDirectory, ILocalLogger logger)
		{
			_rootDirectory = rootDirectory;
			_logger = logger;
			_newEvents = new List<EventMessage>(10000);
			_scope = new EventMessageScope();

			Reinit();

			// раньше ошибки таймеров пропадали молча: на событие Error никто не подписывался
			_repeater.Error += OnRepeaterError;
			_repeaterFileCheck.Error += OnRepeaterError;

			_repeater.Start((token) =>
			{
				Flush();
			});
			_repeaterFileCheck.Start((token) =>
			{
				Check();
			});
		}

		private void Reinit()
		{
			var rootLogDir = GetRootLogDir();
			if (!Directory.Exists(rootLogDir))
			{
				Directory.CreateDirectory(rootLogDir);
				return;
			}
			foreach (var logFile in GetLogFiles())
			{
				var eventMessages = new List<EventMessage>();
				AddFile(logFile.Value, logFile.Key, eventMessages);
				_scope.AddRange(eventMessages);
			}
		}

		/// <summary>
		/// Файлы журнала (Log/yyyyMM/yyyyMM.log) с месяцем, к которому они относятся, по возрастанию месяца
		/// </summary>
		private List<KeyValuePair<DateTime, string>> GetLogFiles()
		{
			var result = new List<KeyValuePair<DateTime, string>>();
			var rootLogDir = GetRootLogDir();
			if (!Directory.Exists(rootLogDir))
				return result;

			var regex = new Regex("(?<y>\\d{4})(?<m>\\d{2})\\.log");
			foreach (var file in Directory.GetFiles(rootLogDir, "*.log", SearchOption.AllDirectories))
			{
				var match = regex.Match(Path.GetFileName(file));
				if (!match.Success)
					continue;
				var year = int.Parse(match.Groups["y"].Value);
				var month = int.Parse(match.Groups["m"].Value);
				var date = new DateTime(year, month, 1, 0, 0, 0, 0, DateTimeKind.Utc);
				result.Add(new KeyValuePair<DateTime, string>(date, file));
			}
			return result.OrderBy(x => x.Key).ToList();
		}

		/// <summary>
		/// Останавливает таймеры и сохраняет ещё не записанные события
		/// </summary>
		public void Dispose()
		{
			_repeater.Dispose();
			_repeaterFileCheck.Dispose();
			Flush();
		}

		public void Write(EventType type, string fileId, long size, string folderId)
		{
			var dateTime = DateTime.UtcNow;
			Write(new EventMessage()
			{
				Time = dateTime,
				Id = fileId,
				FolderId = folderId,
				Type = type,
				Size = size
			});
		}
		public void Rewrite(List<EventMessage> eventMessages, DateTime time)
		{
			var rootLogDir = GetRootLogDir();
			lock (_syncSave)
			{
				var files = Directory.Exists(rootLogDir)
					? Directory.GetFiles(rootLogDir, "*.log", SearchOption.AllDirectories)
					: Array.Empty<string>();
				foreach (var file in files)
				{
					File.Move(file, $"{file}_{Guid.NewGuid()}.old");
				}

				// События, ещё не сброшенные на диск (например, удаления из ClearTtl), сохраняем в новый журнал.
				// Часть из них уже есть в eventMessages (это снимок журнала); множество строим только по _newEvents.
				// Раньше Distinct строил множество из всех событий журнала, а ToList делал ещё одну полную копию.
				var pending = new HashSet<EventMessage>(_newEvents);
				foreach (var eventMessage in eventMessages)
					pending.Remove(eventMessage);
				var notSaved = new List<EventMessage>(pending.Count);
				foreach (var eventMessage in _newEvents)
				{
					if (pending.Remove(eventMessage))
						notSaved.Add(eventMessage);
				}
				_newEvents.Clear();

				// Сбрасываем состояние: иначе новый файл с тем же именем не загрузится (он уже есть в _files),
				// а в памяти останутся записи об удалённых файлах.
				_files.Clear();
				_scope = new EventMessageScope();

				if (eventMessages.Count > 0)
					Save(eventMessages, DateTime.UtcNow);
				if (notSaved.Count > 0)
					Save(notSaved, DateTime.UtcNow);
				Reinit();
			}
		}
		public void Write(EventMessage eventMessage)
		{
			lock (_syncSave)
			{
				_newEvents.Add(eventMessage);
				_scope.Add(eventMessage);
			}
		}

		public void Flush()
		{
			lock (_syncSave)
			{
				if (_newEvents.Any())
				{
					Save(_newEvents, DateTime.UtcNow);
					_newEvents.Clear();
				}
			}
		}

		private void Check()
		{
			// _files и счётчики строк меняются также в Refresh/Rewrite, поэтому работаем под той же блокировкой
			lock (_syncSave)
			{
				var events = new List<EventMessage>();
				foreach (var file in _files)
				{
					var fileInfo = new FileInfo(file.FileName);
					// читаем только дописанный хвост: позиция хранится в байтах
					if (fileInfo.Exists && (fileInfo.Length != file.Position || fileInfo.LastWriteTimeUtc > file.LastWriteTimeUtc))
					{
						file.LastWriteTimeUtc = fileInfo.LastWriteTimeUtc;
						events.AddRange(LoadFromFile(file));
					}
				}

				if (events.Any())
				{
					_scope.AddRange(events);
				}
			}
		}

		private void Save(List<EventMessage> items, DateTime date)
		{
			var fileName = GetFileName(date);
			var fileInfo = new FileInfo(fileName);
			if (!fileInfo.Exists && !fileInfo.Directory.Exists)
			{
				fileInfo.Directory.Create();
			}
			//using (var outstream = File.AppendText(fileName))
			try
			{

				// Строки пишутся сразу в файл. Раньше весь список собирался в StringBuilder и строку:
				// при Rewrite из ClearTtl это две копии всего журнала в памяти (по ~250 байт на событие).
				using (var outstream = new StreamWriter(fileName, true, Encoding.UTF8, 64 * 1024))
				{
					foreach (var item in items)
					{
						// Время в ISO 8601 ("O"): не зависит от культуры сервера и хранит время с точностью до тика.
						outstream.WriteLine($"{item.Time.ToString("O", CultureInfo.InvariantCulture)};{Escape(item.Id)};{Escape(item.FolderId)};{(int)item.Type};{item.Size.ToString(CultureInfo.InvariantCulture)}");
					}
				}
			}
			catch (Exception ex)
			{

				throw;
			}
		}

		private List<EventMessage> LoadFromFile(DateTime time)
		{
			var list = new List<EventMessage>();
			// Журналы помесячные: загружаем все файлы начиная с месяца time включительно.
			// Раньше перебор шёл по дням начиная с time + 1 день: журнал месяца time мог пропуститься,
			// а для DateTime.MinValue выходило около 740 тыс. итераций.
			var fromMonth = new DateTime(time.Year, time.Month, 1, 0, 0, 0, DateTimeKind.Utc);
			foreach (var logFile in GetLogFiles())
			{
				if (logFile.Key >= fromMonth)
				{
					AddFile(logFile.Value, logFile.Key, list);
				}
			}
			_scope.SetMinMax(time);
			return list;
		}

		private void AddFile(string fileName, DateTime dateTime, List<EventMessage> list)
		{
			var fileInfo = new FileInfo(fileName);
			if (fileInfo.Exists && _files.All(x => x.FileName != fileName))
			{
				_scope.SetMinMax(dateTime);

				var fileInfoLocal = new FileInfoLocal()
				{
					FileName = fileName,
					LastWriteTimeUtc = fileInfo.LastWriteTimeUtc
				};
				list.AddRange(LoadFromFile(fileInfoLocal));

				_files.Add(fileInfoLocal);
			}
		}

		private const int ReadBufferSize = 64 * 1024;

		private static readonly byte[] Utf8Preamble = Encoding.UTF8.GetPreamble();

		/// <summary>
		/// Читает строки журнала с сохранённой позиции до конца файла. Раньше файл каждый раз читался с начала,
		/// а уже прочитанные строки пропускались по счётчику: раз в минуту весь месячный журнал читался
		/// с диска и разбивался на строки. Неполная последняя строка (журнал дописывается) остаётся до следующего чтения.
		/// </summary>
		private static List<EventMessage> LoadFromFile(FileInfoLocal fileInfoLocal)
		{
			var list = new List<EventMessage>();
			using (var fileStream = new FileStream(fileInfoLocal.FileName, FileMode.Open, FileAccess.Read,
				FileShare.ReadWrite | FileShare.Delete, ReadBufferSize))
			{
				// файл заменили или обрезали — читаем заново
				if (fileStream.Length < fileInfoLocal.Position)
					fileInfoLocal.Position = 0;
				fileStream.Position = fileInfoLocal.Position;

				var buffer = new byte[ReadBufferSize];
				var count = 0;
				int read;
				while ((read = fileStream.Read(buffer, count, buffer.Length - count)) > 0)
				{
					count += read;
					var start = 0;
					int end;
					while ((end = Array.IndexOf(buffer, (byte)'\n', start, count - start)) >= 0)
					{
						ParseLine(buffer, start, end - start, fileInfoLocal.Position + start == 0, list);
						start = end + 1;
					}

					fileInfoLocal.Position += start;
					count -= start;
					if (count > 0 && start > 0)
						Buffer.BlockCopy(buffer, start, buffer, 0, count);
					// строка длиннее буфера
					if (count == buffer.Length)
						Array.Resize(ref buffer, buffer.Length * 2);
				}
			}
			return list;
		}

		private static void ParseLine(byte[] buffer, int offset, int length, bool isFileStart, List<EventMessage> list)
		{
			if (isFileStart && length >= Utf8Preamble.Length
				&& buffer.AsSpan(offset, Utf8Preamble.Length).SequenceEqual(Utf8Preamble))
			{
				offset += Utf8Preamble.Length;
				length -= Utf8Preamble.Length;
			}
			if (length > 0 && buffer[offset + length - 1] == (byte)'\r')
				length--;
			if (length <= 0)
				return;

			var data = Encoding.UTF8.GetString(buffer, offset, length).Split(';');
			if (data.Length >= 3)
			{
				var eventMessage = new EventMessage();
				if (Parse(data, eventMessage))
					list.Add(eventMessage);
			}
		}

		private static bool Parse(string[] data, EventMessage eventMessage)
		{
			bool isParsed = true;
			DateTime date;
			// Новые записи — в формате ISO 8601 с признаком UTC. Старые писались в формате текущей культуры,
			// их разбираем как раньше.
			if (DateTime.TryParse(data[0], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out date)
				&& date.Kind == DateTimeKind.Utc)
			{
				eventMessage.Time = date;
			}
			else if (DateTime.TryParse(data[0], out date))
			{
				eventMessage.Time = new DateTime(date.Ticks, DateTimeKind.Utc);
			}
			else
			{
				isParsed = false;
			}
			eventMessage.Id = Unescape(data[1]);
			var index = 2;
			if (data.Length > 4)
			{
				eventMessage.FolderId = Unescape(data[2]);
				index = 3;
			}

			int type = 0;
			if (int.TryParse(data[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out type))
			{
				eventMessage.Type = (EventType)type;
			}
			else
			{
				isParsed = false;
			}
			if (data.Length > index + 1)
			{
				long size;
				if (long.TryParse(data[index + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out size))
				{
					eventMessage.Size = size;
				}
			}
			return isParsed;
		}

		private static readonly char[] EscapedChars = { '%', ';', '\r', '\n' };

		/// <summary>
		/// Экранирует символы, которые ломают строку журнала (разделитель ';' и переводы строк)
		/// </summary>
		private static string Escape(string value)
		{
			if (string.IsNullOrEmpty(value) || value.IndexOfAny(EscapedChars) < 0)
				return value;
			return value.Replace("%", "%25").Replace(";", "%3B").Replace("\r", "%0D").Replace("\n", "%0A");
		}

		private static string Unescape(string value)
		{
			if (string.IsNullOrEmpty(value) || value.IndexOf('%') < 0)
				return value;
			return value.Replace("%3B", ";").Replace("%0D", "\r").Replace("%0A", "\n").Replace("%25", "%");
		}

		private void OnRepeaterError(object sender, UnhandledExceptionEventArgs e)
		{
			var message = $"FileStorageLog {_rootDirectory}: background operation failed";
			var exception = e.ExceptionObject as Exception;
			if (_logger != null)
			{
				_logger.Error(message, exception);
			}
			else
			{
				System.Diagnostics.Trace.TraceError($"{message}: {exception}");
			}
		}

		public string GetRootLogDir()
		{
			return Path.Combine(_rootDirectory, "Log");
		}

		private string GetFileName(DateTime dateTime)
		{
			var dateStr = dateTime.ToString("yyyyMM");
			var directory = Path.Combine(GetRootLogDir(), dateStr);

			var fileName = Path.Combine(directory, $"{dateStr}.log");
			return fileName;
		}

		public List<EventMessage> GetChangedIds(DateTime time, int take)
		{
			Refresh(time);
			return _scope.GetByDate(time, take);
		}

		public List<EventMessage> GetChangesToIds(DateTime time)
		{
			Refresh(time);
			return _scope.GetToDate(time);
		}
		public List<EventMessage> GetAll()
		{
			Refresh(DateTime.UtcNow);
			return _scope.GetAll();
		}
		public void Clear()
		{
			lock (_syncSave)
			{
				_newEvents.Clear();
				_files.Clear();
			}
		}

		public bool IsExists(string id)
		{
			var values = _scope.GetByDate(id);
			if (values.Any())
				return true;
			return false;
		}

		public DateTime? GetDateById(string id)
		{
			var values = _scope.GetByDate(id);
			if (!values.Any())
				return null;
			return values.OrderBy(x => x.Time).Last().Time;
		}

		public void Refresh(DateTime time)
		{
			if ((_scope._minTime > time || time > _scope._maxTime) && time <= DateTime.UtcNow)
			{
				lock (_syncSave)
				{
					if ((_scope._minTime > time || time > _scope._maxTime) && time <= DateTime.UtcNow)
					{
						var events = LoadFromFile(time);
						if (events.Any())
						{
							_scope.AddRange(events);
						}
					}
				}
			}
		}

		public EventMessage GetLast()
		{
			return _scope.GetLast();
		}
	}
}
