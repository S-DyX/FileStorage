using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace FileStorage.Core.Helpers
{
	public static class StringHelper
	{
		public static string GetSha1Hash(this string value)
		{
			var hash = string.Empty;
			using (var sha1 = SHA1.Create())
			{
				var data = value ?? string.Empty;
				sha1.ComputeHash(Encoding.UTF8.GetBytes(data));
				hash = sha1.Hash.GetHashString();
			}
			return hash;
		}
		public static string GetHashString(this byte[] hash)
		{
			var sb = new StringBuilder();
			for (int i = 0; i < hash.Length; i++)
			{
				sb.Append(hash[i].ToString("X2"));
			}
			return sb.ToString();
		}

		public static bool IsNullOrEmpty(this string value)
		{
			return String.IsNullOrEmpty(value);
		}

		public static string Fill(this string format, params object[] args)
		{
			return String.Format(format, args);
		}

		public static string Convert(this int val)
		{
			return val.ToString(CultureInfo.InvariantCulture);
		}

		public static string Convert(this int? value)
		{
			return value.HasValue ? value.Value.Convert() : String.Empty;
		}

		public static byte[] ToBytes(this string value)
		{
			if (value.IsNullOrEmpty())
				return null;
			var bytes = new byte[value.Length * sizeof(char)];
			Buffer.BlockCopy(value.ToCharArray(), 0, bytes, 0, bytes.Length);
			return bytes;
		}

		public static string FromBytes(this byte[] bytes)
		{
			if (bytes.IsNullOrEmpty())
				return null;
			var chars = new char[bytes.Length / sizeof(char)];
			Buffer.BlockCopy(bytes, 0, chars, 0, bytes.Length);
			return new string(chars);
		}
	}
}
