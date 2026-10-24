using System;
using System.Buffers;
using System.Buffers.Binary;
using System.IO;
using System.Text;
using System.Text.Unicode;

namespace FileStorage.Core.Helpers
{
	/// <summary>
	/// Detects the content type of a file by its signature (magic bytes) instead of comparing hex strings.
	/// </summary>
	public static class FileSignatureDetector
	{
		/// <summary>
		/// Bytes read from the start of the stream: enough for TAR (signature at offset 257) and for the
		/// first entries of ZIP based formats (docx, xlsx, pptx, odt, epub, jar, apk).
		/// </summary>
		private const int HeaderSize = 8192;

		/// <summary>
		/// ISO 9660 primary volume descriptor: "CD001" at offset 0x8001.
		/// </summary>
		private const int IsoSignatureOffset = 0x8001;

		/// <summary>
		/// Size of an OLE compound file (doc, xls, ppt, msg) directory entry.
		/// </summary>
		private const int OleDirectoryEntrySize = 128;

		private static readonly FileType Png = new("image/png", "png");
		private static readonly FileType Jpeg = new("image/jpeg", "jpg");
		private static readonly FileType Gif = new("image/gif", "gif");
		private static readonly FileType Bmp = new("image/bmp", "bmp");
		private static readonly FileType Tiff = new("image/tiff", "tif");
		private static readonly FileType Webp = new("image/webp", "webp");
		private static readonly FileType Heic = new("image/heic", "heic");
		private static readonly FileType Avif = new("image/avif", "avif");
		private static readonly FileType Ico = new("image/x-icon", "ico");
		private static readonly FileType Svg = new("image/svg+xml", "svg");
		private static readonly FileType Psd = new("image/vnd.adobe.photoshop", "psd");
		private static readonly FileType Djvu = new("image/vnd.djvu", "djvu");

