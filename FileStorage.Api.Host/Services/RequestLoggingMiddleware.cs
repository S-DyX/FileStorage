using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace FileStorage.Api.Host.Services
{
	public class RequestLoggingMiddleware
	{
		private readonly RequestDelegate _next;
		private readonly ILogger _logger;

		public RequestLoggingMiddleware(RequestDelegate next, ILoggerFactory loggerFactory)
		{
			_next = next;
			_logger = loggerFactory.CreateLogger<RequestLoggingMiddleware>();
		}

		public async Task Invoke(HttpContext context)
		{
			context.Request.EnableBuffering();

			// Leave the body open so the next middleware can read it.

			try
			{

				await _next(context);
			}
			catch (Exception ex)
			{
				var content = GetDetails(context.Request);
				_logger.LogError(ex,
					$"Request {context.Request?.Method} {context.Request?.Path.Value} => {context.Response?.StatusCode}, Request:{content}");
				throw;
			}
			finally
			{
				var content = GetDetails(context.Request);

				if (content.Length > 10000)
				{
					_logger.LogInformation(
						$"Request {context.Request?.Method} {context.Request?.Path.Value} => {context.Response?.StatusCode}, Request:{content}");
				}
				else
				{
					_logger.LogInformation(content);


				}
			}
		}
		public static string GetDetails(HttpRequest request)
		{
			string baseUrl = $"{request.Scheme}://{request.Host}{request.Path}{request.QueryString.Value}";
			StringBuilder sbHeaders = new StringBuilder();
			foreach (var header in request.Headers)
				sbHeaders.Append($"{header.Key}: {header.Value}\n");

			string body = "no-body";
			if (request.Body.Length > 1024 * 5)
			{

			}
			else
			{
				using (var reader = new StreamReader(
					request.Body,
					encoding: Encoding.UTF8,
					detectEncodingFromByteOrderMarks: false,
					bufferSize: 2222,
					leaveOpen: true))
				{
					request.Body.Position = 0;
					body = reader.ReadToEndAsync().Result;
					// Do some processing with body…

					// Reset the request body stream position so the next middleware can read it
					request.Body.Position = 0;
				}
			}



			return $"{request.Protocol} {request.Method} {baseUrl}\n\n{sbHeaders}\n{body}";
		}
	}
}
