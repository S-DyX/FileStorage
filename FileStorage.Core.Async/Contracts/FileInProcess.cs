using System;
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
			Check(key);
			lock (_syncInstance)
			{
				Check(key);
				_process.Add(key, true);
			}
		}
		public void Release(string key)
		{
			if (!_process.ContainsKey(key))
				return;

			lock (_syncInstance)
			{
				if (_process.ContainsKey(key))
					_process.Remove(key);
			}
		}

		private void Check(string key)
		{
			if (_process.ContainsKey(key))
			{
				throw new InvalidOperationException("Key is already used:" + key);
			}
		}


		private readonly object _syncInstance = new object();
		private readonly Dictionary<string, bool> _process = new Dictionary<string, bool>();
	}
}
