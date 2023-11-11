using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileStorage.Contracts
{ 

	public interface IWcfFileStorageService
	{

		Stream GetStream(string id);


		Stream GetStreamNamed(string id, string storageName);



		FileInfoResponse SaveStream(Stream stream);


		byte[] GetBytes(string id);


		byte[] GetBytesNamed(string id, string storageName);


		FileInfoResponse SaveBytes(byte[] bytes);


		FileInfoResponse SaveBytesByExternal(byte[] bytes, string externalId);


		byte[] GetBytesByExternal(string externalId);


		string GetExistIdByExternal(string externalId);


		string GetIdByExternal(string externalId);


		string GetFullNameByExternal(string externalId);


		string GetFullName(string id);



		void RefreshLog();


		FileLogData GetIdsByDate(DateTime time, int take);


		List<string> GetIds(long offset, int count);


		DateTime? GetLogDateById(string id);


		long GetCount();


		long GetSize(string id);


		FileInfoResponse SaveByName(string name, byte[] bytes, bool close);

		FileInfoResponse SaveById(string id, byte[] bytes, bool close);


		FileInfoResponse SaveByIdNamed(string id, byte[] bytes, bool close, string storageName);


		byte[] GetBytesOffset(string id, long offset, int size);

		void Delete(string id);

		bool Exists(string hash);



		bool ExistsNamed(string hash, string storageName);



		void Move(string fromId, string toId);


		void DeleteTemp(string id);


		void ClearTtl(DateTime date);


		FileInfoResponse SaveByBatchRequest(FileBatchRequest request);


	}




}
