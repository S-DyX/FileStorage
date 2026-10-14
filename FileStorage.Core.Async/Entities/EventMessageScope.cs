using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FileStorage.Core.Interfaces;

namespace FileStorage.Core.Entities
{
	/// <summary>
	/// Журнал событий в памяти, сгруппированный по месяцам.
	/// Все обращения к словарю месяцев идут под одной блокировкой: класс используется из нескольких потоков
	/// (запись событий, таймеры Flush/Check, параллельный ClearTtl, чтение журнала).
	/// </summary>
	public sealed class EventMessageScope
	{
		private readonly object _sync = new object();

		private readonly Dictionary<DateTime, EventMessageGroup> _group;

		// Один экземпляр строки на каждый FolderId: у папки много файлов, а при чтении журнала
		// каждая строка файла давала свою копию FolderId.
		private readonly Dictionary<string, string> _folderIds = new Dictionary<string, string>();

		private DateTime _min;

		private DateTime _max;

		private EventMessage _last;

		public DateTime _minTime
		{
			get { lock (_sync) { return _min; } }
		}

		public DateTime _maxTime
		{
			get { lock (_sync) { return _max; } }
		}

		public EventMessageScope()
		{
			_min = DateTime.MaxValue;
			_max = DateTime.MinValue;
			_group = new Dictionary<DateTime, EventMessageGroup>();
		}

		public void SetMinMax(DateTime dateTime)
		{
			lock (_sync)
			{
				SetMinMaxInternal(dateTime);
			}
		}

		private void SetMinMaxInternal(DateTime dateTime)
		{
			if (dateTime > DateTime.UtcNow)
				return;
			if (_min > dateTime)
				_min = dateTime;
			if (_max < dateTime)
				_max = dateTime;
		}

		private EventMessageGroup GetOrCreateGroup(DateTime dateTime, int capacity)
		{
			EventMessageGroup eventMessageGroup;
			if (!_group.TryGetValue(dateTime, out eventMessageGroup))
			{
				eventMessageGroup = new EventMessageGroup(dateTime, capacity);
				_group.Add(dateTime, eventMessageGroup);
			}
			return eventMessageGroup;
		}

		private string InternFolderId(string folderId)
		{
			if (string.IsNullOrEmpty(folderId))
				return folderId;
			string pooled;
			if (_folderIds.TryGetValue(folderId, out pooled))
				return pooled;
			_folderIds.Add(folderId, folderId);
			return folderId;
		}

		public void Add(EventMessage message)
		{
			lock (_sync)
			{
				message.FolderId = InternFolderId(message.FolderId);
				SetMinMaxInternal(message.Time);
				GetOrCreateGroup(GetDateTime(message.Time), 100).Add(message);
				_last = message;
			}
		}

		private static DateTime GetDateTime(DateTime time)
		{
			return new DateTime(time.Year, time.Month, 01);
		}

		public void AddRange(IEnumerable<EventMessage> messages)
		{
			var groups = messages.GroupBy(x => GetDateTime(x.Time)).ToList();
			lock (_sync)
			{
				foreach (var g in groups)
				{
					var eventMessageGroup = GetOrCreateGroup(g.Key, g.Count());
					foreach (var message in g)
					{
						message.FolderId = InternFolderId(message.FolderId);
						SetMinMaxInternal(message.Time);
						eventMessageGroup.Add(message);
						_last = message;
					}
				}
			}
		}

		public List<EventMessage> GetByDate(DateTime time, int take)
		{
			var result = new List<EventMessage>();
			if (take <= 0)
				return result;

			var date = GetDateTime(time);
			List<EventMessageGroup> groups;
			lock (_sync)
			{
				groups = _group.Where(x => x.Key >= date)
					.OrderBy(x => x.Key)
					.Select(x => x.Value)
					.ToList();
			}

			// Месяцы не пересекаются по времени, поэтому события, взятые по порядку месяцев из отсортированных
			// групп, уже упорядочены. Раньше на каждый вызов перебирались и сортировались все события
			// начиная с нужного месяца, причём под общей блокировкой, останавливавшей запись.
			foreach (var group in groups)
			{
				if (result.Count >= take)
					break;
				group.CopySortedFrom(time, take - result.Count, result);
			}
			return result;
		}

		public List<EventMessage> GetToDate(DateTime time)
		{
			var date = GetDateTime(time);
			lock (_sync)
			{
				return _group.Where(x => x.Key <= date)
					.SelectMany(s => s.Value)
					.Where(v => v.Time < time).ToList();
			}
		}

		public List<EventMessage> GetAll()
		{
			lock (_sync)
			{
				return _group
					.SelectMany(s => s.Value)
					.ToList();
			}
		}

		public List<EventMessage> GetByDate(string id)
		{
			var result = new List<EventMessage>();
			lock (_sync)
			{
				foreach (var group in _group.Values)
				{
					var eventM = group.GetById(id);
					if (eventM != null)
						result.Add(eventM);
				}
			}
			return result;
		}

