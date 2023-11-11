using System;

namespace FileStorage.Core.Interfaces
{
	public interface ILocalLogger
	{
		bool IsDebugEnabled { get; }

		bool IsInfoEnabled { get; }

		bool IsWarnEnabled { get; }

		void ErrorFormat(string format, params object[] args);

		void Info(object message);

		void Debug(object message);
		
		void Error(string message, Exception ex);
	}
}
