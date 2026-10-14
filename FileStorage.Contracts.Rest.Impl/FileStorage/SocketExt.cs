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
            // Кадр: '@' + длина (Int32) + данные + '#'. Один массив вместо List<byte> и его копии через ToArray.
            var frame = new byte[bytes.Length + 6];
            frame[0] = (byte)'@';
            BitConverter.TryWriteBytes(new Span<byte>(frame, 1, 4), bytes.Length);
            Buffer.BlockCopy(bytes, 0, frame, 5, bytes.Length);
            frame[frame.Length - 1] = (byte)'#';
            return frame;
        }

      
   }
}