		public EventMessage GetLast()
		{
			lock (_sync)
			{
				return _last;
			}
		}
	}

	/// <summary>
	/// События одного месяца. Хранится последнее событие по каждой паре «папка + id файла»:
	/// при переносе между папками удаление из старой папки и сохранение в новой не перекрывают друг друга.
	/// </summary>
	public sealed class EventMessageGroup : IEnumerable<EventMessage>
	{
		public EventMessageGroup(DateTime date, int capacity = 100)
		{
			_events = new Dictionary<string, object>(capacity);
			Date = date;
		}
		private readonly object _sync = new object();

		// id файла → последнее событие (EventMessage), а если у id больше одной папки —
		// словарь «папка → последнее событие». У FileStorage папок нет, у FolderStorage почти всегда одна,
		// поэтому отдельный словарь на каждый id не создаётся.
		private readonly Dictionary<string, object> _events;

		// события, отсортированные по времени; сбрасывается при изменении группы
		private EventMessage[] _sorted;

		private int _version;

		public DateTime Date { get; private set; }

		public void Add(EventMessage eventMessage)
		{
			lock (_sync)
			{
				_sorted = null;
				_version++;

				var folder = eventMessage.FolderId ?? string.Empty;
				object current;
				if (!_events.TryGetValue(eventMessage.Id, out current) || folder.Length == 0)
				{
					// Первое событие файла или событие без папки (FileStorage или старые записи журнала
					// FolderStorage): оно относится к файлу целиком, как раньше, когда журнал хранился только по id.
					_events[eventMessage.Id] = eventMessage;
					return;
				}

				var single = current as EventMessage;
				if (single != null)
				{
					var currentFolder = single.FolderId ?? string.Empty;
					if (currentFolder == folder)
					{
						_events[eventMessage.Id] = eventMessage;
						return;
					}
					// вторая папка для того же id — только тогда нужен словарь
					_events[eventMessage.Id] = new Dictionary<string, EventMessage>(2)
					{
						[currentFolder] = single,
						[folder] = eventMessage
					};
					return;
				}

				((Dictionary<string, EventMessage>)current)[folder] = eventMessage;
			}
		}

		public void AddRange(IEnumerable<EventMessage> eventMessages)
		{
			foreach (var ev in eventMessages)
			{
				Add(ev);
			}
		}

		/// <summary>
		/// Последнее по времени событие файла в любой папке
		/// </summary>
		public EventMessage GetById(string id)
		{
			lock (_sync)
			{
				object current;
				if (!_events.TryGetValue(id, out current))
					return null;
				var single = current as EventMessage;
				if (single != null)
					return single;

				EventMessage latest = null;
				foreach (var eventMessage in ((Dictionary<string, EventMessage>)current).Values)
				{
					if (latest == null || eventMessage.Time >= latest.Time)
						latest = eventMessage;
				}
				return latest;
			}
		}

		/// <summary>
		/// Добавляет в result не больше count событий с Time >= time в порядке времени
		/// </summary>
		internal void CopySortedFrom(DateTime time, int count, List<EventMessage> result)
		{
			EventMessage[] sorted;
			int version;
			var needSort = false;
			lock (_sync)
			{
				sorted = _sorted;
				version = _version;
				if (sorted == null)
				{
					// новый снимок принадлежит только этому вызову; общий отсортированный массив не меняется
					sorted = Snapshot();
					needSort = true;
				}
			}

			if (needSort)
			{
				// сортируем вне блокировки, чтобы не задерживать запись; результат запоминаем,
				// только если группа за это время не менялась
				Array.Sort(sorted, (x, y) => x.Time.CompareTo(y.Time));
				lock (_sync)
				{
					if (_version == version)
						_sorted = sorted;
				}
			}

			// бинарный поиск первого события с Time >= time
			int low = 0, high = sorted.Length;
			while (low < high)
			{
				var middle = low + (high - low) / 2;
				if (sorted[middle].Time < time)
					low = middle + 1;
				else
					high = middle;
			}
			for (var i = low; i < sorted.Length && count > 0; i++, count--)
			{
				result.Add(sorted[i]);
			}
		}

		private EventMessage[] Snapshot()
		{
			var list = new List<EventMessage>(_events.Count);
			foreach (var value in _events.Values)
			{
				var single = value as EventMessage;
				if (single != null)
					list.Add(single);
				else
					list.AddRange(((Dictionary<string, EventMessage>)value).Values);
			}
			return list.ToArray();
		}

		public IEnumerator<EventMessage> GetEnumerator()
		{
			// перебираем снимок, чтобы параллельный Add не ломал перечисление
			lock (_sync)
			{
				return ((IEnumerable<EventMessage>)Snapshot()).GetEnumerator();
			}
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}
	}
	public sealed class EventMessage
	{
		public string FolderId { get; set; }

		public string Id { get; set; }

		public DateTime Time { get; set; }

		public EventType Type { get; set; }

		public long Size { get; set; }
	}
}