		private static readonly FileType Pdf = new("application/pdf", "pdf");
		private static readonly FileType PostScript = new("application/postscript", "ps");
		private static readonly FileType Rtf = new("application/rtf", "rtf");
		private static readonly FileType Doc = new("application/msword", "doc");
		private static readonly FileType Xls = new("application/vnd.ms-excel", "xls");
		private static readonly FileType Ppt = new("application/vnd.ms-powerpoint", "ppt");
		private static readonly FileType Msg = new("application/vnd.ms-outlook", "msg");
		private static readonly FileType Docx = new("application/vnd.openxmlformats-officedocument.wordprocessingml.document", "docx");
		private static readonly FileType Xlsx = new("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "xlsx");
		private static readonly FileType Pptx = new("application/vnd.openxmlformats-officedocument.presentationml.presentation", "pptx");
		private static readonly FileType Odt = new("application/vnd.oasis.opendocument.text", "odt");
		private static readonly FileType Ods = new("application/vnd.oasis.opendocument.spreadsheet", "ods");
		private static readonly FileType Odp = new("application/vnd.oasis.opendocument.presentation", "odp");
		private static readonly FileType Epub = new("application/epub+zip", "epub");

		private static readonly FileType Zip = new("application/zip", "zip");
		private static readonly FileType Jar = new("application/java-archive", "jar");
		private static readonly FileType Apk = new("application/vnd.android.package-archive", "apk");
		private static readonly FileType Rar = new("application/vnd.rar", "rar");
		private static readonly FileType SevenZip = new("application/x-7z-compressed", "7z");
		private static readonly FileType Gzip = new("application/gzip", "gz");
		private static readonly FileType Bzip2 = new("application/x-bzip2", "bz2");
		private static readonly FileType Xz = new("application/x-xz", "xz");
		private static readonly FileType Zstd = new("application/zstd", "zst");
		private static readonly FileType Tar = new("application/x-tar", "tar");
		private static readonly FileType Cab = new("application/vnd.ms-cab-compressed", "cab");
		private static readonly FileType Iso = new("application/x-iso9660-image", "iso");

		private static readonly FileType Mp3 = new("audio/mpeg", "mp3");
		private static readonly FileType Aac = new("audio/aac", "aac");
		private static readonly FileType M4a = new("audio/mp4", "m4a");
		private static readonly FileType Wav = new("audio/wav", "wav");
		private static readonly FileType Flac = new("audio/flac", "flac");
		private static readonly FileType Ogg = new("audio/ogg", "ogg");
		private static readonly FileType Midi = new("audio/midi", "mid");
		private static readonly FileType Amr = new("audio/amr", "amr");

		private static readonly FileType Mp4 = new("video/mp4", "mp4");
		private static readonly FileType QuickTime = new("video/quicktime", "mov");
		private static readonly FileType ThreeGp = new("video/3gpp", "3gp");
		private static readonly FileType Avi = new("video/x-msvideo", "avi");
		private static readonly FileType Mkv = new("video/x-matroska", "mkv");
		private static readonly FileType WebM = new("video/webm", "webm");
		private static readonly FileType Flv = new("video/x-flv", "flv");
		private static readonly FileType Wmv = new("video/x-ms-wmv", "wmv");
		private static readonly FileType Mpeg = new("video/mpeg", "mpg");
		private static readonly FileType MpegTs = new("video/mp2t", "ts");
		private static readonly FileType OggVideo = new("video/ogg", "ogv");

		private static readonly FileType Woff = new("font/woff", "woff");
		private static readonly FileType Woff2 = new("font/woff2", "woff2");
		private static readonly FileType Otf = new("font/otf", "otf");
		private static readonly FileType Ttf = new("font/ttf", "ttf");

		private static readonly FileType Exe = new("application/vnd.microsoft.portable-executable", "exe");
		private static readonly FileType Wasm = new("application/wasm", "wasm");
		private static readonly FileType Sqlite = new("application/vnd.sqlite3", "sqlite");

		private static readonly FileType Text = new("text/plain", "txt");
		private static readonly FileType Csv = new("text/csv", "csv");
		private static readonly FileType Html = new("text/html", "html");
		private static readonly FileType Xml = new("application/xml", "xml");
		private static readonly FileType Json = new("application/json", "json");

		/// <summary>
		/// Marker of an OLE compound file: the concrete type is read from its directory.
		/// </summary>
		private static readonly FileType OleCompound = new("application/x-ole-storage", "doc");

		/// <summary>
		/// Unambiguous signatures, checked first.
		/// </summary>
		private static readonly Signature[] StrongSignatures =
		{
			new(0, new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, Png),
			new(0, new byte[] { 0xFF, 0xD8, 0xFF }, Jpeg),
			new(0, "GIF87a"u8.ToArray(), Gif),
			new(0, "GIF89a"u8.ToArray(), Gif),
			new(0, new byte[] { 0x49, 0x49, 0x2A, 0x00 }, Tiff),
			new(0, new byte[] { 0x4D, 0x4D, 0x00, 0x2A }, Tiff),
			new(0, "8BPS"u8.ToArray(), Psd),
			new(0, "AT&TFORM"u8.ToArray(), Djvu),
			new(0, "%PDF-"u8.ToArray(), Pdf),
			new(0, "%!PS"u8.ToArray(), PostScript),
			new(0, "{\\rtf"u8.ToArray(), Rtf),
			new(0, new byte[] { 0x52, 0x61, 0x72, 0x21, 0x1A, 0x07, 0x01, 0x00 }, Rar),
			new(0, new byte[] { 0x52, 0x61, 0x72, 0x21, 0x1A, 0x07, 0x00 }, Rar),
			new(0, new byte[] { 0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C }, SevenZip),
			new(0, new byte[] { 0x1F, 0x8B }, Gzip),
			new(0, "BZh"u8.ToArray(), Bzip2),
			new(0, new byte[] { 0xFD, 0x37, 0x7A, 0x58, 0x5A, 0x00 }, Xz),
			new(0, new byte[] { 0x28, 0xB5, 0x2F, 0xFD }, Zstd),
			new(0, "MSCF"u8.ToArray(), Cab),
			new(257, "ustar"u8.ToArray(), Tar),
			new(0, "ID3"u8.ToArray(), Mp3),
			new(0, "fLaC"u8.ToArray(), Flac),
			new(0, "MThd"u8.ToArray(), Midi),
			new(0, "#!AMR"u8.ToArray(), Amr),
			new(0, "FLV"u8.ToArray(), Flv),
			new(0, new byte[] { 0x30, 0x26, 0xB2, 0x75, 0x8E, 0x66, 0xCF, 0x11 }, Wmv),
			new(0, "wOFF"u8.ToArray(), Woff),
			new(0, "wOF2"u8.ToArray(), Woff2),
			new(0, "OTTO"u8.ToArray(), Otf),
			new(0, "SQLite format 3\0"u8.ToArray(), Sqlite),
			new(0, new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 }, OleCompound),
		};

