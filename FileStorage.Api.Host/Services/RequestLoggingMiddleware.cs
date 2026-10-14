using System;
using System.Collections.Generic;
using System.Diagnostics;
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
		// тело запроса попадает в лог, только если оно не больше этого размера
		private const int MaxLoggedBodySize = 1024 * 5;

		private readonly RequestDelegate _next;
		private readonly ILogger _logger;

		public RequestLoggingMiddleware(RequestDelegate next, ILoggerFactory loggerFactory)
		{
			_next = next;
			_logger = loggerFactory.CreateLogger<RequestLoggingMiddleware>();
		}

		public async Task Invoke(HttpContext context)
		{
			var request = context.Request;
			// Буферизуем только небольшие запросы с известной длиной — только их тело и попадает в лог.
			// Раньше буферизовалось тело каждого запроса, включая загрузку файлов: лишняя копия
			// каждого загружаемого файла в памяти или во временном файле.
			if (request.ContentLength.HasValue && request.ContentLength.Value <= MaxLoggedBodySize)
			{
				request.EnableBuffering();
			}

			var stopwatch = Stopwatch.StartNew();
			try
			{
				await _next(context);
			}
			catch (Exception ex)
			{
				var content = await GetDetailsAsync(request);
				_logger.LogError(ex,
					$"Request {request.Method} {request.Path.Value} => {context.Response?.StatusCode}, Request:{content}");
				throw;
			}

			// На каждый запрос — короткая строка; заголовки и тело — только при уровне Debug.
			// Раньше они собирались и писались в лог на уровне Information для каждого запроса.
			_logger.LogInformation($"Request {request.Method} {request.Path.Value} => {context.Response?.StatusCode} ({stopwatch.ElapsedMilliseconds} ms)");
			if (_logger.IsEnabled(LogLevel.Debug))
			{
				_logger.LogDebug(await GetDetailsAsync(request));
			}
		}

		public static async Task<string> GetDetailsAsync(HttpRequest request)
		{
			string baseUrl = $"{request.Scheme}://{request.Host}{request.Path}{request.QueryString.Value}";
			var sbHeaders = new StringBuilder();
			foreach (var header in request.Headers)
				sbHeaders.Append($"{header.Key}: {header.Value}\n");

			string body = "no-body";
			// тело читаем, только если оно буферизовано и небольшое
			if (request.Body.CanSeek && request.Body.Length <= MaxLoggedBodySize)
			{
				request.Body.Position = 0;
				using (var reader = new StreamReader(
					request.Body,
					encoding: Encoding.UTF8,
					detectEncodingFromByteOrderMarks: false,
					bufferSize: 2222,
					leaveOpen: true))
				{
					body = await reader.ReadToEndAsync();
				}
				// возвращаем позицию, чтобы тело могли прочитать дальше
				request.Body.Position = 0;
			}

			return $"{request.Protocol} {request.Method} {baseUrl}\n\n{sbHeaders}\n{body}";
		}
	}
}
