//using System;
//using System.Collections.Generic;
//using System.Globalization;
//using System.IO;
//using System.Linq;
//using System.Net;
//using System.Net.Http;
//using System.Text.RegularExpressions;
//using System.Threading.Tasks;
//using Microsoft.AspNetCore.Http;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.Net.Http.Headers;

//namespace FileStorage.Api.Host.Services
//{
//	public class PartialContentResponse : IActionResult
//	{
//		private const string ContentLength = "Content-Length";

//		private const string AcceptRanges = "Accept-Ranges";

//		private const string ContentRange = "Content-Range";

//		private const string ContentDisposition = "Content-Disposition";

//		private readonly HttpRequest _context;

//		private readonly Stream _source;

//		private readonly long _contentLength;

//		private readonly long _rangeStart;

//		private readonly long _rangeEnd;

//		private static Regex _regex = new Regex(@"(\d+)-(\d+)?");

//		public PartialContentResponse(Stream source, string contentType, HttpRequest context, HttpResponse response, bool forceDownload = false, string fileName = null)
//		{
//			if (forceDownload && fileName == null) throw new ArgumentNullException("fileName", "fileName must be provided when forceDownload is true.");

//			var _contentLength = source.Length;
//			response.Headers["Content-Length"] = _contentLength.ToString(CultureInfo.InvariantCulture);
//			response.Headers["Accept-Ranges"] = "bytes";
//			var rangeHeader = context.Headers["Range"].FirstOrDefault();
//			long _rangeStart = 0;
//			long _rangeEnd = 5;
//			if (!string.IsNullOrEmpty(rangeHeader) && rangeHeader.Contains("="))
//			{
//				var start = rangeHeader.Split('=')[1];
//				var m = _regex.Match(start);
//				_rangeStart = long.Parse(m.Groups[1].Value);
//				_rangeEnd = _contentLength - 1;
//				if (m.Groups[2] != null && !string.IsNullOrWhiteSpace(m.Groups[2].Value))
//				{
//					_rangeEnd = Convert.ToInt64(m.Groups[2].Value);
//				}
//			}
//			else
//			{
//				response.Headers[ContentRange] = $"bytes {_rangeStart}-{_rangeEnd}/{_contentLength}";
//				response.StatusCode = (int)HttpStatusCode.PartialContent;
//			}
//			response.Headers[ContentLength] = (_rangeEnd + 1 - _rangeStart).ToString();
//			if (!string.IsNullOrWhiteSpace(fileName))
//				response.Headers[ContentDisposition] = $"attachment; filename={fileName}";

//			response.Body = this.GetResponseBodyDelegate(source);
//			response.Headers["Content-Length"] = _contentLength.ToString(CultureInfo.InvariantCulture);
//		}


//		private Stream GetResponseBodyDelegate(Stream sourceDelegate)
//		{
//			var stream = new MemoryStream();
//			if (this._rangeStart == 0 && this._rangeEnd == this._contentLength - 1)
//			{
//				this._source.CopyTo(stream);
//			}
//			else
//			{
//				if (!this._source.CanSeek)
//				{
//					throw new InvalidOperationException(
//						"Sending Range Responses requires a seekable stream eg. FileStream or MemoryStream");
//				}

//				var totalBytesToSend = this._rangeEnd - this._rangeStart + 1;
//				var bufferSize = 0x1000;
//				if (bufferSize > totalBytesToSend)
//					bufferSize = (int)totalBytesToSend;
//				var buffer = new byte[bufferSize];
//				var bytesRemaining = totalBytesToSend;

//				this._source.Seek(this._rangeStart, SeekOrigin.Begin);
//				while (bytesRemaining > 0)
//				{
//					var count = (int)Math.Min(bytesRemaining, buffer.Length);
//					this._source.Read(buffer, 0, count);

//					try
//					{
//						stream.Write(buffer, 0, count);
//						bytesRemaining -= count;
//					}
//					catch (Exception httpException)
//					{
//						/* in Asp.Net we can call HttpResponseBase.IsClientConnected
//						* to see if the client broke off the connection
//						* and avoid trying to flush the response stream.
//						* instead I'll swallow the exception that IIS throws in this situation
//						* and rethrow anything else.*/
//						if (httpException.Message
//							== "An error occurred while communicating with the remote host. The error code is 0x80070057.") return null;

//						throw;
//					}
//				}
//			}

//			return stream;
//		}

//		public Task ExecuteResultAsync(ActionContext context)
//		{
//			foreach (var item in this)
//			{
//				if (item.Stream != null)
//				{
//					var content = new StreamContent(item.Stream);

//					if (item.ContentType != null)
//					{
//						content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(item.ContentType);
//					}

//					if (item.FileName != null)
//					{
//						var contentDisposition = new ContentDispositionHeaderValue("attachment");
//						contentDisposition.SetHttpFileName(item.FileName);
//						content.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("attachment");
//						content.Headers.ContentDisposition.FileName = contentDisposition.FileName.Value;
//						content.Headers.ContentDisposition.FileNameStar = contentDisposition.FileNameStar.Value;
//					}

//					this._content.Add(content);
//				}
//			}

//			context.HttpContext.Response.ContentLength = _content.Headers.ContentLength;
//			context.HttpContext.Response.ContentType = _content.Headers.ContentType.ToString();

//			await _content.CopyToAsync(context.HttpContext.Response.Body);
//		}
//	}
//}
