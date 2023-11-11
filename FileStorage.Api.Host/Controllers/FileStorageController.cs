using FileStorage.Api.Host.Services;
using FileStorage.Contracts;
using FileStorage.Core.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FileStorage.Api.Host.Helpers;
using FileStorage.Core;

namespace FileStorage.Api.Host.Controllers
{
	[ApiController]
	[Route("api/v1/[controller]")]
	public class FileStorageController : ControllerBase
	{
		private readonly IFileStorageLocalService _fileStorageLocalService;
		private readonly IFileStorageService _fileStorageService;
		private readonly IFileStorageFactory<string> _fileStorageFactory;

		public FileStorageController(IFileStorageLocalService fileStorageLocalService,
			IFileStorageService fileStorageService, IFileStorageFactory<string> fileStorageFactory )
		{
			this._fileStorageLocalService = fileStorageLocalService;
			this._fileStorageService = fileStorageService;
			_fileStorageFactory = fileStorageFactory;
		}
		[HttpGet]
		[Route("path")]
		public IActionResult Path(string id)
		{
			var storageName = GetFsName();
			var fullName = this._fileStorageService.GetFullName(id, storageName); 
			return Ok(fullName); 
		}
		[HttpGet]
		[Route("list")]
		public IActionResult List(string id)
		{
			var list = _fileStorageFactory.GetNames();
			return Ok(list);
		}

		[HttpGet]
		[Route("stream")]
		public IActionResult Stream(string id)
		{
			var stream = this._fileStorageLocalService.GetById(id, this.Request);
			if (stream == null)
				return NotFound($"{id}");
			//return Request.Partial(Response, stream.FileStream,stream.ContentType, stream.FileDownloadName);
			stream.EnableRangeProcessing = true;
			return stream;


		}
		[HttpGet]
		[Route("exists")]
		public object Exists(string id)
		{
			var storageName = GetFsName();
			var result = new { Exists = _fileStorageService.Exists(id, storageName) };
			return result;
		}


		[HttpOptions]
		[HttpGet]
		[Route("size")]
		public object Size(string id)
		{
			var storageName = GetFsName();
			var result = new { size = _fileStorageService.GetSize(id, storageName) };
			return result;
		}

       
		[HttpGet]
		[Route("getbytes")]
		public object GetBytes(string id, int? offset, int? count)
		{
			var storageName = GetFsName();

			if (string.IsNullOrEmpty(id))
				return BadRequest();

			var result = new GetBytesResult
			{
				Offset = offset,
				Size = count,
				Bytes = _fileStorageService.GetBytesOffset(id, offset ?? 0, count ?? 10000, storageName)
			};
			return result;
		}

		[HttpPost]
		[Route("savebytes")]
		public IActionResult SaveBytes([FromBody] SaveBytesRequest data)
		{
			var storageName = GetFsName();

			if (string.IsNullOrEmpty(data.Id))
				return BadRequest();

			_fileStorageService.SaveById(new FileBatchRequest
			{
				Id = data.Id,
				Bytes = data.Bytes,
				Close = data.Close,
				StorageName = storageName,
				SessionId = data.SessionId
			});
			return Ok();
		}

		[HttpPost]
		[Route("upload")]
		public IActionResult Upload(IFormFileCollection files)
		{
			var storageName = GetFsName();

			if (files == null)
				return BadRequest();
			var result = new List<object>();
			var memoryStream = new MemoryStream();
			if (files.Count == 0)
			{
				var file = Request.Multipart(ModelState);
				memoryStream = file?.Stream as MemoryStream;
				if (memoryStream != null)
				{
					var name = file.File.Name;
					var id = _fileStorageService.GetIdByExternal(name);
					_fileStorageService.SaveStream(memoryStream, id, storageName);
					result.Add(new
					{
						FileName = name,
						Id = id
					});
				}
			}
			else
			{
				foreach (var file in files)
				{
					var name = WebUtility.HtmlDecode(file.FileName ?? file.Name);
					
					file.CopyTo(memoryStream);
					memoryStream.Position = 0;
					var id = _fileStorageService.GetIdByExternal(name);
					_fileStorageService.SaveStream(memoryStream, id, storageName);
					result.Add(new
					{
						FileName = name,
						Id = id
					});
				}
			}

			

			return Ok();
		}
		[HttpGet]
		[Route("exists/External")]
		public object ExistsExternal(string id)
		{
			var storageName = GetFsName();
			var internalId = _fileStorageService.GetIdByExternal(id);
			var result = new
			{
				Exists = _fileStorageService.Exists(internalId, storageName),
				Id = internalId
			};
			return result;
		}

		[HttpGet]
		[Route("external")]
		public object GetExternalId(string id)
		{
			var storageName = GetFsName();
			var internalId = _fileStorageService.GetIdByExternal(id);
			var result = new { internalId };
			return result;
		}
		[HttpGet]
		[Route("count")]
		public object Count()
		{
			var storageName = GetFsName();
			var result = new
			{
				count = _fileStorageService.GetCount(storageName)
			};
			return result;
		}
		[HttpGet]
		[Route("stream/external")]
		public object StreamByExternalId(string id)
		{
			var storageName = GetFsName();
			var internalId = _fileStorageService.GetIdByExternal(id);
			var stream = this._fileStorageLocalService.GetById(internalId, this.Request);
			if (stream == null)
				NotFound($"{id}");

			return stream;
		}

		[HttpGet]
		[Route("delete")]
		public object Delete(string id)
		{
			var storageName = GetFsName();
			_fileStorageService.Delete(id, storageName);
			return Ok();
		}

		[HttpGet]
		[Route("delete/external")]
		public object DeleteByExternal(string id)
		{
			var storageName = GetFsName();
			var internalId = _fileStorageService.GetIdByExternal(id);
			_fileStorageService.Delete(internalId, storageName);
			return Ok();
		}
		[HttpGet]
		[Route("changeDepth")]
		public object ChangeDepth(int newDepth)
		{
			var storageName = GetFsName();
			_fileStorageService.ChangeDepthDirectory(newDepth, storageName);
			return Ok();
		}
		private string GetFsName()
		{
			var queryParameter = Request.GetQueryParameter("name");
			return queryParameter;
		}


	}
	internal class GetBytesResult
	{
		public int? Offset { get; set; }
		public int? Size { get; set; }
		public byte[] Bytes { get; set; }

	}

}
