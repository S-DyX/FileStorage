using FileStorage.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace FileStorage.Contracts.Impl.Connections
{
    public interface ITcpClientRegistry : IDisposable
    {
        TcpClientContainer AddCommand(TcpClient client, NetworkStream stream, string token, IMessageProcessingService messageProcessingService);
        void Start();
        void Stop();

    }
    public sealed class TcpClientRegistry : ITcpClientRegistry
    {
        private readonly ILocalLogger _localLogger;

        private readonly List<TcpClientContainer> _commands = new List<TcpClientContainer>();


        public TcpClientRegistry(ILocalLogger localLogger)
        {
            _localLogger = localLogger;
        }

        public TcpClientContainer AddCommand(TcpClient client,
            NetworkStream stream,
            string robot,
            IMessageProcessingService messageProcessingService)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            if (robot == null)
            {
                return null;
            }

            var tcpChatClient = new TcpClientContainer(client, null, stream, messageProcessingService, _localLogger, TcpClientTypes.ReadFile);
            tcpChatClient.Start();
            lock (_commands)
            {
                _commands.RemoveAll(x => !x.IsRun());
                _commands.Add(tcpChatClient);
            }

            return tcpChatClient;
        }




        public void Start()
        {

            foreach (var client in _commands)
            {
                client.Start();
            }
        }

        public void Stop()
        {
            Parallel.ForEach(_commands, client =>
            {
                client.Stop();
            });

        }

        public void Dispose()
        {

            Parallel.ForEach(_commands, tcpClient =>
            {
                tcpClient.Client?.Dispose();
            });
        }

    }
}
