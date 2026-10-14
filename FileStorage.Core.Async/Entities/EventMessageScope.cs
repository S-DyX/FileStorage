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
	/// Все обращения к словарям идут под одной блокировкой: класс используется из нескольких потоков
	/// (запись событий, таймеры Flush/Check, параллельный ClearTtl, чтение журнала).
	/// </summary>
	public sealed class EventMessageScope
	{
		private readonly object _sync = new object();

		private readonly Dictionary<DateTime, EventMessageGroup> _group;

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

		public void Add(EventMessage message)
		{
			lock (_sync)
			{
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
						SetMinMaxInternal(message.Time);
						eventMessageGroup.Add(message);
						_last = message;
					}
				}
			}
		}

		public List<EventMessage> GetByDate(DateTime time, int take)
		{
			var date = GetDateTime(time);
			lock (_sync)
			{
				return _group.Where(x => x.Key >= date)
					.SelectMany(s => s.Value)
					.Where(v => v.Time >= time)
					.OrderBy(x => x.Time)
					.Take(take)
					.ToList();
			}
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
			_events = new Dictionary<string, Dictionary<string, EventMessage>>(capacity);
			Date = date;
		}
		private readonly object _sync = new object();

		// id файла → (папка → последнее событие); у FileStorage папка всегда пустая
		private readonly Dictionary<string, Dictionary<string, EventMessage>> _events;

		public DateTime Date { get; private set; }

		public void Add(EventMessage eventMessage)
		{
			lock (_sync)
			{
				var folder = eventMessage.FolderId ?? string.Empty;
				Dictionary<string, EventMessage> byFolder;
				if (!_events.TryGetValue(eventMessage.Id, out byFolder))
				{
					byFolder = new Dictionary<string, EventMessage>(1);
					_events.Add(eventMessage.Id, byFolder);
				}
				else if (folder.Length == 0)
				{
					// Событие без папки (FileStorage или старые записи журнала FolderStorage) относится к файлу целиком —
					// как раньше, когда журнал хранился только по id.
					byFolder.Clear();
				}
				byFolder[folder] = eventMessage;
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
				Dictionary<string, EventMessage> byFolder;
				if (!_events.TryGetValue(id, out byFolder))
					return null;
				EventMessage latest = null;
				foreach (var eventMessage in byFolder.Values)
				{
					if (latest == null || eventMessage.Time >= latest.Time)
						latest = eventMessage;
				}
				return latest;
			}
		}

		public IEnumerator<EventMessage> GetEnumerator()
		{
			// перебираем снимок, чтобы параллельный Add не ломал перечисление
			lock (_sync)
			{
				return _events.Values.SelectMany(x => x.Values).ToList().GetEnumerator();
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
