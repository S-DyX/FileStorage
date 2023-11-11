using System;
using System.Collections.Generic;
using System.IO;
using FileStorage.Contracts;

namespace FileStorage.Core.Contracts
{
	public interface IFolderStorageService
	{

		Stream GetStream(FolderStorageInfo info);


		FileInfoResponse SaveStream(Stream stream, FolderStorageInfo info);


		byte[] GetBytes(FolderStorageInfo info);


		


		FileInfoResponse SaveBytesByExternal(byte[] bytes, string folderExternalId, string fileExternalId, string storageName);


		byte[] GetBytesByExternal(string folderExternalId, string fileExternalId, string storageName);


		string GetExistIdByExternal(string folderExternalId, string fileExternalId, string storageName);

		string GetIdByExternal(string externalId);


		string GetFullNameByExternal(string folderExternalId, string fileExternalId, string storageName);


		string GetFullName(FolderStorageInfo info);



		void RefreshLog(string storageName);


		FileLogData GetIdsByDate(DateTime time, int take, string storageName);


		List<string> GetIds(long offset, int count, string storageName);


		DateTime? GetLogDateById(FolderStorageInfo info);


		long GetCount(string storageName);


		long GetSize(FolderStorageInfo info);


		FileInfoResponse SaveById(FileBatchRequest request);


		byte[] GetBytesOffset(FolderStorageInfo info, long offset, int size);


		void Delete(FolderStorageInfo info);


		bool Exists(FolderStorageInfo info);


		void Move(string toFolderId, string fromFolderId, string fromFileId, string storageName);


		void DeleteTemp(FolderStorageInfo info);


		void ClearTtl(DateTime date, string storageName);
		Stream GetStreamExternalId(string folderExternalId, string fileExternalId, string storageName);

		void ChangeDepthDirectory(int newDepth, string storageName);

		bool IsExistsByExternalId(string folderExternalId, string fileExternalId, string storageName);

        //FileInfoResponse SaveBytes(byte[] bytes, FolderStorageInfo info);

		//void SaveBytes(FolderStorageInfo info, byte[] bytes, int offset, int size);
	}
}
