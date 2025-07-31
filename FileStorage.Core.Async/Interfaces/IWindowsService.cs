using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileStorage.Core.Interfaces
{
	/// <summary>
	/// Интерфейс сервиса ОС, выполняющего работу.
	/// </summary>
	public interface IWindowsService
	{
		/// <summary>
		/// Вызывается при старте сервиса.
		/// </summary>
		void Start();

		/// <summary>
		/// Вызывается при остановке сервиса.
		/// </summary>
		void Stop();

		/// <summary>
		/// Вызывается при постановке сервиса на паузу.
		/// </summary>
		void Paused();

		/// <summary>
		/// Вызывается при продолжении работы сервиса после паузы.
		/// </summary>
		void Continued();
	}

}