		/// <summary>
		/// Short signatures that may occur in other data, checked after the containers.
		/// </summary>
		private static readonly Signature[] WeakSignatures =
		{
			new(0, new byte[] { 0x00, 0x00, 0x01, 0xBA }, Mpeg),
			new(0, new byte[] { 0x00, 0x00, 0x01, 0xB3 }, Mpeg),
			new(0, new byte[] { 0x00, 0x61, 0x73, 0x6D }, Wasm),
			new(0, new byte[] { 0x00, 0x01, 0x00, 0x00, 0x00 }, Ttf),
			new(0, new byte[] { 0x00, 0x00, 0x01, 0x00 }, Ico),
			new(0, "MZ"u8.ToArray(), Exe),
		};

		private static readonly byte[] EbmlMagic = { 0x1A, 0x45, 0xDF, 0xA3 };
		private static readonly byte[] Utf8Bom = { 0xEF, 0xBB, 0xBF };
		private static readonly byte[] Utf16LeBom = { 0xFF, 0xFE };
		private static readonly byte[] Utf16BeBom = { 0xFE, 0xFF };

		private static readonly byte[] WordDocumentName = Encoding.Unicode.GetBytes("WordDocument");
		private static readonly byte[] WorkbookName = Encoding.Unicode.GetBytes("Workbook");
		private static readonly byte[] BookName = Encoding.Unicode.GetBytes("Book");
		private static readonly byte[] PowerPointName = Encoding.Unicode.GetBytes("PowerPoint Document");
		private static readonly byte[] OutlookMessageName = Encoding.Unicode.GetBytes("__substg1.0_");

		/// <summary>
		/// Detects the content type of the stream by its first bytes. The position of a seekable stream is restored.
		/// </summary>
		/// <param name="stream">Stream positioned at the start of the file.</param>
		/// <returns>Content type and extension of the file; null for an unknown format.</returns>
		public static FileSignature Detect(Stream stream)
		{
			if (stream == null || !stream.CanRead)
				return null;

			var start = stream.CanSeek ? stream.Position : 0;
			var buffer = ArrayPool<byte>.Shared.Rent(HeaderSize);
			try
			{
				var count = stream.ReadAtLeast(buffer.AsSpan(0, HeaderSize), HeaderSize, throwOnEndOfStream: false);
				var header = buffer.AsSpan(0, count);
				var type = Detect(header, count < HeaderSize);
				if (ReferenceEquals(type, OleCompound))
					type = DetectOle(stream, start, header);
				else if (type == null)
					type = DetectIso(stream, start);

				return type == null ? null : new FileSignature(type.ContentType, type.Extension);
			}
			finally
			{
				ArrayPool<byte>.Shared.Return(buffer);
				if (stream.CanSeek)
					stream.Position = start;
			}
		}

		/// <summary>
		/// Detects the content type of the file by its first bytes.
		/// </summary>
		/// <param name="fileName">Full name of the file.</param>
		/// <returns>Content type and extension of the file; null for an unknown format.</returns>
		public static FileSignature Detect(string fileName)
		{
			using (var stream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 4096))
			{
				return Detect(stream);
			}
		}

		private static FileType Detect(ReadOnlySpan<byte> header, bool isWholeFile)
		{
			if (header.IsEmpty)
				return null;

			var type = Find(header, StrongSignatures)
				?? DetectZip(header)
				?? DetectRiff(header)
				?? DetectIsoMedia(header)
				?? DetectMatroska(header)
				?? DetectOgg(header)
				?? Find(header, WeakSignatures)
				?? DetectMpegAudio(header)
				?? DetectMpegTs(header)
				?? DetectBmp(header);

			return type ?? DetectText(header, isWholeFile);
		}

