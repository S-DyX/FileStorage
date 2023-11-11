using FileStorage.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using FileStorage.Core;
using FileStorage.Core.Contracts;

namespace FileStorage.Api.Host.Services
{
	public interface IFolderStorageLocalService
	{
		FileStreamResult GetById(FolderStorageInfo info, HttpRequest request);
		//FileStreamResult FromStream(dynamic parameters, HttpRequest request);
		FileStreamResult GetFromStreamVideo(Stream stream);
	}

	public sealed class FolderStorageLocalService : IFolderStorageLocalService
	{
		private readonly IFolderStorageService _fileStorageService;
		public FolderStorageLocalService(IFolderStorageService fileStorageService)
		{
			_fileStorageService = fileStorageService;
		}

		public FileStreamResult GetById(FolderStorageInfo info, HttpRequest request)
		{
			if (string.IsNullOrEmpty(info.FileId) || string.IsNullOrEmpty(info.FolderId))
				return null;

			var contentTypes = request.Headers["Content-Type"];
			var acceptTypes = request.Headers["Accept"];

			string storageName = request.Query["name"].FirstOrDefault()?.ToString();
			string type = request.Query["type"];
			string fileName = request.Query["fileName"];
			fileName = WebUtility.UrlEncode(fileName ?? string.Empty);

		
			if (!string.IsNullOrEmpty(fileName))
			{
				fileName = fileName
					.Replace("+", " ")
					.Replace("-", "_");

			}

			var isExists = _fileStorageService.Exists(info);
			if (!isExists)
				return null;


			var stream = _fileStorageService.GetStream(info);
			var fileContentType = stream.GetContentType();
			switch (type)
			{
				case "load":
					if (string.IsNullOrEmpty(fileName))
					{
						fileName = info.FileId;
					}

					if (!string.IsNullOrEmpty(fileContentType?.Extension))
					{
						fileName = $"{fileName}.{fileContentType?.Extension}";
					}

					break;
			}
			FileStreamResult fileStreamResult = CheckLoadAtt(type, ref fileName, stream, fileContentType);
			if (fileStreamResult != null)
				return fileStreamResult;

			var listOfContentTypes = contentTypes.ToArray() ?? contentTypes.ToArray();


			if (!listOfContentTypes.Any() && fileContentType.ContentType != null)
			{
				var streamPartial = GetStreamPartial(stream, fileContentType.ContentType);
				streamPartial.FileDownloadName = fileName;
				return streamPartial;
			}

			return GetResponseFromStream(stream, fileName, listOfContentTypes?.FirstOrDefault());
		}

		private FileStreamResult CheckLoadAtt(string type, ref string fileName, Stream stream, FileContentType fileContentType)
		{
			FileStreamResult fileStreamResult = null;
			if (type != null)
			{
				var resultContentType = fileContentType.ContentType;
				switch (type)
				{
					case "pdf":
						resultContentType = "application/pdf";
						break;

					case "image":
						resultContentType = "image/jpeg";
						break;
					case "video":
						resultContentType = "video/mp4";
						break;
					case "load":
						if (string.IsNullOrEmpty(fileContentType.ContentType))
						{
							if (!fileName.EndsWith(".mp4"))
							{
								fileName = $"{fileName}.mp4";
								resultContentType = "video/mp4";

							}
						}
						else
						{
							resultContentType = fileContentType.ContentType;
						}
						break;
					case "csv":
						if (!fileName.EndsWith(".csv"))
						{
							fileName = $"{fileName}.csv";
						}
						resultContentType = "text/csv";
						break;
				}

				if (!string.IsNullOrEmpty(resultContentType))
				{
					fileStreamResult = GetStreamPartial(stream, resultContentType);
					fileStreamResult.FileDownloadName = fileName;

				}
			}

			return fileStreamResult;
		}


		private FileStreamResult GetResponseFromStream(Stream stream, string fileName, string contentTypes)
		{
			var result = GetStreamPartial(stream, contentTypes ?? "application/octet-stream");
			result.FileDownloadName = fileName;
			return result;
		}

		private FileStreamResult GetFromStreamVideo(FolderStorageInfo info)
		{
			return GetStreamPartial(_fileStorageService.GetStream(info), "video/mp4");
		}



		private FileStreamResult GetStreamPartial(Stream stream, string contentType)
		{
			return new FileStreamResult(stream, contentType ?? "application/octet-stream")
			{
				EnableRangeProcessing = true,

			};
		}

		public FileStreamResult GetFromStreamVideo(Stream stream)
		{
			return GetStreamPartial(stream, "video/mp4");
		}
	}
}
