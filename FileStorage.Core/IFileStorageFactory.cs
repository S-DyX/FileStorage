using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using FileStorage.Core.Interfaces;
using FileStorage.Core.Interfaces.Settings;

namespace FileStorage.Core
{
	/// <summary>
	/// File storage factory
	/// </summary>
	/// <typeparam name="TValue"></typeparam>
	public interface IFileStorageFactory<TValue>
	{
		/// <summary>
		/// Create new file storage if not exists
		/// </summary>
		/// <param name="name"></param>
		/// <returns></returns>
		IFileStorage<TValue> Create(string name);

		/// <summary>
		/// Return exists file storage
		/// </summary>
		/// <param name="name"></param>
		/// <returns></returns>
		IFileStorage<TValue> Get(string name);

		List<IFileStorage<TValue>> GetAll();

		List<TValue> GetNames();
	}

	/// <summary>
	/// <see cref="IFileStorageFactory{TValue}"/>
	/// </summary>
	public sealed class FileStorageFactory : IFileStorageFactory<string>, IDisposable
	{
		private readonly IFileStorageSettings _fileStorageSettings;
		private readonly IFileStorageVirtual _fileStorageVirtual;
		private readonly ILocalLogger _localLogger;
		// все обращения к словарю — под _sync: Dictionary не потокобезопасен даже для чтения во время записи
		private readonly Dictionary<string, IFileStorage<string>> _fileStorages = new Dictionary<string, IFileStorage<string>>();
		private readonly object _sync = new object();

		public FileStorageFactory(IFileStorageSettings fileStorageSettings, IFileStorageVirtual fileStorageVirtual, ILocalLogger localLogger)
		{
			_fileStorageSettings = fileStorageSettings;
			_fileStorageVirtual = fileStorageVirtual;
			_localLogger = localLogger;
		}

		/// <summary>
		/// <see cref="IFileStorageFactory{TValue}.Create(string)"/>
		/// </summary>
		/// <param name="name">file storage name</param>
		/// <returns><see cref="IFileStorage{TValue}"/></returns>
		public IFileStorage<string> Create(string name)
		{
			var key = name;
			if (string.IsNullOrWhiteSpace(key))
				key = @default;

			lock (_sync)
			{
				IFileStorage<string> fileStorage;
				if (_fileStorages.TryGetValue(key, out fileStorage))
					return fileStorage;

				fileStorage = new FileStorage(key, _fileStorageSettings, _fileStorageVirtual, _localLogger);
				_fileStorages[key] = fileStorage;
				return fileStorage;
			}
		}
		private static string @default = "Default";

		/// <summary>
		/// <see cref="IFileStorageFactory{TValue}.Get(string)"/>
		/// </summary>
		/// <param name="name">file storage name</param>
		/// <returns><see cref="IFileStorage{TValue}"/></returns>
		public IFileStorage<string> Get(string name)
		{
			// как и раньше, несуществующее хранилище создаётся
			return Create(name);
		}

		public List<IFileStorage<string>> GetAll()
		{
			lock (_sync)
			{
				return _fileStorages.Values.ToList();
			}
		}

		public List<string> GetNames()
		{
			lock (_sync)
			{
				return _fileStorages.Keys.ToList();
			}
		}

		/// <summary>
		/// Останавливает журналы всех созданных хранилищ (таймеры, несохранённые события)
		/// </summary>
		public void Dispose()
		{
			lock (_sync)
			{
				foreach (var fileStorage in _fileStorages.Values)
				{
					(fileStorage as IDisposable)?.Dispose();
				}
				_fileStorages.Clear();
			}
		}
	}
}
