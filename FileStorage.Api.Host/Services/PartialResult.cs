using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace FileStorage.Api.Host.Services
{

	public class PartialResult : IActionResult
	{
		private readonly FileStreamResult _streamResult;
		private const string ContentLength = "Content-Length";

		private const string AcceptRanges = "Accept-Ranges";

		private const string ContentRange = "Content-Range";

		private const string ContentDisposition = "Content-Disposition";



		private readonly Stream _source;

		private readonly long _contentLength;

		private readonly long _rangeStart;

		private readonly long _rangeEnd;

		private static Regex _regex = new Regex(@"(\d+)-(\d+)?");
		public PartialResult(FileStreamResult streamResult)
		{
			_streamResult = streamResult;
		}

		public async Task ExecuteResultAsync(ActionContext context)
		{
			context.HttpContext.Request.Partial(context.HttpContext.Response, _streamResult.FileStream, _streamResult.FileDownloadName);
			//foreach (var item in this)
			//{
			//	if (item.Stream != null)
			//	{
			//		var content = new StreamContent(item.Stream);

			//		if (item.ContentType != null)
			//		{
			//			content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(item.ContentType);
			//		}

			//		if (item.FileName != null)
			//		{
			//			var contentDisposition = new ContentDispositionHeaderValue("attachment");
			//			contentDisposition.SetHttpFileName(item.FileName);
			//			content.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("attachment");
			//			content.Headers.ContentDisposition.FileName = contentDisposition.FileName.Value;
			//			content.Headers.ContentDisposition.FileNameStar = contentDisposition.FileNameStar.Value;
			//		}

			//		this._content.Add(content);
			//	}
			//}

			
			context.HttpContext.Response.ContentType = _streamResult.ContentType;
			//var content = new System.Net.Http.MultipartContent("byteranges");
			var bodyLength = context.HttpContext.Response.Body.Length;
			var bytes = new byte[bodyLength];
			context.HttpContext.Response.Body.Write(bytes, 0, (int)bodyLength);
			var content = new FileContentResult(bytes, _streamResult.ContentType);
			await content.ExecuteResultAsync(context);
		}
	}
}
