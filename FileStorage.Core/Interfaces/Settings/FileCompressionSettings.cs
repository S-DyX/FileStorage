using System;
using System.Collections.Generic;
using System.IO.Compression;

namespace FileStorage.Core.Interfaces.Settings
{
	/// <summary>
	/// Storing files of well compressible types as zip archives: "&lt;id&gt;.zip" instead of "&lt;id&gt;".
	/// Archives are unpacked transparently on read.
	/// </summary>
	public sealed class FileCompressionSettings
	{
		/// <summary>
		/// Default types (extensions from <see cref="Helpers.FileSignatureDetector"/>) that compress well.
		/// </summary>
		public static readonly IReadOnlyCollection<string> DefaultExtensions = new[]
		{
			"txt", "csv", "json", "xml", "html", "svg", "rtf", "ps",
			"doc", "xls", "ppt", "msg",
			"bmp", "tif", "ico", "psd",
			"wav", "tar", "sqlite", "exe", "wasm", "ttf", "otf"
		};

		/// <summary>
		/// Compress new files (on by default). Archives already on disk are read regardless of this setting.
		/// </summary>
		public bool Enabled { get; set; } = true;

		/// <summary>
		/// Smaller files are stored as is, bytes.
		/// </summary>
		public long MinSize { get; set; } = 4 * 1024;

		/// <summary>
		/// Larger files are stored as is, bytes: partial reads of an archive unpack it into a temporary file.
		/// </summary>
		public long MaxSize { get; set; } = 256L * 1024 * 1024;

		/// <summary>
		/// The archive is kept only if its size is not more than this part of the original size.
		/// </summary>
		public double MaxRatio { get; set; } = 0.9;

		/// <summary>
		/// Compression level.
		/// </summary>
		public CompressionLevel Level { get; set; } = CompressionLevel.Optimal;

		/// <summary>
		/// Compress files whose type is not detected by the signature (the ratio check still applies).
		/// </summary>
		public bool CompressUnknown { get; set; }

		/// <summary>
		/// Types to compress: extensions from <see cref="Helpers.FileSignatureDetector"/>.
		/// </summary>
		public HashSet<string> Extensions { get; set; } = new HashSet<string>(DefaultExtensions, StringComparer.OrdinalIgnoreCase);
	}
}
