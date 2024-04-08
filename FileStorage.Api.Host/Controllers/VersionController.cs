using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Reflection;

namespace FileStorage.Api.Host.Controllers
{
	[Route("api/v1/[controller]")]
	public class VersionController : ControllerBase
	{

		public VersionController()
		{
		}

		[AllowAnonymous]
		[HttpGet]
		public IActionResult Get()
		{
			var assembly = Assembly.GetExecutingAssembly();
			var assemblyName = assembly.GetName();
			var version = assemblyName.Version;
			return Ok(new
			{
				Version = version,
				Name = assemblyName.Name,
			});
		}


	}
}
