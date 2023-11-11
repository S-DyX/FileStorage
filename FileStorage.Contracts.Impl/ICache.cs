using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FileStorage.Contracts.Impl
{
	public interface ICache<TKey, TValue>
	{
		TValue this[TKey key] { get; set; }
		List<TValue> GetExists(List<TKey> keys);
		List<TValue> GetExists(List<TKey> keys, out List<TKey> notExists);

		void Update(List<TValue> values, Func<TValue, TKey> func);

		void Clear();
	}

	public interface ICache<TValue> : ICache<string, TValue>
	{
	}

	public class Cache<TValue> : Cache<string, TValue>, ICache<TValue> where TValue : class
	{
	}

	public class Cache<TKey, TValue> : ICache<TKey, TValue> where TValue : class
	{
		private readonly int _minutes;
		private readonly MemoryCache _cache;
		private readonly MemoryCacheEntryOptions _policy;
		public Cache()
			: this(1)
		{
		}

		public Cache(int minutes)
		{
			_minutes = minutes;
			_cache = new MemoryCache(new MemoryCacheOptions() { });
			_policy = new MemoryCacheEntryOptions();
			_policy.SetAbsoluteExpiration(TimeSpan.FromMinutes(minutes));
		}

		public TValue this[TKey key]
		{
			get
			{
				return _cache.Get<TValue>(key.ToString());
			}
			set
			{
				_policy.AbsoluteExpiration = new DateTimeOffset(DateTime.Now.AddMinutes(_minutes));
				_cache.Set(key.ToString(), value, _policy);
			}
		}

		public List<TValue> GetExists(List<TKey> keys)
		{
			var result = new List<TValue>(keys.Count);
			foreach (var key in keys)
			{
				var value = _cache.Get<TValue>(key.ToString());
				if (value != null)
				{
					result.Add(value);
				}
			}

			return result;
		}

		public List<TValue> GetExists(List<TKey> keys, out List<TKey> notExists)
		{
			notExists = new List<TKey>();
			var result = new List<TValue>(keys.Count);
			foreach (var key in keys)
			{
				var value = _cache.Get<TValue>(key.ToString());
				if (value != null)
				{
					result.Add(value);
				}
				else
				{
					notExists.Add(key);
				}
			}

			return result;
		}

		public void Update(List<TValue> values, Func<TValue, TKey> func)
		{
			foreach (var item in values)
			{
				var key = func.Invoke(item);
				this[key] = item;
			}
		}

		public void Clear()
		{
			_cache.Compact(100);
		}
	}
}
