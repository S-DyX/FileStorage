using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json;
using Service.Registry.Utils;

namespace FileStorage.Contracts.Rest.Impl.FileStorage
{
	/// <summary>
	/// Общие операции REST-клиентов: кодирование параметров запроса, GET/POST с проверкой ответа
	/// </summary>
	internal static class RestHelper
	{
		/// <summary>
		/// Значение параметра запроса в URL-кодировке (раньше id и имена подставлялись как есть,
		/// и символы вроде '&amp;', '#', '+' или пробела ломали запрос)
		/// </summary>
		public static string Encode(string value)
		{
			return Uri.EscapeDataString(value ?? string.Empty);
		}

		/// <summary>
		/// Добавляет к адресу имя хранилища, если оно задано
		/// </summary>
		public static string WithName(string address, string storageName)
		{
			if (string.IsNullOrEmpty(storageName))
				return address;
			return address + (address.Contains("?") ? "&" : "?") + "name=" + Encode(storageName);
		}

		public static string GetString(HttpClient client, string url)
		{
			return RunSyncUtil.RunSync(() => client.GetStringAsync(url));
		}

		/// <summary>
		/// GET без разбора тела ответа; true, если сервер ответил успешно
		/// </summary>
		public static bool GetOk(HttpClient client, string url)
		{
			using (var response = RunSyncUtil.RunSync(() => client.GetAsync(url)))
			{
				return response.IsSuccessStatusCode;
			}
		}

		/// <summary>
		/// POST JSON. Если сервер ответил ошибкой — исключение: раньше ошибки записи терялись молча.
		/// </summary>
		public static void PostJson(HttpClient client, string url, object request)
		{
			using (var content = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json"))
			using (var response = RunSyncUtil.RunSync(() => client.PostAsync(url, content)))
			{
				response.EnsureSuccessStatusCode();
			}
		}

		/// <summary>
		/// Открывает тело ответа как поток, не загружая его в память. null — файла нет (404 или пустой ответ).
		/// </summary>
		public static Stream OpenRead(HttpClient client, string url)
		{
			var response = RunSyncUtil.RunSync(() => client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead));
			if (response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.NoContent)
			{
				response.Dispose();
				return null;
			}
			try
			{
				response.EnsureSuccessStatusCode();
				var stream = RunSyncUtil.RunSync(() => response.Content.ReadAsStreamAsync());
				return new HttpResponseStream(response, stream);
			}
			catch
			{
				response.Dispose();
				throw;
			}
		}
	}

	/// <summary>
	/// Поток тела HTTP-ответа; при закрытии освобождает и сам ответ
	/// </summary>
	internal sealed class HttpResponseStream : Stream
	{
		private readonly HttpResponseMessage _response;
		private readonly Stream _stream;

		public HttpResponseStream(HttpResponseMessage response, Stream stream)
		{
			_response = response;
			_stream = stream;
		}

		public override bool CanRead => true;
		public override bool CanSeek => false;
		public override bool CanWrite => false;
		public override long Length => _response.Content.Headers.ContentLength ?? throw new NotSupportedException();
		public override long Position
		{
			get => throw new NotSupportedException();
			set => throw new NotSupportedException();
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			return _stream.Read(buffer, offset, count);
		}

		public override void Flush()
		{
		}

		public override long Seek(long offset, SeekOrigin origin)
		{
			throw new NotSupportedException();
		}

		public override void SetLength(long value)
		{
			throw new NotSupportedException();
		}

		public override void Write(byte[] buffer, int offset, int count)
		{
			throw new NotSupportedException();
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				_stream.Dispose();
				_response.Dispose();
			}
			base.Dispose(disposing);
		}
	}
}
