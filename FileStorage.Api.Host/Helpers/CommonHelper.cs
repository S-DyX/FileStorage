using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mime;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace FileStorage.Api.Host.Services
{
	public class FileContentType
	{
		public string ContentType { get; set; }
		public string Extension { get; set; }
	}

	public static class CommonHelper
	{
		// Partial() и GetResponseBodyDelegate() удалены: они копировали файл в MemoryStream и затем в массив
		// (две копии в памяти). Отдача с поддержкой Range — FileStreamResult с EnableRangeProcessing.

		/// <summary>
		/// Detects the content type of the file by its signature (magic bytes), see <see cref="FileSignatureDetector"/>.
		/// The position of the stream is restored.
		/// </summary>
		/// <param name="stream">File stream positioned at the start of the file.</param>
		/// <returns>Content type and extension; both are null for an unknown format.</returns>
		public static FileContentType GetContentType(this Stream stream)
		{
			return FileSignatureDetector.Detect(stream);
		}

		public static string GetQueryParameter(this HttpRequest request, string parameterName)
		{
			var parameter = request.Query[parameterName];

			return parameter.FirstOrDefault() == null
				? String.Empty
				: parameter.ToString();
		}
	}
}
