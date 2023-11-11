//using System;
//using System.Collections.Concurrent;
//using System.Collections.Generic;
//using System.Configuration;
//using System.Linq;
//using System.Net;
//using System.Text;
//using System.Threading.Tasks;
//using FileStorage.Core.Entities;
//using FileStorage.Core.Interfaces;
//using Service.Registry.Common;

//namespace FileStorage.Contracts.Impl
//{
//	public class FileStorageSyncServiceModule : IFileStorageSyncServiceModule
//	{
//		private readonly IFileStorageService _fileStorageService;

//		private readonly IServiceRegistryFactory _serviceRegistryFactory;
//		private readonly ILogger _logger;
//		private readonly IFileStorageApiProxy _fileStorageApiProxy;
//		private readonly bool _isMainNode;
//		private bool _isAllLoad;
//		private readonly object _synAllLoad = new object();

//		public FileStorageSyncServiceModule(IFileStorageService fileStorageService,
//			IServiceRegistryFactory serviceRegistryFactory,
//			IFileStorageApiProxy fileStorageApiProxy,
//			ILogger logger)
//		{
//			_fileStorageService = fileStorageService;

//			_serviceRegistryFactory = serviceRegistryFactory;

//			_logger = logger;
//			_fileStorageApiProxy = fileStorageApiProxy;
//			_isMainNode = true;
//			//var mainNode = ConfigurationManager.AppSettings["IsMainNode"];
//			//if (bool.TryParse(mainNode, out _isMainNode))
//			//{

//			//}
//		}

//		private readonly Dictionary<string, DateTime> _lastUpdate = new Dictionary<string, DateTime>();
//		private readonly Dictionary<string, DateTime> _lastUpload = new Dictionary<string, DateTime>();

//		public void DownloadFiles()
//		{
//			return;
//			lock (_synAllLoad)
//			{
//				try
//				{

//					{
//						string name;
//						if (StopProcess(out name))
//						{
//							return;
//						}


//						var lastUpdate = _lastUpdate;

//						FileLogData data;
//						do
//						{
//							var proxy = _serviceRegistryFactory.CreateProxy<IWcfFileStorageService>();
//							data = SyncFiles(lastUpdate, name, proxy, _fileStorageService, null/*LoadFileFromApi*/);

//							//_fileStorageLog.Flush();
//							_logger.Info($"Last Date: {lastUpdate[name]}");
//							//);
//							Parallel.ForEach(data.DeletedIds, id =>
//							//foreach (var id in data.Ids)
//							{
//								var size = proxy.GetSize(id);
//								if (_fileStorageService.Exists(id, null))
//								{
//									if (size > 0 && _fileStorageService.GetSize(id, null) == size)
//									{
//										_logger.Info($"Same size: {id}");
//									}
//									else
//									{
//										_fileStorageService.Delete(id, null);
//										_logger.Info($"Deleted: {id}");
//									}

//								}
//							});
//						} while (data?.Files?.Count > 10);

//					}
//				}
//				catch (Exception ex)
//				{
//					_logger.Error(ex.Message, ex);
//					throw;
//				}
//			}
//		}


//		public void DownloadAllFiles()
//		{
//			return;
//			if (_isAllLoad || _isMainNode)
//				return;
//			lock (_synAllLoad)
//			{
//				if (_isAllLoad || _isMainNode)
//					return;

//				_logger.Info($"Start load All");
//				int offset = 0;
//				int totalCount = 0;
//				try
//				{
//					var proxy = _serviceRegistryFactory.CreateProxy<IWcfFileStorageService>();
//					var dict = new Dictionary<string, string>();
//					var count = 20000;
//					List<string> ids;
//					do
//					{
//						ids = proxy.GetIds(offset, count);
//						foreach (var id in ids)
//						{
//							if (!dict.ContainsKey(id))
//								dict.Add(id, id);
//							else
//							{
//								_logger.Info("Not unique key :" + id);
//							}
//						}
//						var distinct = ids.Distinct().ToList();
//						Parallel.ForEach(distinct, id =>
//						{
//							if (!_fileStorageService.Exists(id, null))
//							{
//								_logger.Info("All Load:" + id);
//								FileInProcess.Instance.WaitWhileIsLock(id);
//								LoadFileFromApi(id, null/*_fileStorageService*/);
//							}
//						});
//						offset += ids.Count;
//						totalCount += distinct.Count;

