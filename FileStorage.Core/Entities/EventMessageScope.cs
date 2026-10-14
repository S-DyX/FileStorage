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

	public sealed class EventMessageGroup : IEnumerable<EventMessage>
	{
		public EventMessageGroup(DateTime date, int capacity = 100)
		{
			_eventsDictionary = new Dictionary<string, EventMessage>(capacity);
			Date = date;
		}
		private readonly object _sync = new object();

		public DateTime Date { get; private set; }

		public void Add(EventMessage eventMessage)
		{
			lock (_sync)
			{
				_eventsDictionary[eventMessage.Id] = eventMessage;
			}
		}

		public void AddRange(IEnumerable<EventMessage> eventMessages)
		{
			foreach (var ev in eventMessages)
			{
				Add(ev);
			}
		}

		public EventMessage GetById(string id)
		{
			lock (_sync)
			{
				EventMessage eventMessage;
				return _eventsDictionary.TryGetValue(id, out eventMessage) ? eventMessage : null;
			}
		}

		private readonly Dictionary<string, EventMessage> _eventsDictionary;

		public IEnumerator<EventMessage> GetEnumerator()
		{
			// перебираем снимок, чтобы параллельный Add не ломал перечисление
			lock (_sync)
			{
				return _eventsDictionary.Values.ToList().GetEnumerator();
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
