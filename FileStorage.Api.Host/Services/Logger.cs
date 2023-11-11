using FileStorage.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FileStorage.Api.Host.Services
{
	public class LocalLogger : ILocalLogger
	{
		private readonly ILogger logger;

		public LocalLogger(ILoggerFactory logger)
		{
			this.logger = logger.CreateLogger("common");
		}
		public bool IsErrorEnabled => true;

		public bool IsDebug => true;

		public bool IsFatalEnabled => true;

		public bool IsInfoEnabled => true;

		public bool IsWarnEnabled => true;

		public bool IsDebugEnabled => throw new NotImplementedException();

		public void Debug(string message)
		{
			logger.LogDebug(message);
		}

		public void Debug(object message)
		{
			throw new NotImplementedException();
		}

		public void Error(string message)
		{
			logger.LogError(message);
		}

		public void Error(Exception ex)
		{
			logger.LogError(ex, ex.ToString());
		}

		public void Error(string message, Exception ex)
		{
			logger.LogError(ex, message);
		}

		public void ErrorFormat(string format, params object[] args)
		{
			logger.LogError(format, args);
		}

		public void Info(string message)
		{
			logger.LogInformation(message);
		}

		public void Info(object message)
		{
			logger.LogInformation(message?.ToString());
		}

		public void Warn(string message)
		{
			logger.LogWarning(message);
		}
	}
}
