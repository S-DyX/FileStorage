using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FileStorage.Core.Interfaces;

namespace FileStorage.Core.Entities
{
	public sealed class EventMessageScope
	{
		public DateTime _minTime { get; protected set; }
		public DateTime _maxTime { get; protected set; }


		public EventMessageScope()
		{
			_minTime = DateTime.MaxValue;
			_maxTime = DateTime.MinValue;
			_group = new Dictionary<DateTime, EventMessageGroup>();
		}

		private readonly Dictionary<DateTime, EventMessageGroup> _group;

		private EventMessage _last;

		private readonly object _sync = new object();

		public void SetMinMax(DateTime dateTime)
		{
			if (dateTime > DateTime.UtcNow)
				return;
			if (_minTime > dateTime)
				_minTime = dateTime;
			if (_maxTime < dateTime)
				_maxTime = dateTime;
		}

		public void Add(EventMessage message)
		{
			SetMinMax(message.Time);
			var dateTime = GetDateTime(message.Time);

			EventMessageGroup eventMessageGroup;
			if (!_group.ContainsKey(dateTime))
			{
				lock (_sync)
				{
					if (!_group.ContainsKey(dateTime))
					{
						eventMessageGroup = new EventMessageGroup(dateTime);
						_group.Add(dateTime, eventMessageGroup);

					}
					else
					{
						eventMessageGroup = _group[dateTime];
					}
				}
			}
			else
			{
				eventMessageGroup = _group[dateTime];
			}
			eventMessageGroup.Add(message);
			_last = message;
		}

		private static DateTime GetDateTime(DateTime time)
		{
			return new DateTime(time.Year, time.Month, 01);
		}

		public void AddRange(IEnumerable<EventMessage> messages)
		{
			foreach (var message in messages)
			{
				Add(message);
			}
		}

		public List<EventMessage> GetByDate(DateTime time, int take)
		{
			var date = GetDateTime(time);
			var eventMessages = _group.Where(x => x.Key >= date).SelectMany(s => s.Value).Where(v => v.Time >= time).ToList();
			return eventMessages.OrderBy(x => x.Time).Take(take).ToList();
		}

		public List<EventMessage> GetToDate(DateTime time)
		{
			var date = GetDateTime(time);
			return _group.Where(x => x.Key <= date).SelectMany(s => s.Value).Where(v => v.Time < time).ToList();
			
		}

		public List<EventMessage> GetByDate(string id)
		{
			var result = new List<EventMessage>();
			foreach (var group in _group.Values)
			{
				var eventM = group.GetById(id);
				if (eventM != null)
					result.Add(eventM);
			}
			return result;
		}

		public EventMessage GetLast()
		{
			return _last;
		}
	}

	public sealed class EventMessageGroup : IEnumerable<EventMessage>
	{
		public EventMessageGroup(DateTime date)
		{
			_eventsDictionary = new Dictionary<string, EventMessage>(10000000);
			Date = date;
		}
		private readonly object _sync = new object();

		public DateTime Date { get; private set; }

		public void Add(EventMessage eventMessage)
		{
			lock (_sync)
			{
				if (!_eventsDictionary.ContainsKey(eventMessage.Id))
					_eventsDictionary.Add(eventMessage.Id, eventMessage);
				else
				{
					_eventsDictionary[eventMessage.Id] = eventMessage;
				}
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
			if (_eventsDictionary.ContainsKey(id))
			{
				return _eventsDictionary[id];
			}
			return null;
		}

		private readonly Dictionary<string, EventMessage> _eventsDictionary;

		public IEnumerator<EventMessage> GetEnumerator()
		{
			return _eventsDictionary.Values.GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}
	}
	public sealed class EventMessage
	{
		public string Id { get; set; }

		public DateTime Time { get; set; }

		public EventType Type { get; set; }

		public long Size { get; set; }
	}
}
