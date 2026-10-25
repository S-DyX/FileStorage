using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Compression;
using System.Threading;
using FileStorage.Core.Interfaces;
using FileStorage.Core.Interfaces.Settings;

namespace FileStorage.Core.Helpers
{
	/// <summary>
	/// A stored file may be kept as a zip archive "&lt;file&gt;.zip" with a single entry. The storages check the
	/// plain file first and then its archive, so no database or extra fields are needed.
	/// </summary>
	internal static class FileArchive
	{
		/// <summary>
		/// Extension of an archived file.
		/// </summary>
		public const string Extension = ".zip";

		/// <summary>
		/// Archives with smaller content are unpacked into memory, larger ones — into a cached temporary file.
		/// </summary>
		private const long MemoryThreshold = 1024 * 1024;

		/// <summary>
		/// Name of the archive of the file.
		/// </summary>
		public static string GetArchiveName(string fileName)
		{
			return fileName + Extension;
		}

		/// <summary>
		/// Name of the stored file: the archive extension is removed.
		/// </summary>
		public static string GetStoredName(string fileName)
		{
			return fileName.EndsWith(Extension, StringComparison.OrdinalIgnoreCase)
				? fileName.Substring(0, fileName.Length - Extension.Length)
				: fileName;
		}

		/// <summary>
		/// Compresses <paramref name="source"/> into <paramref name="archive"/> when the type of the file is
		/// compressible and the archive is small enough. Returns false (and leaves no archive) otherwise.
		/// </summary>
		/// <param name="source">File to compress.</param>
		/// <param name="archive">Archive to create.</param>
		/// <param name="entryName">Name of the entry in the archive (the file id).</param>
		/// <param name="settings">Compression settings; null — do not compress.</param>
		/// <param name="logger">Logger of compression errors.</param>
		public static bool TryCompress(string source, string archive, string entryName, FileCompressionSettings settings, ILocalLogger logger)
		{
			if (settings == null || !settings.Enabled)
				return false;

			try
			{
				var length = new FileInfo(source).Length;
				if (length < settings.MinSize || length > settings.MaxSize || !IsCompressible(source, settings))
					return false;

				using (var output = new FileStream(archive, FileMode.Create, FileAccess.Write, FileShare.None, 81920))
				using (var zip = new ZipArchive(output, ZipArchiveMode.Create))
				{
					var entry = zip.CreateEntry(entryName, settings.Level);
					using (var entryStream = entry.Open())
					using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, 81920))
					{
						input.CopyTo(entryStream);
					}
				}

