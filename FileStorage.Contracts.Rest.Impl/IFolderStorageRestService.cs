using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace FileStorage.Contracts.Rest.Impl
{
    public interface ITcpFolderStorageService: IFolderStorageRestService
    {
    }

    /// <summary>
    /// The interface for working with filestorage at REST
    /// </summary>
    public interface IFolderStorageRestService
    {
        /// <summary>
        /// Return internal id from FS by externalId
        /// </summary>
        /// <param name="externalId"></param> 
        /// <returns></returns>
        string GetIdByExternal(string externalId);

        /// <summary>
        /// Return count of documents in FS
        /// </summary>
        /// <param name="storageName"></param>
        /// <returns></returns>
        long GetCount(string storageName);

        /// <summary>
        /// Save bytes to FS 
        /// </summary>
        /// <param name="externalFolderId">this is folder id from your system</param>
        /// <param name="externalFileId">this is file id from your system</param> 
        /// <param name="storageName"></param>
        /// <param name="bytes"></param>
        /// <param name="sessionId"></param>
        void Write(string externalFolderId, string externalFileId, string storageName, byte[] bytes, string sessionId);

        /// <summary>
        /// Save stream to FS
        /// </summary>
        /// <param name="externalFolderId">this is folder id from your system</param>
        /// <param name="externalFileId">this is file id from your system</param> 
        /// <param name="storageName"></param>
        /// <param name="stream"></param>
        /// <param name="sessionId"></param>
        void Write(string externalFolderId, string externalFileId, string storageName, Stream stream, string sessionId);

        /// <summary>
        /// Load data from FS to Stream
        /// </summary>
        /// <param name="externalFolderId">this is folder id from your system</param>
        /// <param name="externalFileId">this is file id from your system</param> 
        /// <param name="storageName"></param>
        /// <param name="stream"></param>
        void Read(string externalFolderId, string externalFileId, string storageName, Stream stream);

        /// <summary>
        /// Return file size
        /// </summary>
        /// <param name="externalFolderId">this is folder id from your system</param>
        /// <param name="externalFileId">this is file id from your system</param> 
        /// <param name="storageName"></param>
        /// <returns></returns>
        long GetSize(string externalFolderId, string externalFileId, string storageName);

        /// <summary>
        /// Is exists on FS
        /// </summary>
        /// <param name="externalFolderId">this is folder id from your system</param>
        /// <param name="externalFileId">this is file id from your system</param> 
        /// <param name="storageName"></param>
        /// <returns></returns>
        bool IsExists(string externalFolderId, string externalFileId, string storageName);

        ///// <summary>
        ///// Return exist id in FS by externalId
        ///// </summary>
        ///// <param name="externalFolderId">this is folder id from your system</param>
        ///// <param name="externalFileId">this is file id from your system</param> 
        ///// <param name="storageName"></param>
        ///// <returns></returns>
        //string GetExistIdByExternal(string externalFolderId, string externalFileId, string storageName);


        /// <summary>
        /// Partial read 
        /// </summary>
        /// <param name="externalFolderId">this is folder id from your system</param>
        /// <param name="externalFileId">this is file id from your system</param> 
        /// <param name="offset"></param>
        /// <param name="size"></param>
        /// <param name="storageName"></param>
        /// <returns></returns>
        byte[] Read(string externalFolderId, string externalFileId, long offset, long size, string storageName);

        /// <summary>
        /// Delete file from FS
        /// </summary>
        /// <param name="externalFolderId">this is folder id from your system</param>
        /// <param name="externalFileId">this is file id from your system</param> 
        /// <param name="storageName"></param>
        /// <returns></returns>
        bool Delete(string externalFolderId, string externalFileId, string storageName);

        /// <summary>
        /// Return memory stream
        /// </summary>
        /// <param name="externalFolderId">this is folder id from your system</param>
        /// <param name="externalFileId">this is file id from your system</param> 
        /// <param name="name"></param>
        /// <returns></returns>
        Stream GetStream(string externalFolderId, string externalFileId, string name);


        void Move(string externalFolderId, string toExternalFolderId, string externalFileId, string storageName);
    }
}
