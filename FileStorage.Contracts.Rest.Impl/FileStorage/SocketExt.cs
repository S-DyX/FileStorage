using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading;

namespace FileStorage.Contracts.Rest.Impl.FileStorage
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
            lock (socket)
            {
                socket?.Send(bytes); 
            }
            
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
            var message = new List<byte>(bytes.Length + 3 + 8);
            message.Add((byte)'@');
            var size = bytes.Length.GetBytes();
            message.AddRange(size);
            message.AddRange(bytes);
            message.Add((byte)'#');
            bytes = message.ToArray();
            return bytes;
        }

      
   }
}