				if (new FileInfo(archive).Length <= length * settings.MaxRatio)
					return true;
			}
			catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is InvalidDataException)
			{
				// the file is saved as is if it can not be compressed
				logger?.Error($"Can not compress {source}", e);
			}

			TryDelete(archive);
			return false;
		}

		/// <summary>
		/// Size of the archived file (the size of its content, not of the archive); -1 if the archive is not valid.
		/// </summary>
		public static long GetLength(string archive)
		{
			try
			{
				using (var zip = ZipFile.OpenRead(archive))
				{
					return zip.Entries.Count == 1 ? zip.Entries[0].Length : -1;
				}
			}
			catch (InvalidDataException)
			{
				return -1;
			}
		}

		/// <summary>
		/// Seekable read only stream with the content of the archive; null if the archive is not valid.
		/// </summary>
		public static Stream OpenRead(string archive)
		{
			var info = new FileInfo(archive);
			using (var zip = ZipFile.OpenRead(archive))
			{
				if (zip.Entries.Count != 1)
					return null;

				var entry = zip.Entries[0];
				if (entry.Length > MemoryThreshold)
					return DecompressedFileCache.Open(info, entry);

				// Stream.ReadExactly is not available in .NET 6
				var result = new MemoryStream((int)entry.Length);
				using (var input = entry.Open())
				{
					input.CopyTo(result);
				}
				result.Position = 0;
				return result;
			}
		}

		/// <summary>
		/// Unpacks the archive into <paramref name="target"/> (overwrites it).
		/// </summary>
		public static void Extract(string archive, string target)
		{
			using (var zip = ZipFile.OpenRead(archive))
			{
				if (zip.Entries.Count != 1)
					throw new InvalidDataException($"{archive} is not an archive of a stored file");

				using (var input = zip.Entries[0].Open())
				using (var output = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None, 81920))
				{
					input.CopyTo(output);
				}
			}
		}

		/// <summary>
		/// Is the type of the file (by its signature) in the list of compressible types.
		/// </summary>
		private static bool IsCompressible(string fileName, FileCompressionSettings settings)
		{
			var signature = FileSignatureDetector.Detect(fileName);
			return signature == null
				? settings.CompressUnknown
				: settings.Extensions?.Contains(signature.Extension) == true;
		}

		internal static void TryDelete(string fileName)
		{
			try
			{
				if (!string.IsNullOrEmpty(fileName) && File.Exists(fileName))
					File.Delete(fileName);
			}
			catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
			{
				// the file is in use: it is removed by the next cleanup
			}
		}
	}

	/// <summary>
	/// Unpacked copies of large archives in the temporary folder. Partial reads (getbytes, Range requests)
	/// open the file many times: without the cache every read would unpack the archive from the start.
	/// A copy is removed after <see cref="IdleTime"/> without reads or when the archive changes.
	/// </summary>
	internal static class DecompressedFileCache
	{
		private static readonly TimeSpan IdleTime = TimeSpan.FromMinutes(5);
		private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(1);
		private static readonly ConcurrentDictionary<string, CacheItem> Items = new ConcurrentDictionary<string, CacheItem>(StringComparer.Ordinal);
		private static readonly string CacheDirectory = CreateCacheDirectory();
		private static readonly Timer CleanupTimer = new Timer(_ => Cleanup(), null, CleanupInterval, CleanupInterval);

		/// <summary>
		/// Read only stream over the unpacked copy of the archive entry.
		/// </summary>
		public static Stream Open(FileInfo archive, ZipArchiveEntry entry)
		{
			while (true)
			{
				var item = Items.GetOrAdd(archive.FullName, _ => new CacheItem());
				lock (item)
				{
					// the item was removed by the cleanup while we were waiting for the lock
					if (item.IsRemoved)
						continue;

					if (item.FileName == null || item.ArchiveWriteTime != archive.LastWriteTimeUtc
						|| item.ArchiveLength != archive.Length || !File.Exists(item.FileName))
					{
						var fileName = Path.Combine(CacheDirectory, Guid.NewGuid().ToString("N"));
						try
						{
							using (var input = entry.Open())
							using (var output = new FileStream(fileName, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920))
							{
								input.CopyTo(output);
							}
						}
						catch
						{
							FileArchive.TryDelete(fileName);
							throw;
						}

						FileArchive.TryDelete(item.FileName);
						item.FileName = fileName;
						item.ArchiveWriteTime = archive.LastWriteTimeUtc;
						item.ArchiveLength = archive.Length;
					}

					item.LastAccess = DateTime.UtcNow;
					// FileShare.Delete: the copy may be removed while it is read
					return new FileStream(item.FileName, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 81920);
				}
			}
		}

		private static void Cleanup()
		{
			var expired = DateTime.UtcNow - IdleTime;
			foreach (var pair in Items)
			{
				var item = pair.Value;
				lock (item)
				{
					if (item.LastAccess > expired)
						continue;
					item.IsRemoved = true;
					FileArchive.TryDelete(item.FileName);
					Items.TryRemove(pair.Key, out _);
				}
			}
		}

		private static string CreateCacheDirectory()
		{
			// a folder per process: several FileStorage servers on one host do not delete each other's files
			var directory = Path.Combine(Path.GetTempPath(), "filestorage-archives", Environment.ProcessId.ToString());
			if (Directory.Exists(directory))
				Directory.Delete(directory, true);
			Directory.CreateDirectory(directory);
			AppDomain.CurrentDomain.ProcessExit += (_, _) =>
			{
				try
				{
					Directory.Delete(directory, true);
				}
				catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
				{
				}
			};
			return directory;
		}

		private sealed class CacheItem
		{
			public string FileName { get; set; }

			public DateTime ArchiveWriteTime { get; set; }

			public long ArchiveLength { get; set; }

			public DateTime LastAccess { get; set; }

			public bool IsRemoved { get; set; }
		}
	}
}
