using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace FileStorage.Contracts.Rest.Impl.FileStorage
{
    public static class StringHelper
    {
        public static string GetSha1Hash(this string value)
        {
            // Как на сервере (FileStorage.Core): пустой и null id хешируются как пустая строка.
            // Раньше здесь возвращалась пустая строка, и id расходились с серверными.
            using (var sha1 = SHA1.Create())
            {
                return sha1.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty)).GetHashString();
            }
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
            if (bytes == null || bytes.Length == 0)
                return null;
            // Символ занимает 2 байта. При нечётной длине последний неполный байт заменяем на U+FFFD;
            // раньше Buffer.BlockCopy падал с ArgumentException.
            var charCount = bytes.Length / sizeof(char);
            var hasTail = bytes.Length % sizeof(char) != 0;
            var chars = new char[charCount + (hasTail ? 1 : 0)];
            Buffer.BlockCopy(bytes, 0, chars, 0, charCount * sizeof(char));
            if (hasTail)
                chars[charCount] = '�';
            return new string(chars);
        }
    }
}