//					} while (ids.Count >= count);
//				}
//				catch (Exception ex)
//				{
//					_logger.Error(ex.Message, ex);
//				}

//				_logger.Info($"All load count is {offset} unique {totalCount}");
//				_isAllLoad = true;
//			}
//		}

//		private FileLogData SyncFiles(Dictionary<string, DateTime> lastUpdate,
//			string name,
//			IWcfFileStorageService fromFilestorage,
//			IFileStorageService toFilestorage,
//			Action<string, IFileStorageService> load)
//		{
//			var date = DateTime.UtcNow.AddMonths(-6);
//			if (lastUpdate.ContainsKey(name))
//			{
//				date = lastUpdate[name];
//			}
//			else
//			{
//				lastUpdate.Add(name, date);
//			}
//			_logger.Info($"Last Date:{date}, {name}");

//			var data = fromFilestorage.GetIdsByDate(date, 30000);
//			_logger.Info($"Count:{data.Files.Count}, MaxDate: {data.MaxDate}");

//			var datas = data.Files.OrderBy(x => x.Time).ToList();
//			var wasLoaded = new ConcurrentBag<string>();
//			var ids = datas.Select(x => x.Id).Distinct().ToList();
//			Parallel.ForEach(ids, id =>
//			//foreach (var item in datas)
//			{

//				try
//				{
//					if (id != null)
//					{
//						FileInProcess.Instance.WaitWhileIsLock(id);
//						if (!toFilestorage.Exists(id, null))
//						{
//							load.Invoke(id, toFilestorage);
//						}
//						else if (toFilestorage.GetSize(id, null) != fromFilestorage.GetSize(id))
//						{
//							_logger.Info($"Exists:{id}");
//							toFilestorage.Delete(id, null);
//							load.Invoke(id, toFilestorage);
//						}
//						else
//							_logger.Debug($"Exists:{id}");
//						wasLoaded.Add(id);
//					}
//				}
//				catch (Exception ex)
//				{
//					_logger.Error(id, ex);
//					throw;
//				}
//			});

//			var dateTime = datas.Max(x => x.Time);
//			if (wasLoaded.Count == ids.Count && lastUpdate[name] < dateTime)
//			{
//				lastUpdate[name] = dateTime;
//			}
//			return data;
//		}

//		private bool StopProcess(out string name)
//		{
//			bool stopProcess = false;
//			using (var client = _serviceRegistryFactory.CreateClient<IWcfFileStorageService>())
//			{
//				name = client.ChannelFactory.Endpoint.Address.ToString();
//				var host = Dns.GetHostEntry(Dns.GetHostName());
//				_logger.Info($"Address:{name}");
//				foreach (IPAddress ip in host.AddressList)
//				{
//					if (_isMainNode)
//						stopProcess = true;

//					if (name.ToLower().Contains(ip.AddressFamily.ToString().ToLower()))
//					{
//						stopProcess = true;
//					}
//				}
//			}
//			return stopProcess;
//		}

//		public void CheckDiff()
//		{
//			return;
//			try
//			{
//				{
//					string name;
//					if (StopProcess(out name))
//					{
//						return;
//					}


//					var proxy = _serviceRegistryFactory.CreateProxy<IWcfFileStorageService>();
//					int offset = 0;
//					int count = 10000;

