using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Text;
using System.Threading;
using Newtonsoft.Json;

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
            // Кадр: '@' + длина (Int32) + данные + '#'. Один массив вместо List<byte> и его копии через ToArray;
            // длина пишется напрямую, а не через Marshal.AllocHGlobal на каждый кадр.
            var frame = new byte[bytes.Length + 6];
            frame[0] = (byte)'@';
            BitConverter.TryWriteBytes(new Span<byte>(frame, 1, 4), bytes.Length);
            Buffer.BlockCopy(bytes, 0, frame, 5, bytes.Length);
            frame[frame.Length - 1] = (byte)'#';
            return frame;
        }

        private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

        /// <summary>
        /// Сериализует сообщение в JSON сразу в кадр протокола ('@' + длина + UTF-8 JSON + '#').
        /// Формат тот же, что у JsonConvert.SerializeObject + AddHeader, но без промежуточной строки
        /// (для содержимого файла в base64 она занимала бы около 2,7 размера файла) и без лишних копий массива.
        /// Данные кадра — первые Length байт GetBuffer().
        /// </summary>
        public static MemoryStream SerializeFrame<T>(T message, int capacity = 256)
        {
            var stream = new MemoryStream(Math.Max(capacity, 16));
            stream.WriteByte((byte)'@');
            stream.Write(new byte[4], 0, 4);
            using (var writer = new StreamWriter(stream, Utf8NoBom, 16 * 1024, true))
            using (var jsonWriter = new JsonTextWriter(writer))
            {
                JsonSerializer.CreateDefault().Serialize(jsonWriter, message);
            }
            var payloadLength = (int)stream.Length - 5;
            stream.WriteByte((byte)'#');
            BitConverter.TryWriteBytes(new Span<byte>(stream.GetBuffer(), 1, 4), payloadLength);
            return stream;
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
                    Thread.Sleep(0);
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
                Thread.Sleep(5);

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

                Thread.Sleep(5);

                i++;
            }
        }

    }
}
