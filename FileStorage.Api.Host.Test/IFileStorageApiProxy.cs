using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace FileStorage.Api.Host.Test
{
    public interface IFileStorageApiProxy
    {
        byte[] GetBytes(string id, long offset, int size, string name);
        byte[] GetBytes(string id, long offset, int size);

        bool IsExists(string id, string storageName);

        string GetExternalId(string id, string storageName);

        string Save(string id, Stream file, string storageName);

        string GetId(string id);

        int GetSize(string id, string storageName);

    }
}
