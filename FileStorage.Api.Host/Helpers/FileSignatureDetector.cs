using System.IO;

namespace FileStorage.Api.Host.Services
{
	/// <summary>
	/// Detects the content type of a file by its signature, see FileStorage.Core.Helpers.FileSignatureDetector.
	/// </summary>
	internal static class FileSignatureDetector
	{
		/// <summary>
		/// Detects the content type of the stream by its first bytes. The position of a seekable stream
		/// is restored; for an unknown format both properties of the result are null.
		/// </summary>
		/// <param name="stream">Stream positioned at the start of the file.</param>
		/// <returns>Content type and extension of the file.</returns>
		public static FileContentType Detect(Stream stream)
		{
			var signature = global::FileStorage.Core.Helpers.FileSignatureDetector.Detect(stream);
			return new FileContentType
			{
				ContentType = signature?.ContentType,
				Extension = signature?.Extension
			};
		}
	}
}
