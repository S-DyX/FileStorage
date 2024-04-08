﻿using System;
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
	public sealed class FileStorageFactory : IFileStorageFactory<string>
	{
		private readonly IFileStorageSettings _fileStorageSettings;
		private readonly IFileStorageVirtual _fileStorageVirtual;
		private readonly ILocalLogger _localLogger;
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

			if (_fileStorages.ContainsKey(key))
				return _fileStorages[key];

			lock (_sync)
			{
				if (_fileStorages.ContainsKey(key))
					return _fileStorages[key];

				var fileStorage = new FileStorage(key, _fileStorageSettings, _fileStorageVirtual, _localLogger);
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
			var key = name;
			if (string.IsNullOrWhiteSpace(key))
				key = @default;


			if (_fileStorages.ContainsKey(key))
				return _fileStorages[key];
			var directory = FileStorage.GetRoot(_fileStorageSettings, name);

			
				return Create(name);

			//return Create(@default);
		}

		public List<IFileStorage<string>> GetAll()
		{
			return _fileStorages.Values.ToList();
		}

		public List<string> GetNames()
		{
			return _fileStorages.Keys.ToList();
		}
	}
}
