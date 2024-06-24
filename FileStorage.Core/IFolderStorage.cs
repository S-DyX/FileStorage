using System;
using System.Collections.Generic;
using System.IO;
using FileStorage.Core.Contracts;
using FileStorage.Core.Entities;
using FileStorage.Core.Interfaces;

namespace FileStorage.Core
{
	public sealed class FolderStorageInfo
    {
        public string FileId { get; set; }
        public string FolderId { get; set; }

        public string StorageName { get; set; }

        public string UserId { get; set; }

		public bool DoNotNeedLog { get; set; }
	}

    /// <summary>
	/// Hierarchical folder storage stored on disk 
	/// </summary> 
	public interface IFolderStorage
	{
		/// <summary>
		/// Save <see cref="FileContainer"/>
		/// </summary>
		/// <param name="file"></param>
		/// <returns></returns>
		string Save(FileContainer file);

		/// <summary>
		/// Save stream by id
		/// </summary>
		/// <param name="stream"></param>
		/// <param name="info"></param>
		void Save(Stream stream, FolderStorageInfo info);

		/// <summary>
		/// Get stream by id
		/// </summary>
		/// <param name="info"></param>
		/// <returns></returns>
		Stream GetStream(FolderStorageInfo info);

		/// <summary>
		/// Append byres to the end 
		/// </summary>
		/// <param name="array"></param>
		/// <param name="info"></param>
		void Append(byte[] array, FolderStorageInfo info);


		/// <summary>
		/// Save byte array by id
		/// </summary>
		/// <param name="array"></param>
		/// <param name="id"></param>
		void Save(byte [] array, FolderStorageInfo info);

		/// <summary>
		/// Get <see cref="FileContainer"/> by id
		/// </summary>
		/// <param name="id"></param>
		/// <returns></returns>
		FileContainer Get(FolderStorageInfo info);

		/// <summary>
		/// Get stream by id
		/// </summary>
		/// <param name="id"></param>
		/// <returns></returns>
		Stream GetStream(FolderStorageInfo info, FileMode mode, FileAccess access, FileShare share);

		/// <summary>
		/// Delete file by id
		/// </summary>
		/// <param name="id"></param>
		/// <returns></returns>
		bool Delete(FolderStorageInfo info);

		/// <summary>
		/// Delete temp file, for partial load
		/// </summary>
		/// <param name="id"></param>
		/// <returns></returns>
		bool DeleteTemp(FolderStorageInfo info);

		/// <summary>
		/// Delete all files in repository
		/// </summary>
		void DeleteAll();

		/// <summary>
		/// Is file exists 
		/// </summary>
		/// <param name="id"></param>
		/// <returns></returns>
		bool Exists(FolderStorageInfo info);

		/// <summary>
		/// Method return full path to the file on the disk
		/// </summary>
		/// <param name="folderId"></param>
		/// <returns></returns>
		string GetFullFileName(FolderStorageInfo info);

		/// <summary>
		/// Move file 
		/// </summary>
		/// <param name="fromId"></param>
		/// <param name="toId"></param>
		void Move(FolderStorageInfo infoFrom, FolderStorageInfo infoTo);

		/// <summary>
		/// 
		/// </summary>
		/// <param name="info"></param>
		/// <returns></returns>
		string GetRelativeFileName(FolderStorageInfo info);
		 

		/// <summary>
		/// Save stream by id
		/// </summary>
		/// <param name="input"></param>
		/// <param name="info"></param>
		/// <returns></returns>
		FileToken SaveStream(Stream input, FolderStorageInfo info);

		/// <summary>
		/// Change file storage hierarchies
		/// </summary>
		/// <param name="oldDepth"></param>
		/// <param name="newDepth"></param>
		void ChangeDepthDirectory(int oldDepth, int newDepth);

		/// <summary>
		/// Return count of documents on file storage
		/// </summary>
		/// <returns></returns>
		long GetCount();
		 
		void RefreshLog();

		/// <summary>
		/// Return ids from file storage
		/// </summary>
		/// <param name="offset"></param>
		/// <param name="count"></param>
		/// <returns></returns>
		List<string> GetIds(long offset, int count);

		/// <summary>
		/// Events log
		/// </summary>
		/// <param name="time"></param>
		/// <param name="take"></param>
		/// <returns></returns>
		List<EventMessage> GetLog(DateTime time, int take);

		DateTime? GetLogDateById(FolderStorageInfo info);


		/// <summary>
		/// Return file size
		/// </summary>
		/// <param name="info"></param>
		/// <returns></returns>
		long GetSize(FolderStorageInfo info);

		/// <summary>
		/// Partial save, file saved to temporary file while close is false
		/// </summary>
		/// <param name="SaveFireRequest"></param> 
		void SaveToFile(SaveFileRequest request);
		 
		/// <summary>
		/// Delete all files which older than date
		/// </summary>
		/// <param name="date"></param>
		void ClearTtl(DateTime date);

		/// <summary>
		/// Drop store
		/// </summary>
		void Drop();
	}
}
