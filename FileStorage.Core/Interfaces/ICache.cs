using System;
using System.Collections.Generic;
using Microsoft.Extensions.Caching.Memory;

namespace FileStorage.Core.Interfaces
{
	public interface ICache<TKey, TValue>
	{
		/// <summary>
		/// Get value by key
		/// </summary>
		/// <param name="key"></param>
		/// <returns></returns>
		TValue this[TKey key] { get; set; }

		/// <summary>
		/// Is exists
		/// </summary>
		/// <param name="key"></param>
		/// <returns></returns>
		bool IsExists(TKey key);

		/// <summary>
		/// Try get value
		/// </summary>
		/// <param name="key"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		bool TryGetValue(TKey key, out TValue value);

		/// <summary>
		/// Получаем значения по массиву ключений
		/// </summary>
		/// <param name="keys"></param>
		/// <returns></returns>
		List<TValue> GetExists(List<TKey> keys);
		List<TValue> GetExists(List<TKey> keys, out List<TKey> notExists);

		/// <summary>
		/// Обновляем значения, а так же передаем функцию вычисления ключа по занчению
		/// </summary>
		/// <param name="values"></param>
		/// <param name="func"></param>
		void Update(List<TValue> values, Func<TValue, TKey> func);

		/// <summary>
		/// Устанивливаем значение по ключу
		/// </summary>
		/// <param name="key"></param>
		/// <param name="value"></param>
		void SetValue(TKey key, TValue value);

		/// <summary>
		/// Удаляем значение по ключу
		/// </summary>
		/// <param name="key"></param>
		void Delete(TKey key);

		/// <summary>
		/// Устанавливаем значение по ключу с временем истечения
		/// </summary>
		/// <param name="key"></param>
		/// <param name="value"></param>
		/// <param name="seconds"></param>
		void SetValue(TKey key, TValue value, int seconds);

		/// <summary>
		/// Clear
		/// </summary>
		void Clear();

		/// <summary>
		/// Count of elements
		/// </summary>
		/// <returns></returns>
		int Count();
	}

	public interface ICache<TValue> : ICache<string, TValue>
	{
	}


	/// <inheritdoc />
	public class Cache<TKey, TValue> : ICache<TKey, TValue> where TValue : class
	{
		private readonly int _seconds;
		private readonly MemoryCache _cache;
		private readonly MemoryCacheEntryOptions _policy;

		/// <summary>
		/// Ctor
		/// </summary>
		public Cache()
			: this(2)
		{
		}

		/// <summary>
		/// Ctor
		/// </summary>
		/// <param name="seconds"></param>
		public Cache(int seconds)
		{
			_seconds = seconds;
			_cache = new MemoryCache(new MemoryCacheOptions() { });
			_policy = new MemoryCacheEntryOptions();
			_policy.SetAbsoluteExpiration(TimeSpan.FromSeconds(_seconds));
		}
		/// <inheritdoc />
		public TValue this[TKey key]
		{
			get
			{
				var value = _cache.Get<TValue>(key);
				return value;
			}
			set { SetValue(key, value); }
		}
		/// <inheritdoc />
		public void SetValue(TKey key, TValue value)
		{
			var seconds = _seconds;
			SetValue(key, value, seconds);
		}

		/// <inheritdoc />
		public void Delete(TKey key)
		{
			// Метод Set в строчке _cache.Set(key, default(TValue), policy) создает ключ в кеше, поэтому в данном случае он не выполняет то что от него 
			// ожидается. Правильно сразу вызвать _cache.Remove(key), так как там выполняется безопасное удаление.
			// var policy = new MemoryCacheEntryOptions();
			// policy.SetAbsoluteExpiration(TimeSpan.FromSeconds(1));
			// policy.AbsoluteExpiration = new DateTimeOffset(DateTime.Now.AddSeconds(1));
			// _cache.Set(key, default(TValue), policy);
			_cache.Remove(key);
		}

		/// <inheritdoc />
		public void SetValue(TKey key, TValue value, int seconds)
		{
			if (value == null)
			{
				Delete(key);
				return;
			}

			var policy = new MemoryCacheEntryOptions();
			policy.SetAbsoluteExpiration(TimeSpan.FromSeconds(seconds));
			policy.AbsoluteExpiration = new DateTimeOffset(DateTime.Now.AddSeconds(seconds));
			_cache.Set(key, value, policy);
		}

		/// <inheritdoc />
		public bool IsExists(TKey key)
		{
			var value = _cache.Get(key);
			return (value != null);
		}

		/// <inheritdoc />
		public List<TValue> GetExists(List<TKey> keys)
		{
			var result = new List<TValue>(keys.Count);
			foreach (var key in keys)
			{
				var value = _cache.Get<TValue>(key);
				if (value != null)
				{
					result.Add(value);
				}
			}

			return result;
		}

		/// <inheritdoc />
		public List<TValue> GetExists(List<TKey> keys, out List<TKey> notExists)
		{
			notExists = new List<TKey>();
			var result = new List<TValue>(keys.Count);
			foreach (var key in keys)
			{
				var value = _cache.Get<TValue>(key);
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

		/// <inheritdoc />
		public void Update(List<TValue> values, Func<TValue, TKey> func)
		{
			foreach (var item in values)
			{
				var key = func.Invoke(item);
				this[key] = item;
			}
		}

		/// <inheritdoc />
		public void Clear()
		{
			_cache.Compact(100);
		}

		/// <inheritdoc />
		public int Count()
		{
			return _cache.Count;
		}

		/// <inheritdoc />
		public bool TryGetValue(TKey key, out TValue value)
		{
			return _cache.TryGetValue(key, out value);
		}
	}
	/// <inheritdoc cref="ICache{TValue}"/>
	public class Cache<TValue> : Cache<string, TValue>, ICache<TValue> where TValue : class
	{
	}
}
