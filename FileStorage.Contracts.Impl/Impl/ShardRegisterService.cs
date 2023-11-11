using System;
using System.Collections.Generic;
using System.Text;
using FileStorage.Core.Contracts;
using FileStorage.Core.Interfaces.Settings;

namespace FileStorage.Contracts.Impl.Impl
{
    public sealed class ShardRegisterService : IShardRegisterService
    {
        private readonly IFileStorageSettings _fileStorageSettings;
        private readonly Dictionary<string, ShardInfo> _dictionary = new Dictionary<string, ShardInfo>();
        public ShardRegisterService(IFileStorageSettings fileStorageSettings)
        {
            _fileStorageSettings = fileStorageSettings;
        }

        public void Register(ShardInfo info)
        {
            //_fileStorageSettings.NumberOfShards
            if (!_dictionary.ContainsKey(info.Id))
            {
                _dictionary[info.Id] = info;
            }
            else
            {
                var shardInfo = _dictionary[info.Id];
                shardInfo.LastActiveTime = DateTime.UtcNow;
            }
        }

        public List<ShardInfo> GetRegister(string hash)
        {
            return null;
            //_fileStorageSettings.NumberOfShards
            //throw new NotImplementedException();
        }
    }
}
