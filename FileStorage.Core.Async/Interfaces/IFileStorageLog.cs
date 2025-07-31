using FileStorage.Core.Entities;
using System;
using System.Collections.Generic;
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



	public interface IFileStorageLog
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

		public long Lines { get; set; }
	}

	public class FileStorageLog : IFileStorageLog
	{
		private readonly List<EventMessage> _newEvents;

		private EventMessageScope _scope;

		private readonly int _maxEventBufSize = 100;

		private readonly object _sync = new object();

		private readonly object _syncSave = new object();
		private readonly string _rootDirectory;

		private DisallowConcurrentModeRepeater _repeater = new DisallowConcurrentModeRepeater(new TimeSpan(), new TimeSpan(0, 0, 0, 60));

		private DisallowConcurrentModeRepeater _repeaterFileCheck = new DisallowConcurrentModeRepeater(new TimeSpan(), new TimeSpan(0, 0, 0, 60));

		private List<FileInfoLocal> _files = new List<FileInfoLocal>();

		public FileStorageLog(string rootDirectory)
		{
			_rootDirectory = rootDirectory;
			_newEvents = new List<EventMessage>(10000);
			_scope = new EventMessageScope();

			Reinit();

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
			if (Directory.Exists(rootLogDir))
			{
				var files = Directory.GetFiles(rootLogDir, "*.log", SearchOption.AllDirectories);
				var regex = new Regex("(?<y>\\d{4})(?<m>\\d{2})\\.log");
				foreach (var file in files)
				{
					var fileInfo = new FileInfo(file);
					var match = regex.Match(fileInfo.Name);
					if (match.Success)
					{
						var month = match.Groups["m"].Value;
						var year = match.Groups["y"].Value;
						var date = new DateTime(int.Parse(year), int.Parse(month), 1, 0, 0, 0, 0, DateTimeKind.Utc);
						var eventMessages = new List<EventMessage>();
						AddFile(file, date, eventMessages);
						_scope.AddRange(eventMessages);
					}
				}
			}
			else
			{
				Directory.CreateDirectory(rootLogDir);
			}
		}


		public void Write(EventType type, string fileId, long size, string folderId)
		{
			var dateTime = DateTime.UtcNow;
			Write(new EventMessage()
			{
				Time = dateTime,
				Id = fileId,
				FolderId = folderId,
				Type = EventType.FileSave,
				Size = size
			});
		}
		public void Rewrite(List<EventMessage> eventMessages, DateTime time)
		{
			var rootLogDir = GetRootLogDir();
			lock (_syncSave)
			{

				var files = Directory.GetFiles(rootLogDir, "*.log", SearchOption.AllDirectories);
				foreach (var file in files)
				{
					File.Move(file, $"{file}_{Guid.NewGuid()}.old");
				}
				if (eventMessages.Any())
					Save(eventMessages, DateTime.UtcNow);
				Reinit();

			}


		}
		public void Write(EventMessage eventMessage)
		{

			lock (_syncSave)
			{
				_newEvents.Add(eventMessage);
			}

			_scope.Add(eventMessage);
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
			foreach (var file in _files)
			{
				var fileInfo = new FileInfo(file.FileName);
				if (fileInfo.Exists && fileInfo.LastWriteTimeUtc > file.LastWriteTimeUtc)
				{
					var events = LoadFromFile(file);

					if (events.Any())
					{
						lock (_sync)
						{
							if (events.Any())
							{
								_scope.AddRange(events);
							}
						}
					}
				}
			}

		}





		private void Save(List<EventMessage> items, DateTime date)
		{
			var sb = new StringBuilder();
			foreach (var item in items)
			{
				sb.AppendLine($"{item.Time};{item.Id};{item.FolderId};{(int)item.Type};{item.Size}");
			}
			var fileName = GetFileName(date);
			var fileInfo = new FileInfo(fileName);
			if (!fileInfo.Exists && !fileInfo.Directory.Exists)
			{
				fileInfo.Directory.Create();
			}
			//using (var outstream = File.AppendText(fileName))
			try
			{

				using (var outstream = new StreamWriter(fileName, true, Encoding.UTF8))
				{
					outstream.Write(sb.ToString());
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

			var dateTime = time.Date;
			var fileName = GetFileName(dateTime);

			while (dateTime.Date <= DateTime.UtcNow)
			{
				dateTime = dateTime.AddDays(1);
				fileName = GetFileName(dateTime);
				AddFile(fileName, dateTime, list);
			}
			_scope.SetMinMax(time);

			//var fileInfo = new FileInfo(fileName);

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

		private static List<EventMessage> LoadFromFile(FileInfoLocal fileInfoLocal)
		{
			var list = new List<EventMessage>();
			long lineIndex = 0;
			using (var fileStream = File.OpenRead(fileInfoLocal.FileName))
			{
				using (var streamReader = new StreamReader(fileStream, Encoding.UTF8))
				{
					String line;
					while ((line = streamReader.ReadLine()) != null)
					{
						lineIndex++;
						if (fileInfoLocal.Lines >= lineIndex)
							continue;

						var data = line.Split(';');

						if (data.Length >= 3)
						{
							EventMessage eventMessage = new EventMessage();
							if (Parse(data, eventMessage))
								list.Add(eventMessage);
						}

					}
				}
			}
			fileInfoLocal.Lines = lineIndex;
			return list;
		}

		private static bool Parse(string[] data, EventMessage eventMessage)
		{
			bool isParsed = true;
			DateTime date;
			if (DateTime.TryParse(data[0], out date))
			{
				eventMessage.Time = new DateTime(date.Ticks, DateTimeKind.Utc);
			}
			else
			{
				isParsed = false;
			}
			eventMessage.Id = data[1];
			var index = 2;
			if (data.Length > 4)
			{
				eventMessage.FolderId = data[2];
				index = 3;

			}



			int type = 0;
			if (int.TryParse(data[index], out type))
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
				if (long.TryParse(data[index + 1], out size))
				{
					eventMessage.Size = size;
				}
			}
			return isParsed;
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
			_newEvents.Clear();
			_files.Clear();
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
				List<EventMessage> events = new List<EventMessage>();
				lock (_syncSave)
				{
					if ((_scope._minTime > time || time > _scope._maxTime) && time <= DateTime.UtcNow)
					{
						events = LoadFromFile(time);
					}
				}
				if (events.Any())
				{
					lock (_sync)
					{
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
