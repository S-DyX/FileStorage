using System;
using System.Collections.Generic;
using System.IO;
using FileStorage.Core.Contracts;
using FileStorage.Core.Entities;
using FileStorage.Core.Interfaces;

namespace FileStorage.Core
{
	/// <summary>
	/// Hierarchical file storage stored on disk 
	/// </summary>
	/// <typeparam name="TValue"></typeparam>
	public interface IFileStorage<TValue>
	{
		/// <summary>
		/// Save <see cref="FileContainer"/>
		/// </summary>
		/// <param name="file"></param>
		/// <returns></returns>
		TValue Save(FileContainer file);

		/// <summary>
		/// Save stream by id
		/// </summary>
		/// <param name="stream"></param>
		/// <param name="id"></param>
		void Save(Stream stream, TValue id);

		/// <summary>
		/// Append byres to the end 
		/// </summary>
		/// <param name="array"></param>
		/// <param name="id"></param>
		void Append(byte[] array, TValue id);


		/// <summary>
		/// Save byte array by id
		/// </summary>
		/// <param name="array"></param>
		/// <param name="id"></param>
		void Save(byte [] array, TValue id);

		/// <summary>
		/// Get <see cref="FileContainer"/> by id
		/// </summary>
		/// <param name="id"></param>
		/// <returns></returns>
		FileContainer Get(TValue id);

		/// <summary>
		/// Get stream by id
		/// </summary>
		/// <param name="id"></param>
		/// <returns></returns>
		Stream GetStream(TValue id);

		/// <summary>
		/// Delete file by id
		/// </summary>
		/// <param name="id"></param>
		/// <returns></returns>
		bool Delete(TValue id);

		/// <summary>
		/// Delete temp file, for partial load
		/// </summary>
		/// <param name="id"></param>
		/// <returns></returns>
		bool DeleteTemp(TValue id);

		/// <summary>
		/// Delete all files in repository
		/// </summary>
		void DeleteAll();

		/// <summary>
		/// Is file exists 
		/// </summary>
		/// <param name="id"></param>
		/// <returns></returns>
		bool Exists(TValue id);

		/// <summary>
		/// Method return full path to the file on the disk
		/// </summary>
		/// <param name="id"></param>
		/// <returns></returns>
		string GetFullFileName(TValue id);

		/// <summary>
		/// Move file 
		/// </summary>
		/// <param name="fromId"></param>
		/// <param name="toId"></param>
		void Move(TValue fromId, TValue toId);

		/// <summary>
		/// 
		/// </summary>
		/// <param name="id"></param>
		/// <returns></returns>
		string GetRelativeFileName(TValue id);

		/// <summary>
		/// Save file stream where id get from body hash
		/// </summary>
		/// <param name="input"></param>
		/// <returns></returns>
		FileToken SaveAsHash(Stream input);

		/// <summary>
		/// Save stream by id
		/// </summary>
		/// <param name="input"></param>
		/// <param name="id"></param>
		/// <returns></returns>
		FileToken SaveStream(Stream input, string id);

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

		//Stream GetStream(long offset);

		void RefreshLog();

		/// <summary>
		/// Return ids from file storage
		/// </summary>
		/// <param name="offset"></param>
		/// <param name="count"></param>
		/// <returns></returns>
		List<TValue> GetIds(long offset, int count);

		/// <summary>
		/// Events log
		/// </summary>
		/// <param name="time"></param>
		/// <param name="take"></param>
		/// <returns></returns>
		List<EventMessage> GetLog(DateTime time, int take);

		DateTime? GetLogDateById(string id);

		/// <summary>
		/// Return full file name
		/// </summary>
		/// <param name="id"></param>
		/// <returns></returns>
		string GetFullFileName(string id);

		/// <summary>
		/// Return file size
		/// </summary>
		/// <param name="id"></param>
		/// <returns></returns>
		long GetSize(string id);

		/// <summary>
		/// Partial save, file saved to temporary file while close is false
		/// </summary>
		/// <param name="request"></param> 
		void SaveToFile(SaveFileRequest request); 

		/// <summary>
		/// Delete all files which older than date
		/// </summary>
		/// <param name="date"></param>
		void ClearTtl(DateTime date);

		/// <summary>
		/// Drop all 
		/// </summary>
		void Drop();
	}
}
