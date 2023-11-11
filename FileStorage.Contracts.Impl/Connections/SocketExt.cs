using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace FileStorage.Contracts.Impl.Connections
{
    public static class SocketExt
    {
        public static bool SendWithHeaders(this TcpClient clientClient, byte[] message)
        {
            if (!clientClient.Connected || message == null)
                return false;
            message = AddHeader(message);
            var broadcastStream = clientClient.GetStream();
            broadcastStream.Write(message, 0, message.Length);
            broadcastStream.Flush();
            return true;
        }
        public static void SendBytes(this Socket socket, byte[] bytes)
        {
            if (bytes == null)
                return;
            bytes = AddHeader(bytes);
            socket?.Send(bytes);
        }
        public static void SendBytes(this UdpClient udpClient, byte[] bytes, IPEndPoint ipEndPoint)
        {
            if (bytes == null)
                return;
            bytes = AddHeader(bytes);
            udpClient.Send(bytes, bytes.Length, ipEndPoint);
        }

        public static void SendBytes(this NetworkStream socket, byte[] bytes)
        {
            if (bytes == null)
                return;
            bytes = AddHeader(bytes);
            socket?.Write(bytes, 0, bytes.Length);
        }

        public static byte[] AddHeader(this byte[] bytes)
        {
            var message = new List<byte>(bytes.Length + 3);
            message.Add((byte)'@');
            var size = bytes.Length.GetBytes();
            message.AddRange(size);
            message.AddRange(bytes);
            message.Add((byte)'#');
            bytes = message.ToArray();
            return bytes;
        }

        public static void WaitDataAndPing(this TcpClient client, NetworkStream stream)
        {
            int i = 0;
            while (client.Connected && (!stream.DataAvailable || client.Available == 0))
            {
                if (i == 50)
                {
                    stream.SendBytes(new Byte[] { 32 });
                    i = 0;
                    Thread.Sleep(100);
                }

                i++;
            }
        }

        public static void WaitDataAndPing(this TcpClient client, NetworkStream stream, Func<bool> continueWait = null, int minSize = 3)
        {
            int i = 0;
            while (client.Connected && (!stream.DataAvailable || client.Available < minSize))
            {
                if (continueWait != null && !continueWait.Invoke())
                    break;
                if (i == 250)
                {
                    stream.SendBytes(new Byte[] { 32 });
                    i = 0;

                }
                Thread.Sleep(30);

                i++;
            }
        }

        public static void WaitData(this TcpClient client, NetworkStream stream, Func<bool> continueWait = null, int minSize = 3)
        {
            int i = 0;
            while (client.Connected && (!stream.DataAvailable || client.Available < minSize) && i < 1000)
            {
                if (continueWait != null && !continueWait.Invoke())
                    break;

                Thread.Sleep(30);

                i++;
            }
        }

    }
}
