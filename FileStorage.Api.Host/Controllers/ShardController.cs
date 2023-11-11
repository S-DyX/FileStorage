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
	public class ShardController : ControllerBase
	{ 

		public ShardController()
		{ 
		}
		[HttpGet]
		[Route("register")]
		public IActionResult Path(string id)
		{
			return Ok(); 
		}


	} 

}