		private static FileType Find(ReadOnlySpan<byte> header, Signature[] signatures)
		{
			foreach (var signature in signatures)
			{
				if (Matches(header, signature.Offset, signature.Magic))
					return signature.Type;
			}

			return null;
		}

		private static bool Matches(ReadOnlySpan<byte> header, int offset, ReadOnlySpan<byte> magic)
		{
			return header.Length >= offset + magic.Length
				   && header.Slice(offset, magic.Length).SequenceEqual(magic);
		}

		/// <summary>
		/// ZIP and formats built on it: the first entries name the inner format.
		/// </summary>
		private static FileType DetectZip(ReadOnlySpan<byte> header)
		{
			if (!Matches(header, 0, "PK\x03\x04"u8) && !Matches(header, 0, "PK\x05\x06"u8) && !Matches(header, 0, "PK\x07\x08"u8))
				return null;

			// ODF and EPUB: the first entry is an uncompressed "mimetype" file
			if (Matches(header, 30, "mimetype"u8))
			{
				var mime = header.Slice(38, Math.Min(header.Length - 38, 64));
				if (mime.StartsWith("application/epub+zip"u8))
					return Epub;
				if (mime.StartsWith("application/vnd.oasis.opendocument.text"u8))
					return Odt;
				if (mime.StartsWith("application/vnd.oasis.opendocument.spreadsheet"u8))
					return Ods;
				if (mime.StartsWith("application/vnd.oasis.opendocument.presentation"u8))
					return Odp;
			}

			// Office Open XML: entry names are stored uncompressed in the local file headers
			if (header.IndexOf("word/"u8) >= 0)
				return Docx;
			if (header.IndexOf("xl/"u8) >= 0)
				return Xlsx;
			if (header.IndexOf("ppt/"u8) >= 0)
				return Pptx;
			if (header.IndexOf("AndroidManifest.xml"u8) >= 0)
				return Apk;
			if (header.IndexOf("META-INF/MANIFEST.MF"u8) >= 0)
				return Jar;

			return Zip;
		}

		private static FileType DetectRiff(ReadOnlySpan<byte> header)
		{
			if (!Matches(header, 0, "RIFF"u8))
				return null;
			if (Matches(header, 8, "WAVE"u8))
				return Wav;
			if (Matches(header, 8, "AVI "u8))
				return Avi;
			if (Matches(header, 8, "WEBP"u8))
				return Webp;

			return null;
		}

		/// <summary>
		/// ISO base media file format (mp4, mov, m4a, 3gp, heic, avif): the brand follows "ftyp".
		/// </summary>
		private static FileType DetectIsoMedia(ReadOnlySpan<byte> header)
		{
			if (!Matches(header, 4, "ftyp"u8) || header.Length < 12)
				return null;

			var brand = header.Slice(8, 4);
			if (brand.SequenceEqual("qt  "u8))
				return QuickTime;
			if (brand.SequenceEqual("M4A "u8) || brand.SequenceEqual("M4B "u8))
				return M4a;
			if (brand.SequenceEqual("heic"u8) || brand.SequenceEqual("heix"u8) || brand.SequenceEqual("hevc"u8)
				|| brand.SequenceEqual("mif1"u8) || brand.SequenceEqual("msf1"u8))
				return Heic;
			if (brand.SequenceEqual("avif"u8) || brand.SequenceEqual("avis"u8))
				return Avif;
			if (brand.StartsWith("3g"u8))
				return ThreeGp;

			return Mp4;
		}

		private static FileType DetectMatroska(ReadOnlySpan<byte> header)
		{
			if (!Matches(header, 0, EbmlMagic))
				return null;

			return header.IndexOf("webm"u8) >= 0 ? WebM : Mkv;
		}

