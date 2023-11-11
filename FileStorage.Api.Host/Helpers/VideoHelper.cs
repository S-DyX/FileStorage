//using Microsoft.AspNetCore.Http;
//using System;
//using System.Collections.Generic;
//using System.Globalization;
//using System.IO;
//using System.Linq;
//using System.Net;
//using System.Text.RegularExpressions;
//using System.Threading.Tasks;

//namespace FileStorage.Api.Host.Helpers
//{
//	public static class VideoHelper
//	{
//		public static HttpResponse FromPartialFile(this IResponseFormatter f,
//			Request req,
//			Stream stream,
//			string contentType)
//		{
//			return FromPartialStreamIos(f, req, stream, contentType);
//		}

//		private static Regex _regex = new Regex(@"(\d+)-(\d+)?");
//		public static HttpResponse FromPartialStreamNew(this IResponseFormatter f,
//													HttpRequest req, Stream stream,
//			string contentType)
//		{


//			// Store the len
//			var contentLength = stream.Length;

//			// Create the response now


//			// Use the partial status code

//			var stream2 = stream;
//			var rangeStart = 0l;
//			var rangeEnd = 0l;
//			var contentRange = string.Empty;
//			var statusCode = HttpStatusCode.OK;
//			var rangeHeader = req.Headers["Range"].FirstOrDefault();
//			if (!string.IsNullOrEmpty(rangeHeader) && rangeHeader.Contains("="))
//			{
//				var start = rangeHeader.Split('=')[1];
//				var m = _regex.Match(start);
//				rangeStart = long.Parse(m.Groups[1].Value);
//				rangeEnd = contentLength - 1;
//				if (m.Groups[2] != null && !string.IsNullOrWhiteSpace(m.Groups[2].Value))
//				{
//					rangeEnd = Convert.ToInt64(m.Groups[2].Value);
//				}

//				if (rangeStart < 0 || rangeEnd > contentLength - 1)
//				{
//					statusCode = HttpStatusCode.RequestedRangeNotSatisfiable;
//					stream2 = new MemoryStream();
//				}
//				else
//				{
//					contentRange = $"bytes {rangeStart}-{rangeEnd}/{contentLength}";
//					statusCode = HttpStatusCode.PartialContent;
//					stream2 = stream.GetResponseBodyDelegate(rangeStart, rangeEnd, contentLength);
//				}
//			}
//			else
//			{
//				rangeStart = 0;
//				rangeEnd = 5;
//				stream2 = stream.GetResponseBodyDelegate(rangeStart, rangeEnd, contentLength);
//			}
//			contentLength = rangeEnd + 1 - rangeStart;

//			var res = f.FromStream(stream2, contentType)
//				.WithHeader(AcceptRanges, $"bytes")
//				.WithHeader(ContentLength, contentLength.ToString(CultureInfo.InvariantCulture));




//			if (!string.IsNullOrEmpty(contentRange))
//				res.WithHeader(ContentRange, contentRange);
//			res.StatusCode = statusCode;

//			res.Contents = x =>
//			{
//				stream2.CopyTo(x);
//			};
//			return res;
//		}




//		public static Stream GetResponseBodyDelegate(this Stream source, long rangeStart, long rangeEnd, long contentLength)
//		{
//			var stream = new MemoryStream();

//			if (rangeStart == 0 && rangeEnd == contentLength - 1) source.CopyTo(stream);
//			else
//			{
//				if (!source.CanSeek)
//					throw new InvalidOperationException(
//						"Sending Range Responses requires a seekable stream eg. FileStream or MemoryStream");

//				var totalBytesToSend = rangeEnd - rangeStart + 1;
//				int BufferSize = 0x1000;
//				if (BufferSize > totalBytesToSend)
//					BufferSize = (int)totalBytesToSend;
//				var buffer = new byte[BufferSize];
//				var bytesRemaining = totalBytesToSend;

//				source.Seek(rangeStart, SeekOrigin.Begin);
//				while (bytesRemaining > 0)
//				{
//					var count = bytesRemaining <= buffer.Length
//									? source.Read(buffer, 0, (int)Math.Min(bytesRemaining, int.MaxValue))
//									: source.Read(buffer, 0, buffer.Length);

//					try
//					{
//						stream.Write(buffer, 0, count);
//						stream.Flush();
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

//		private const string ContentLength = "Content-Length";

//		private const string AcceptRanges = "Accept-Ranges";

//		private const string ContentRange = "Content-Range";

//		private const string ContentDisposition = "Content-Disposition";

//		public static HttpResponse FromPartialStreamOld(this HttpResponse f,
//													HttpRequest req, Stream stream,
//			string contentType)
//		{


//			// Store the len
//			var len = stream.Length;
//			// Create the response now


//			// Use the partial status code

//			long startI = 0;
//			var end = 1024l;
//			var length = len - startI;
//			var contentRange = string.Empty;
//			foreach (var s in req.Headers["Range"])
//			{
//				var start = s.Split('=')[1];
//				var m = Regex.Match(start, @"(\d+)-(\d+)?");
//				start = m.Groups[1].Value;
//				end = len - 1;
//				if (m.Groups[2] != null && !string.IsNullOrWhiteSpace(m.Groups[2].Value))
//				{
//					end = Convert.ToInt64(m.Groups[2].Value);
//				}

//				startI = Convert.ToInt64(start);
//				length = len - startI;

//				contentRange = "bytes " + start + "-" + end + "/" + len;


//			}

//			stream.Seek(startI, SeekOrigin.Begin);


//			var res = f.FromStream(stream, contentType)
//				.WithHeader("connection", "keep-alive")
//				.WithHeader("accept-ranges", "bytes");

//			res.WithHeader("content-length", length.ToString(CultureInfo.InvariantCulture));
//			res.WithHeader("Accept-Ranges", $"{0}-{length}");

//			if (!string.IsNullOrEmpty(contentRange))
//				res.WithHeader("content-range", contentRange);
//			res.StatusCode = HttpStatusCode.PartialContent;
//			//stream.Seek(startI, SeekOrigin.Begin);
//			return res;
//		}


//		public static HttpResponse FromPartialStreamIos(this HttpResponse f,
//													HttpRequest req, Stream stream,
//			string contentType)
//		{
//			var res = new PartialContentResponse(stream, contentType, f.HttpContext);

//			return res;
//		}


//	}
//}
