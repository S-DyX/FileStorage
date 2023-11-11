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
		private const string ContentLength = "Content-Length";

		private const string AcceptRanges = "Accept-Ranges";

		private const string ContentRange = "Content-Range";

		private const string ContentDisposition = "Content-Disposition";

		private static Regex _regex = new Regex(@"(\d+)-(\d+)?");
		public static FileContentResult Partial(this HttpRequest context, HttpResponse response, Stream stream, string contentType, string fileName = null)
		{
			var contentLength = stream.Length;
			response.Headers.ContentLength = contentLength;

			response.Headers["Accept-Ranges"] = "bytes";
			var rangeHeader = context.Headers["Range"].FirstOrDefault();
			long rangeStart = 0;
			long rangeEnd = 5;
			if (!string.IsNullOrEmpty(rangeHeader) && rangeHeader.Contains("="))
			{
				var start = rangeHeader.Split('=')[1];
				var m = _regex.Match(start);
				rangeStart = long.Parse(m.Groups[1].Value);
				rangeEnd = contentLength - 1;
				if (m?.Groups[2] != null && !string.IsNullOrWhiteSpace(m.Groups[2].Value))
				{
					rangeEnd = Convert.ToInt64(m.Groups[2].Value);
				}
			}
			else
			{
				response.Headers[ContentRange] = $"bytes {rangeStart}-{rangeEnd}/{contentLength}";
				response.StatusCode = (int)HttpStatusCode.PartialContent;
			}
			
			if (!string.IsNullOrWhiteSpace(fileName))
				response.Headers[ContentDisposition] = $"attachment; filename={fileName}";

			var streamFileStream =  GetResponseBodyDelegate(stream, rangeStart, rangeEnd);
			var bodyLength = streamFileStream.Length;
			response.Headers["Content-Range"] = $"bytes {rangeStart}-{rangeEnd}/{stream.Length}";
			var bytes = new byte[bodyLength];
			streamFileStream.Read(bytes, 0, (int)bodyLength);
			streamFileStream.Position = 0;
			var content = new FileContentResult(bytes, contentType);
			return content;
			
		}
		private static Stream GetResponseBodyDelegate(Stream source, long rangeStart, long rangeEnd)
		{
			var stream = new MemoryStream();
			{
				if (rangeStart == 0 && rangeEnd == source.Length - 1)
				{
					source.CopyTo(stream);
				}
				else
				{
					if (!source.CanSeek)
					{
						throw new InvalidOperationException(
							"Sending Range Responses requires a seekable stream eg. FileStream or MemoryStream");
					}

					var totalBytesToSend = rangeEnd - rangeStart + 1;
					var bufferSize = 0x1000;
					if (bufferSize > totalBytesToSend)
						bufferSize = (int)totalBytesToSend;
					var buffer = new byte[bufferSize];
					var bytesRemaining = totalBytesToSend;

					source.Seek(rangeStart, SeekOrigin.Begin);
					while (bytesRemaining > 0)
					{
						var count = (int)Math.Min(bytesRemaining, buffer.Length);
						source.Read(buffer, 0, count);

						try
						{
							stream.Write(buffer, 0, count);
							bytesRemaining -= count;
						}
						catch (Exception httpException)
						{
							/* in Asp.Net we can call HttpResponseBase.IsClientConnected
							* to see if the client broke off the connection
							* and avoid trying to flush the response stream.
							* instead I'll swallow the exception that IIS throws in this situation
							* and rethrow anything else.*/
							if (httpException.Message
								== "An error occurred while communicating with the remote host. The error code is 0x80070057.") return null;

							throw;
						}
					}
				}
			}
			stream.Position = 0;
			return stream;
		}

		public static FileContentType GetContentType(this Stream stream)
		{
			var result = new FileContentType();
			var bytes = new byte[8];

			stream.Read(bytes, 0, bytes.Length);

			var header = BitConverter.ToString(bytes).Replace("-", "");
			var upper = header.ToUpper();
			switch (upper)
			{
				case "EFBBBF282822D09F":
					result.ContentType = "text/plain";
					result.Extension = "txt";
					break;

				case "1F8B080000000000":
					result.ContentType = "application/x-gzip";
					result.Extension = "gz";
					break;
				case "526172211A0700CF":
				case "504B030414000200":
					result.ContentType = "application/zip";
					result.Extension = "zip";
					break;
				case "EFBBBF3B46726965":
					result.ContentType = "text/csv";
					result.Extension = "csv";
					break;

				case "504B030414000600":
					//xlsx
					result.ContentType = "text/xlsx";
					result.Extension = "xlsx";
					break;

				case "FFD8FFE000104A46":
					result.ContentType = "image/jpeg";
					result.Extension = "jpeg";
					break;
				case "526172211A0700":
				case "08000482CB5009DB":
				case "526172211A070100":
					result.ContentType = "application/zip";
					result.Extension = "rar";
					break;
				case "424DB61213000000":
				case "89504E470D0A1A0A":
					result.ContentType = "image/png";
					result.Extension = "png";
					break;

				case "EFBBBFD0B0D180D1":
					result.ContentType = "text";
					result.Extension = "txt";
					break;

				case "0000002066747970":
				case "0000001966747970":
				case "0000001866747970":
					result.ContentType = "video/mp4";
					result.Extension = "mp4";
					break;

				case "52494646CA890000":
					result.ContentType = "audio/wav";
					result.Extension = "wav";
					break;

				case "5249464676F9CD00":
					result.ContentType = "video/avi";
					result.Extension = "avi";
					break;

				case "255044462В312У35":
					result.ContentType = "application/pdf";
					result.Extension = "pdf";
					break;
				default:
					if (upper.StartsWith("526172211A0700")
						|| upper.StartsWith("504B0304")
						|| upper.StartsWith("504B0506")
						|| upper.StartsWith("504B0708"))
					{
						result.ContentType = "application/zip";
						result.Extension = "rar";
					}
					else if (upper.StartsWith("FFD8FFDB")
						|| upper.StartsWith("FFD8FFEE")
						|| upper.StartsWith("7869660000")
						|| upper.StartsWith("FFD8FFE1"))
					{
						result.ContentType = "image/jpeg";
						result.Extension = "jpeg";
					}
					else if (upper.StartsWith("255044462D"))
					{
						result.ContentType = "application/pdf";
						result.Extension = "pdf";
					}
					else if (upper.StartsWith("494433") || upper.StartsWith("FFFB"))
					{
						result.ContentType = "application/mp3";
						result.Extension = "mp3";
					}
					else if (upper.StartsWith("4344303031") || upper.StartsWith("454D5533"))
					{
						result.ContentType = "application/octet-stream";
						result.Extension = "iso";
					}
					else if (upper.StartsWith("D0CF11E0A1B11AE1"))
					{
						result.ContentType = "application/doc";
						result.Extension = "doc";
					}


					break;
			}

			stream.Position = 0;

			return result;
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
