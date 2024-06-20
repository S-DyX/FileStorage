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
using FileStorage.Core.Contracts;
using FileStorage.Core.Helpers;

namespace FileStorage.Api.Host.Controllers
{
	[ApiController]
	[Route("api/v1/[controller]")]
	public class FolderStorageController : ControllerBase
	{
		private readonly IFolderStorageLocalService _fileStorageLocalService;
		private readonly IFolderStorageService _fileStorageService;
		private readonly IFolderStorageFactory _fileStorageFactory;

		public FolderStorageController(IFolderStorageLocalService fileStorageLocalService,
            IFolderStorageService fileStorageService, IFolderStorageFactory fileStorageFactory)
		{
			this._fileStorageLocalService = fileStorageLocalService;
			this._fileStorageService = fileStorageService;
			_fileStorageFactory = fileStorageFactory;
		}
		[HttpGet]
		[Route("path")]
		public IActionResult Path(string folderId, string fileId)
		{
			var storageName = GetFsName();
            var folderStorageInfo = GetFolderStorageInfo(folderId, fileId);
            var fullName = this._fileStorageService.GetFullName(folderStorageInfo); 
			return Ok(fullName); 
		}

        private FolderStorageInfo GetFolderStorageInfo(string folderId, string fileId)
        {
            var storageName = GetFsName();
			var folderStorageInfo = new FolderStorageInfo()
            {
                FileId = fileId,
                FolderId = folderId,
                StorageName = storageName
            };
            return folderStorageInfo;
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
		public IActionResult Stream(string folderId, string fileId)
        {
            var info = GetFolderStorageInfo(folderId, fileId);
			var stream = this._fileStorageLocalService.GetById(info, this.Request);
			if (stream == null)
				return NotFound($"{info.FileId}");
			//return Request.Partial(Response, stream.FileStream,stream.ContentType, stream.FileDownloadName);
			stream.EnableRangeProcessing = true;
			return stream;


		}
		[HttpGet]
		[Route("exists")]
		public object Exists(string folderId, string fileId)
		{
            var storageName = GetFsName();
			var info = GetFolderStorageInfo(folderId, fileId);
			
			var result = new { Exists = _fileStorageService.Exists(info) };
			return result;
		}

		[HttpGet]
		[Route("clearTtl")]
		public IActionResult ClearTtl(int daysAgo)
		{
			var storageName = GetFsName();
			_fileStorageService.ClearTtl(DateTime.UtcNow.AddDays(-daysAgo), storageName); 
			return Ok();
		}
		[HttpOptions]
		[HttpGet]
		[Route("size")]
		public object Size(string folderId, string fileId)
		{
			var storageName = GetFsName();
            var info = GetFolderStorageInfo(folderId, fileId);
			var result = new { size = _fileStorageService.GetSize(info) };
			return result;
		}
		[HttpGet]
		[Route("move")]
		public object Move(string folderId, string toFolderId, string fileId)
		{
			if (string.IsNullOrEmpty(folderId) || string.IsNullOrEmpty(fileId) || string.IsNullOrEmpty(toFolderId))
				return BadRequest();

			var storageName = GetFsName();
			_fileStorageService.Move(toFolderId,folderId, fileId, storageName);
			return true;
		}

		[HttpGet]
		[Route("getbytes")]
		public object GetBytes(string folderId, string fileId, long? offset, int? count)
		{ 
            var info = GetFolderStorageInfo(folderId, fileId);
			if (string.IsNullOrEmpty(folderId) || string.IsNullOrEmpty(fileId))
				return BadRequest();

			var result = new GetBytesResult
			{
				Offset = offset,
				Size = count,
				Bytes = _fileStorageService.GetBytesOffset(info, offset ?? 0, count ?? 10000)
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
				SessionId = data.SessionId,
                FolderId = data.FolderId,
			});
			return Ok();
		} 

		[HttpGet]
		[Route("exists/External")]
		public object ExistsExternal(string folderId, string fileId)
		{
			var info = GetInfoByExternalId(folderId, fileId);
            var result = new
			{
				Exists = _fileStorageService.Exists(info),
				Id = info.FileId,
				FolderId= info.FolderId
			};
			return result;
		}

        private FolderStorageInfo GetInfoByExternalId(string folderId, string fileId)
        {
            var storageName = GetFsName();
            var fId = _fileStorageService.GetIdByExternal(folderId);
            var fiId = _fileStorageService.GetIdByExternal(fileId);
            var info = GetFolderStorageInfo(fId, fiId);
            return info;
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
		public object StreamByExternalId(string folderId, string fileId)
		{
            var info = GetInfoByExternalId(folderId, fileId);
			var stream = this._fileStorageLocalService.GetById(info, this.Request);
			if (stream == null)
				NotFound($"{info.FileId}");

			return stream;
		}

		[HttpGet]
		[Route("delete")]
		public object Delete(string folderId, string fileId)
		{
            var info = GetFolderStorageInfo(folderId, fileId);
			var storageName = GetFsName();
			_fileStorageService.Delete(info);
			return Ok();
		}

		[HttpGet]
		[Route("delete/external")]
		public object DeleteByExternal(string folderId, string fileId)
		{
            var info = GetInfoByExternalId(folderId, fileId); 
			_fileStorageService.Delete(info);
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

}
