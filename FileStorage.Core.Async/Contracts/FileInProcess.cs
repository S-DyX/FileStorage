using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FileStorage.Contracts
{
	public sealed class FileInProcess
	{
		private FileInProcess()
		{
		}
		private static object _sync = new object();
		private static FileInProcess _instance;
		public static FileInProcess Instance
		{
			get
			{
				if (_instance == null)
				{
					lock (_sync)
					{
						if (_instance == null)
						{
							_instance = new FileInProcess();
						}
					}
				}
				return _instance;
			}
		}
		public bool IsLock(string key)
		{
			return _process.ContainsKey(key);
		}

		public void Register(string key)
		{
			// TryAdd атомарен: два потока не смогут зарегистрировать один ключ
			if (!_process.TryAdd(key, true))
			{
				throw new InvalidOperationException("Key is already used:" + key);
			}
		}
		public void Release(string key)
		{
			_process.TryRemove(key, out _);
		}

		private readonly ConcurrentDictionary<string, bool> _process = new ConcurrentDictionary<string, bool>();
	}
}
