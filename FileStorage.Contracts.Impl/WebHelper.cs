using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace FileStorage.Contracts.Impl
{
	public static class WebHelper
	{
		public static string GetContent(string url, Encoding encoding)
		{
			return GetContent(url, encoding, null);
		}

		public static string GetContent(string url, Encoding encoding, string postData)
		{

			encoding = encoding ?? Encoding.UTF8;
			var request = (HttpWebRequest)WebRequest.Create(url);
			if (!string.IsNullOrEmpty(postData))
			{
				request.Method = "POST";
			}
			request.UserAgent = "Mozilla/5.0 (compatible; MSIE 10.0; Windows NT 6.1; WOW64; Trident/7.0)";
			request.Accept = "text/html, application/xhtml+xml, */*";
			request.Headers.Add("Accept-Language", "ru");
			request.ContentType = "application/json";
			request.KeepAlive = true;
			request.AllowAutoRedirect = false;
			//request.Timeout = 3500;



			if (!string.IsNullOrEmpty(postData))
			{
				// Write post data
				var dataBytes = encoding.GetBytes(postData);
				request.ContentLength = dataBytes.Length;
				using (var requestStream = request.GetRequestStream())
				{
					requestStream.Write(dataBytes, 0, dataBytes.Length);

				}
				return GetContent(encoding, request);
			}
			else
			{
				return GetContent(encoding, request);
			}
		}
		private static string GetContent(Encoding encoding, HttpWebRequest request)
		{
			using (var webResponse = (HttpWebResponse)request.GetResponse())
			{
				if (webResponse.Headers["Content-Type"] != null &&
					webResponse.Headers["Content-Type"].ToLower().IndexOf("windows-1251") > 0)
				{
					encoding = Encoding.GetEncoding("windows-1251");
				}
				if (webResponse.Headers["Content-Type"] != null && webResponse.Headers["Content-Type"].ToLower().Contains("utf-8"))
				{
					encoding = Encoding.UTF8;
				}


				using (var stream = webResponse.GetResponseStream())
				{
					if (stream != null)
					{
						using (var streamReader = new StreamReader(stream, encoding))
						{
							var res = string.Empty;
							if (Encoding.UTF8 != encoding)
							{
								var content = streamReader.ReadToEnd();
								var bytes = encoding.GetBytes(content);
								return Encoding.UTF8.GetString(Encoding.Convert(encoding, Encoding.UTF8, bytes));
							}
							var result = streamReader.ReadToEnd();
							webResponse.Close();
							return result;
						}
					}
				}
			}
			return string.Empty;
		}

	}
}
