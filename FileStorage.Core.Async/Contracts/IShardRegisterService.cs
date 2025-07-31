using System;
using System.Collections.Generic;
using System.Text;

namespace FileStorage.Core.Contracts
{
    public sealed class ShardInfo
    {
        public string Id { get; set; }

        public string Ip { get; set; }

        public DateTime LastActiveTime { get; set; }
    }
    public interface IShardRegisterService
    {
        void Register(ShardInfo info);

        List<ShardInfo> GetRegister(string hash);

    }
}
