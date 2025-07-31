using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileStorage.Core.Interfaces
{
	/// <summary>
	/// Процесс приложения.
	/// </summary>
	public class DefaultWindowsService : IWindowsService
	{

		/// <summary>
		/// Создает экземпляр класса <see cref="DefaultWindowsService"/>.
		/// </summary>
		/// <param name="hosts"></param>
		public DefaultWindowsService()
		{
		}

		/// <summary>
		/// Выполняется пори старте сервиса.
		/// </summary>
		void IWindowsService.Start()
		{
		}

		/// <summary>
		/// Вызывается при остановке сервиса.
		/// </summary>
		void IWindowsService.Stop()
		{
		}

		/// <summary>
		/// Вызывается при постановке сервиса на паузу.
		/// </summary>
		void IWindowsService.Paused()
		{
		}

		/// <summary>
		/// Вызывается при продолжении работы сервиса после паузы.
		/// </summary>
		void IWindowsService.Continued()
		{
		}
	}
}