//					List<string> data;
//					do
//					{
//						data = proxy.GetIds(offset, count);
//						foreach (var id in data)
//						{
//							if (!_fileStorageService.Exists(id, null))
//								_logger.Info("Not found id:" + id);
//						}
//						offset += count;
//					} while (data.Any());
//					//var data = SyncFiles(lastUpdate, name, _fileStorageService, proxy, LoadFileFromLocal);




//				}
//			}
//			catch (Exception ex)
//			{
//				_logger.Error(ex.Message, ex);
//				throw;
//			}


//		}
//		public void UploadFiles()
//		{
//			try
//			{
//				{
//					string name;
//					if (StopProcess(out name))
//					{
//						return;
//					}

//					var lastUpdate = _lastUpload;
//					var proxy = _serviceRegistryFactory.CreateProxy<IWcfFileStorageService>();

//					//var data = SyncFiles(lastUpdate, name, _fileStorageService, proxy, LoadFileFromLocal);

//					_logger.Info($"Last Date: {lastUpdate[name]}");


//				}
//			}
//			catch (Exception ex)
//			{
//				_logger.Error(ex.Message, ex);
//				throw;
//			}


//		}
//		private void LoadFileFromApi(string id, IWcfFileStorageService toFromFileStorageService)
//		{
//			const int constSize = 100000;
//			_logger.Info($"Start Load:{id}");
//			long offset = 0;

//			int size = constSize;
//			bool isOk = true;
//			int len = 0;
//			using (var batch = new FileStorageBatchPersistById(toFromFileStorageService, id))
//			{
//				byte[] bytes;
//				do
//				{
//					_logger.Info($"Load:{id},{offset},{size}");
//					if (isOk && size != constSize)
//					{
//						size = constSize;
//					}
//					try
//					{
//						if (!_fileStorageApiProxy.IsExists(id))
//						{
//							batch.Close();
//							return;
//						}

//						bytes = _fileStorageApiProxy.GetBytes(id, offset, size);
//						if (bytes == null)
//							return;
//						batch.Write(bytes, bytes.Length, bytes?.Length != size);
//						len += bytes.Length;
//					}
//					catch (Exception ex)
//					{
//						isOk = false;
//						_logger.Error(ex.Message, ex);
//						size = size / 2;
//						if (size == 0)
//						{
//							size = 1000;
//						}
//						bytes = new byte[size];
//						continue;
//					}

//					isOk = true;


//					offset += size;
//				} while (bytes.Length == size);

//			}

//			//if (!_fileStorageLog.IsExists(id))
//			//{
//			//	_logger.Info($"not exists on log:{id}");
//			//	_fileStorageLog.Write(new EventMessage()
//			//	{
//			//		Time = DateTime.UtcNow,
//			//		Id = id,
//			//		Type = EventType.FileSave,
//			//		Size = len
//			//	});
//			//}
//			_logger.Info($"End Load:{id}");
//		}

//		private void LoadFileFromLocal(string id, IWcfFileStorageService toFileStorageService)
//		{
//			const int constSize = 100000;

//			_logger.Info($"Start Upload:{id}");
//			int offset = 0;

//			int size = constSize;
//			bool isOk = true;
//			using (var stream = new FileStorageBatchPersistById(toFileStorageService, id))
//			{
//				byte[] bytes;
//				do
//				{
//					_logger.Info($"Upload:{id},{offset},{size}");
//					if (isOk && size != constSize)
//					{
//						size = constSize;
//					}
//					try
//					{
//						bytes = _fileStorageService.GetBytesOffset(id, offset, size, null);
//						if (bytes == null)
//							return;
//						stream.Write(bytes, bytes.Length, bytes?.Length != size);
//					}
//					catch (Exception ex)
//					{
//						isOk = false;
//						_logger.Error(ex.Message, ex);
//						size = size / 2;
//						if (size == 0)
//						{
//							size = 1000;
//						}
//						bytes = new byte[size];
//						continue;
//					}

//					isOk = true;


//					offset += size;
//				} while (bytes.Length == size);

//			}


//			_logger.Info($"End Upload:{id}");
//		}
//	}
//}
