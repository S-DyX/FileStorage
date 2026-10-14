using System;
using System.IO;

namespace FileStorage.Core.Helpers
{
	public static class StreamHelper
	{
		public static void CopyStream(Stream instream, Stream outstream)
		{
			instream.CopyTo(outstream);
		}

		/// <summary>
		/// Побайтно сравнивает содержимое двух файлов
		/// </summary>
		public static bool FilesAreEqual(string fileName1, string fileName2)
		{
			const int bufferSize = 81920;
			using (var stream1 = new FileStream(fileName1, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, bufferSize))
			using (var stream2 = new FileStream(fileName2, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, bufferSize))
			{
				if (stream1.Length != stream2.Length)
					return false;

				var buffer1 = new byte[bufferSize];
				var buffer2 = new byte[bufferSize];
				while (true)
				{
					var read1 = ReadFull(stream1, buffer1);
					var read2 = ReadFull(stream2, buffer2);
					if (read1 != read2)
						return false;
					if (read1 == 0)
						return true;
					if (!buffer1.AsSpan(0, read1).SequenceEqual(buffer2.AsSpan(0, read2)))
						return false;
				}
			}
		}

		/// <summary>
		/// Читает из потока, пока буфер не заполнится или поток не закончится
		/// </summary>
		public static int ReadFull(Stream stream, byte[] buffer)
		{
			var total = 0;
			int read;
			while (total < buffer.Length && (read = stream.Read(buffer, total, buffer.Length - total)) > 0)
			{
				total += read;
			}
			return total;
		}
	}
}
