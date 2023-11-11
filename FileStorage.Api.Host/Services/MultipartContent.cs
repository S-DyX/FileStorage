using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace FileStorage.Api.Host.Services
{
    public class MultipartContent
    {
        public string ContentType { get; set; }

        public string FileName { get; set; }

        public Stream Stream { get; set; }
    }

    public class MultipartResult : Collection<MultipartContent>, IActionResult
    {
        protected readonly System.Net.Http.MultipartContent _content;

        public MultipartResult(string subtype = "byteranges", string boundary = null)
        {
            if (boundary == null)
            {
                this._content = new System.Net.Http.MultipartContent(subtype);
            }
            else
            {
                this._content = new System.Net.Http.MultipartContent(subtype, boundary);
            }
        }

        public async Task ExecuteResultAsync(ActionContext context)
		{
			foreach (var item in this)
			{
				if (item.Stream != null)
				{
					var content = new StreamContent(item.Stream);

					if (item.ContentType != null)
					{
						content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(item.ContentType);
					}

					if (item.FileName != null)
					{
						var contentDisposition = new ContentDispositionHeaderValue("attachment");
						contentDisposition.SetHttpFileName(item.FileName);
						content.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("attachment");
						content.Headers.ContentDisposition.FileName = contentDisposition.FileName.Value;
						content.Headers.ContentDisposition.FileNameStar = contentDisposition.FileNameStar.Value;
					}

					this._content.Add(content);
				}
			}

			context.HttpContext.Response.ContentLength = _content.Headers.ContentLength;
			context.HttpContext.Response.ContentType = _content.Headers.ContentType.ToString();

			await _content.CopyToAsync(context.HttpContext.Response.Body);
        }
    }
}