		private static FileType DetectOgg(ReadOnlySpan<byte> header)
		{
			if (!Matches(header, 0, "OggS"u8))
				return null;

			return header.IndexOf("theora"u8) >= 0 ? OggVideo : Ogg;
		}

		/// <summary>
		/// MPEG audio frame sync: 11 set bits. Layer 0 is AAC (ADTS), layers 1-3 are MPEG audio (mp3).
		/// </summary>
		private static FileType DetectMpegAudio(ReadOnlySpan<byte> header)
		{
			if (header.Length < 2 || header[0] != 0xFF || (header[1] & 0xE0) != 0xE0)
				return null;

			var layer = (header[1] >> 1) & 0x03;
			if (layer == 0)
				return (header[1] & 0xF6) == 0xF0 ? Aac : null;

			return Mp3;
		}

		/// <summary>
		/// MPEG transport stream: 188-byte packets starting with 0x47.
		/// </summary>
		private static FileType DetectMpegTs(ReadOnlySpan<byte> header)
		{
			if (header.Length <= 188 || header[0] != 0x47 || header[188] != 0x47)
				return null;
			if (header.Length > 376 && header[376] != 0x47)
				return null;

			return MpegTs;
		}

		/// <summary>
		/// "BM" alone is too short: the reserved fields must be zero and the DIB header size known.
		/// </summary>
		private static FileType DetectBmp(ReadOnlySpan<byte> header)
		{
			if (header.Length < 18 || header[0] != (byte)'B' || header[1] != (byte)'M')
				return null;
			if (BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(6, 4)) != 0)
				return null;

