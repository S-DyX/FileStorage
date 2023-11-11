using System;
using System.IO;
using System.Net;
using System.Net.Http.Headers; 
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.WebUtilities;


namespace FileStorage.Api.Host.Helpers
{
	public static class MultipartRequestHelper
	{
		// Content-Type: multipart/form-data; boundary="----WebKitFormBoundarymx2fSWqWSd0OxQqq"
		// The spec at https://tools.ietf.org/html/rfc2046#section-5.1 states that 70 characters is a reasonable limit.
		public static string GetBoundary(Microsoft.Net.Http.Headers.MediaTypeHeaderValue contentType, int lengthLimit)
		{
			var boundary = Microsoft.Net.Http.Headers.HeaderUtilities.RemoveQuotes(contentType.Boundary).Value;

			if (string.IsNullOrWhiteSpace(boundary))
			{
				throw new InvalidDataException("Missing content-type boundary.");
			}

			if (boundary.Length > lengthLimit)
			{
				throw new InvalidDataException(
					$"Multipart boundary length limit {lengthLimit} exceeded.");
			}

			return boundary;
		}

		public static bool IsMultipartContentType(string contentType)
		{
			return !string.IsNullOrEmpty(contentType)
				   && contentType.IndexOf("multipart/", StringComparison.OrdinalIgnoreCase) >= 0;
		}

		public static bool HasFormDataContentDisposition(ContentDispositionHeaderValue contentDisposition)
		{
			// Content-Disposition: form-data; name="key";
			return contentDisposition != null
				&& contentDisposition.DispositionType.Equals("form-data")
				&& string.IsNullOrEmpty(contentDisposition.FileName)
				&& string.IsNullOrEmpty(contentDisposition.FileNameStar);
		}


		public static bool HasFileContentDisposition(ContentDispositionHeaderValue contentDisposition)
		{
			// Content-Disposition: form-data; name="myfile1"; filename="Misc 002.jpg"
			return contentDisposition != null
				&& contentDisposition.DispositionType.Equals("form-data")
				&& (!string.IsNullOrEmpty(contentDisposition.FileName)
					|| !string.IsNullOrEmpty(contentDisposition.FileNameStar));
		}


		public static FileResult Multipart(this HttpRequest request, ModelStateDictionary modelState)
		{
			if (MultipartRequestHelper.IsMultipartContentType(request.ContentType))
			{
				// Used to accumulate all the form url encoded key value pairs in the 
				// request.
				var formAccumulator = new KeyValueAccumulator();

				var boundary = request.GetMultipartBoundary();
				var reader = new MultipartReader(boundary, request.Body);
				
				var section = reader.ReadNextSectionAsync().Result;
				request.Body.Position = 0;
				while (section != null)
				{
					var hasContentDispositionHeader =
						ContentDispositionHeaderValue.TryParse(
							section.ContentDisposition, out var contentDisposition);

					if (hasContentDispositionHeader)
					{
						// This check assumes that there's a file
						// present without form data. If form data
						// is present, this method immediately fails
						// and returns the model error.
						if (!MultipartRequestHelper
							.HasFileContentDisposition(contentDisposition))
						{
							modelState.AddModelError("File",
								$"The request couldn't be processed (Error 2).");

							{
								throw new InvalidOperationException("The request couldn't be processed (Error 2).");
								//return BadRequest(modelState);
							}
						}
						else
						{
							if (!modelState.IsValid)
							{
								throw new InvalidOperationException("invalid modelState ");
							}

							var body = section.Body;
							var fileName = WebUtility.HtmlEncode(
								contentDisposition.FileName);
							var trustedFileNameForFileStorage = Path.GetRandomFileName();
							var memoryStream = new MemoryStream();
							section.Body.CopyTo(memoryStream);
							memoryStream.Position = 0;
							var file = new FileResult()
							{
								Stream = memoryStream,
								File = new FileDataModel()
								{
									Name = WebUtility.HtmlDecode(fileName).Replace("\"", ""),
									MimeType = section.ContentType,
									Size = memoryStream.Length

								}
							};
							return file;
							//using (var targetStream = System.IO.File.Create(
							//	Path.Combine(_targetFilePath, trustedFileNameForFileStorage)))
							//{
							//	await targetStream.WriteAsync(streamedFileContent);

							//	_logger.LogInformation(
							//		"Uploaded file '{TrustedFileNameForDisplay}' saved to " +
							//		"'{TargetFilePath}' as {TrustedFileNameForFileStorage}",
							//		trustedFileNameForDisplay, _targetFilePath,
							//		trustedFileNameForFileStorage);
							//}
						}
					}

					// Drain any remaining section body that hasn't been consumed and
					// read the headers for the next section.
					section = reader.ReadNextSectionAsync().Result;
				}
			}

			return null;
		}
	}
}
