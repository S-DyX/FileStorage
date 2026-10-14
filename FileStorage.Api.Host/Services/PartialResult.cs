using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace FileStorage.Api.Host.Services
{
	/// <summary>
	/// Отдаёт файл с поддержкой запросов Range. Раньше файл целиком копировался в MemoryStream,
	/// а затем в массив (две копии в памяти); теперь ответ пишется прямо из потока средствами ASP.NET Core.
	/// </summary>
	public class PartialResult : IActionResult
	{
		private readonly FileStreamResult _streamResult;

		public PartialResult(FileStreamResult streamResult)
		{
			_streamResult = streamResult;
		}

		public Task ExecuteResultAsync(ActionContext context)
		{
			_streamResult.EnableRangeProcessing = true;
			return _streamResult.ExecuteResultAsync(context);
		}
	}
}
