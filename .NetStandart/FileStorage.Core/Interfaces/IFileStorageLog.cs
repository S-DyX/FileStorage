using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using FileStorage.Core.Entities;
using FileStorage.Core.Helpers;
using FileStorage.Core.Interfaces.Settings;
using File = System.IO.File;

namespace FileStorage.Core.Interfaces
{
	public enum EventType
	{
		Unknown,
		FileSave,
		FileLoad,
		FileDelete,
	}



	public interface IFileStorageLog
	{
		void Write(EventType type, string fileId, long size);

		void Write(EventMessage eventMessage);

		void Flush();

		List<EventMessage> GetChangedIds(DateTime time, int take);

		EventMessage GetLast();

		bool IsExists(string id);

		DateTime? GetDateById(string id);

		void Refresh(DateTime time);

		List<EventMessage> GetChangesToIds(DateTime time);
	}

	internal class FileInfoLocal
	{
		public string FileName { get; set; }

		public DateTime LastWriteTimeUtc { get; set; }

		public long Lines { get; set; }
	}

	public class FileStorageLog : IFileStorageLog
	{
		private readonly string _rootDirectory;
		private DisallowConcurrentModeRepeater _repeater = new DisallowConcurrentModeRepeater(new TimeSpan(), new TimeSpan(0, 0, 0, 60));

		private DisallowConcurrentModeRepeater _repeaterFileCheck = new DisallowConcurrentModeRepeater(new TimeSpan(), new TimeSpan(0, 0, 0, 60));

		private List<FileInfoLocal> _files = new List<FileInfoLocal>();

		public FileStorageLog(string rootDirectory)
		{
			_rootDirectory = rootDirectory;
			_newEvents = new List<EventMessage>(10000);
			_scope = new EventMessageScope();
			_repeater.Start((token) =>
			{
				Flush();
			});
			_repeaterFileCheck.Start((token) =>
			{
				Check();
			});
		}

		private readonly List<EventMessage> _newEvents;

		private readonly EventMessageScope _scope;

		private readonly int _maxEventBufSize = 100;

		private readonly object _sync = new object();

		private readonly object _syncSave = new object();



		public void Write(EventType type, string fileId, long size)
		{
			var dateTime = DateTime.UtcNow;
			Write(new EventMessage()
			{
				Time = dateTime,
				Id = fileId,
				Type = EventType.FileSave,
				Size = size
			});
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
					Save(_newEvents);
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





		private void Save(List<EventMessage> items)
		{
			var sb = new StringBuilder();
			foreach (var item in items)
			{
				sb.AppendLine($"{item.Time};{item.Id};{(int)item.Type};{item.Size}");
			}
			var fileName = GetFileName(DateTime.UtcNow);
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
			_scope.SetMinMax(time);

			//var fileInfo = new FileInfo(fileName);

			return list;
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
			int type = 0;
			if (int.TryParse(data[2], out type))
			{
				eventMessage.Type = (EventType)type;
			}
			else
			{
				isParsed = false;
			}
			if (data.Length == 4)
			{
				long size;
				if (long.TryParse(data[3], out size))
				{
					eventMessage.Size = size;
				}
			}
			return isParsed;
		}

		private string GetFileName(DateTime dateTime)
		{
			var dateStr = dateTime.ToString("yyyyMM");
			var directory = Path.Combine(_rootDirectory, "Log", dateStr);

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
