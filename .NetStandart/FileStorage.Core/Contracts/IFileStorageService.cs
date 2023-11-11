using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileStorage.Contracts
{
	public interface IFileStorageService
	{

		Stream GetStream(string id, string storageName);


		FileInfoResponse SaveStream(Stream stream, string storageName);


		byte[] GetBytes(string id, string storageName);


		FileInfoResponse SaveBytes(byte[] bytes, string storageName);


		FileInfoResponse SaveBytesByExternal(byte[] bytes, string externalId, string storageName);


		byte[] GetBytesByExternal(string externalId, string storageName);


		string GetExistIdByExternal(string externalId, string storageName);

		string GetIdByExternal(string externalId);


		string GetFullNameByExternal(string externalId, string storageName);


		string GetFullName(string id, string storageName);



		void RefreshLog(string storageName);


		FileLogData GetIdsByDate(DateTime time, int take, string storageName);


		List<string> GetIds(long offset, int count, string storageName);


		DateTime? GetLogDateById(string id, string storageName);


		long GetCount(string storageName);


		long GetSize(string id, string storageName);


		FileInfoResponse SaveById(FileBatchRequest request);


		byte[] GetBytesOffset(string id, long offset, int size, string storageName);


		void Delete(string id, string storageName);


		bool Exists(string hash, string storageName);


		void Move(string fromId, string toId, string storageName);


		void DeleteTemp(string id, string storageName);


		void ClearTtl(DateTime date, string storageName);
		Stream GetStreamExternalId(string externalId, string storageName);

		void ChangeDepthDirectory(int oldDepth, int newDepth, string storageName);

		bool IsExistsByExternalId(string externalId, string storageName);

		FileInfoResponse SaveStream(Stream input, string id, string storageName);


		void SaveBytes(string id, byte[] bytes, int offset, int size, string storageName);
	}
}