			var dibSize = BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(14, 4));
			return dibSize is 12 or 40 or 52 or 56 or 64 or 108 or 124 ? Bmp : null;
		}

		/// <summary>
		/// Text formats: valid UTF-8 or UTF-16 with a BOM, without control characters.
		/// </summary>
		private static FileType DetectText(ReadOnlySpan<byte> header, bool isWholeFile)
		{
			if (Matches(header, 0, Utf16LeBom) || Matches(header, 0, Utf16BeBom))
				return Text;

			var text = Matches(header, 0, Utf8Bom) ? header.Slice(3) : header;
			if (!isWholeFile)
				text = TrimIncompleteUtf8(text);
			if (text.IsEmpty || !Utf8.IsValid(text) || HasControlCharacters(text))
				return null;

			var content = text.TrimStart(" \t\r\n"u8);
			if (StartsWithIgnoreCase(content, "<?xml"u8))
				return content.IndexOf("<svg"u8) >= 0 ? Svg : Xml;
			if (StartsWithIgnoreCase(content, "<svg"u8))
				return Svg;
			if (StartsWithIgnoreCase(content, "<!doctype html"u8) || StartsWithIgnoreCase(content, "<html"u8))
				return Html;
			if (IsJson(content, isWholeFile))
				return Json;
			if (IsCsv(content))
				return Csv;

			return Text;
		}

		private static bool IsJson(ReadOnlySpan<byte> content, bool isWholeFile)
		{
			if (content.IsEmpty || (content[0] != (byte)'{' && content[0] != (byte)'['))
				return false;
			if (!isWholeFile)
				return true;

			var end = content.TrimEnd(" \t\r\n"u8);
			return end[^1] == (byte)(content[0] == (byte)'{' ? '}' : ']');
		}

		/// <summary>
		/// The first line has at least two ';' or tab separators (Excel exports in the Russian locale use ';').
		/// </summary>
		private static bool IsCsv(ReadOnlySpan<byte> content)
		{
			var lineEnd = content.IndexOfAny((byte)'\r', (byte)'\n');
			var line = lineEnd >= 0 ? content.Slice(0, lineEnd) : content;
			return line.Count((byte)';') >= 2 || line.Count((byte)'\t') >= 2;
		}

		private static bool HasControlCharacters(ReadOnlySpan<byte> text)
		{
			foreach (var b in text)
			{
				if (b < 0x20 && b != (byte)'\t' && b != (byte)'\n' && b != (byte)'\r' && b != 0x0C && b != 0x1B)
					return true;
			}

			return false;
		}

		/// <summary>
		/// The header may cut a multi-byte UTF-8 character: drop its first bytes.
		/// </summary>
		private static ReadOnlySpan<byte> TrimIncompleteUtf8(ReadOnlySpan<byte> text)
		{
			for (var i = text.Length - 1; i >= 0 && i >= text.Length - 4; i--)
			{
				var b = text[i];
				if ((b & 0xC0) == 0x80)
					continue;
				if (b < 0x80)
					return text;

				var length = b >= 0xF0 ? 4 : b >= 0xE0 ? 3 : 2;
				return i + length <= text.Length ? text : text.Slice(0, i);
			}

			return text;
		}

		private static bool StartsWithIgnoreCase(ReadOnlySpan<byte> text, ReadOnlySpan<byte> prefix)
		{
			if (text.Length < prefix.Length)
				return false;

			for (var i = 0; i < prefix.Length; i++)
			{
				if ((text[i] | 0x20) != (prefix[i] | 0x20))
					return false;
			}

			return true;
		}

		/// <summary>
		/// OLE compound file (doc, xls, ppt, msg): reads the first directory sector and looks for the
		/// stream names. Falls back to doc when the directory cannot be read.
		/// </summary>
		private static FileType DetectOle(Stream stream, long start, ReadOnlySpan<byte> header)
		{
			if (header.Length < 0x34)
				return Doc;

			var sectorShift = BinaryPrimitives.ReadUInt16LittleEndian(header.Slice(0x1E, 2));
			var directorySector = BinaryPrimitives.ReadInt32LittleEndian(header.Slice(0x30, 4));
			if (sectorShift is not (9 or 12) || directorySector < 0)
				return Doc;

			var sectorSize = 1 << sectorShift;
			var offset = (long)(directorySector + 1) * sectorSize;
			byte[] rented = null;
			try
			{
				ReadOnlySpan<byte> directory;
				if (offset + sectorSize <= header.Length)
				{
					directory = header.Slice((int)offset, sectorSize);
				}
				else
				{
					if (!stream.CanSeek || start + offset + sectorSize > stream.Length)
						return Doc;

					rented = ArrayPool<byte>.Shared.Rent(sectorSize);
					stream.Position = start + offset;
					var read = stream.ReadAtLeast(rented.AsSpan(0, sectorSize), sectorSize, throwOnEndOfStream: false);
					directory = rented.AsSpan(0, read);
				}

				return DetectOleDirectory(directory);
			}
			finally
			{
				if (rented != null)
					ArrayPool<byte>.Shared.Return(rented);
			}
		}

		private static FileType DetectOleDirectory(ReadOnlySpan<byte> directory)
		{
			// a directory entry starts with the UTF-16 stream name
			for (var entry = 0; entry + OleDirectoryEntrySize <= directory.Length; entry += OleDirectoryEntrySize)
			{
				var name = directory.Slice(entry, 64);
				if (name.StartsWith(WordDocumentName))
					return Doc;
				if (name.StartsWith(WorkbookName) || name.StartsWith(BookName))
					return Xls;
				if (name.StartsWith(PowerPointName))
					return Ppt;
				if (name.StartsWith(OutlookMessageName))
					return Msg;
			}

			return Doc;
		}

		private static FileType DetectIso(Stream stream, long start)
		{
			if (!stream.CanSeek || stream.Length - start < IsoSignatureOffset + 5)
				return null;

			Span<byte> signature = stackalloc byte[5];
			stream.Position = start + IsoSignatureOffset;
			var read = stream.ReadAtLeast(signature, signature.Length, throwOnEndOfStream: false);
			return read == signature.Length && signature.SequenceEqual("CD001"u8) ? Iso : null;
		}

		private sealed record FileType(string ContentType, string Extension);

		private readonly record struct Signature(int Offset, byte[] Magic, FileType Type);
	}

	/// <summary>
	/// Content type of a file detected by its signature.
	/// </summary>
	/// <param name="ContentType">MIME type, e.g. "text/plain".</param>
	/// <param name="Extension">Usual file extension without the dot, e.g. "txt".</param>
	public sealed record FileSignature(string ContentType, string Extension);
}
