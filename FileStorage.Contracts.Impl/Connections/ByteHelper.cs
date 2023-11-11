using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace FileStorage.Contracts.Impl.Connections
{
	public static class ByteHelper
	{

		public static List<byte[]> GetVideoCommands(this byte[] bytesFrom)
		{
			var images = new List<byte[]>();
			var len = bytesFrom.Length;
			for (var index = 0; index < len - 1; index++)
			{
				var b = bytesFrom[index];
				if (b == 123 && bytesFrom[index + 1] == '@')
				{
					var startIndex = index + 2;
					var timestamp = BitConverter.ToInt64(bytesFrom, startIndex);
					startIndex += 8;
					var size = BitConverter.ToInt32(bytesFrom, startIndex);
					startIndex += 4;
					var endIndex = startIndex + size;

					if (size > 0 && endIndex < len && bytesFrom[endIndex] == '#')
					{
						var image = new byte[size];
						Array.Copy(bytesFrom, startIndex, image, 0, size);
						images.Add(image);
						index = endIndex;
					}
				}
			}

			return images;
		}
		public static List<byte[]> GetAudioCommands(this byte[] bytesFrom)
		{
			var images = new List<byte[]>();
			var len = bytesFrom.Length;
			for (var index = 0; index < len - 1; index++)
			{
				var b = bytesFrom[index];
				if (b == 123 && bytesFrom[index + 1] == '@')
				{
					var startIndex = index + 2;
					var timestamp = BitConverter.ToInt64(bytesFrom, startIndex);
					startIndex += 8;
					var size = BitConverter.ToInt32(bytesFrom, startIndex);
					startIndex += 4;
					var endIndex = startIndex + size;

					if (size > 0 && endIndex < len && bytesFrom[endIndex] == '#')
					{
						var image = new byte[size];
						Array.Copy(bytesFrom, startIndex, image, 0, size);
						images.Add(image);
						index = endIndex;
					}
				}
			}

			return images;
		}
		public static byte[] GetCommonMessage(this TcpCommonMessage message)
		{
			message.RequestId = Guid.NewGuid().ToString();
			var serialize = JsonConvert.SerializeObject(message);
			var bytes = System.Text.Encoding.UTF8.GetBytes(serialize);
			return bytes.AddHeader();
		}


		public static bool IsReadToEndCommands(this List<byte> bytes)
		{
			var value = bytes.Take(50).ToArray();
			if (value[0] == '@')
			{
				var size = BitConverter.ToInt32(value, 1);
				if (bytes.Count > size)
					return true;

			}

			return false;

		}

		public static string[] GetCommands(this byte[] bytes)
		{

			if (bytes == null)
				return new string[0];

			var result = new List<string>();
			for (int i = 0; i < bytes.Length; i++)
			{
				if (bytes[i] == '@')
				{
					var size = BitConverter.ToInt32(bytes, i + 1);
					var skip = i + 1 + 4;
					var offset = i + size;
					var end = skip + offset;
					if (bytes.Length > end && bytes[end] == '#')
					{
						var array = bytes.Skip(skip).Take(size).ToArray();
						var command = Encoding.UTF8.GetString(array);
						result.Add(command);
						i = end;
					}
				}
			}

			return result.ToArray();
		}

		public static string ToStr(this byte[] source)
		{
			var sBuilder = new StringBuilder(256);
			foreach (byte t in source)
			{
				sBuilder.Append(t.ToString("x2"));
			}
			return sBuilder.ToString();
		}

		public static string GetHashMd5(this string value)
		{
			if (string.IsNullOrEmpty(value))
				return string.Empty;
			var encoder = new ASCIIEncoding();
			var passwordBytes = encoder.GetBytes(value);
			using (var shaBuilder = MD5.Create())
			{
				var hash = shaBuilder.ComputeHash(passwordBytes);
				return hash.ToStr();
			}
		}
		public static byte[] GetBytes<TStruct>(this TStruct str) where TStruct : struct
		{
			int size = Marshal.SizeOf(str);
			byte[] arr = new byte[size];

			IntPtr ptr = Marshal.AllocHGlobal(size);
			Marshal.StructureToPtr(str, ptr, true);
			Marshal.Copy(ptr, arr, 0, size);
			Marshal.FreeHGlobal(ptr);
			return arr;
		}

		public static List<TStruct> FromBytes<TStruct>(this byte[] arr, int offset = 0) where TStruct : struct
		{
			var value = new TStruct();
			var structureType = value.GetType();
			var size = Marshal.SizeOf(value);

			if (size == 0)
				return null;

			var len = arr.Length / size;
			var result = new List<TStruct>(len);

			while (offset < arr.Length)
			{
				IntPtr ptr = Marshal.AllocHGlobal(size);
				Marshal.Copy(arr, offset, ptr, size);
				value = (TStruct)Marshal.PtrToStructure(ptr, structureType);
				Marshal.FreeHGlobal(ptr);

				result.Add(value);
				offset += size;
			}


			return result;
		}
		public static string ToHexString(byte[] bytes)
		{
			String byteArrayStr = String.Join(" ", bytes.Select(b => $"{b:X2}"));
			return byteArrayStr;
		}

	}
}
