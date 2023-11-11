using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FileStorage.Contracts.Impl.Impl;
using FileStorage.Core.Interfaces;
using FileStorage.Core.Interfaces.Settings;
using Newtonsoft.Json;

namespace FileStorage.Contracts.Impl.Connections
{
	public sealed class TcpProcessingService
	{
		private readonly IFileStorageSettings _settings;
		private readonly ILocalLogger _localLogger;
		private readonly TcpSocketHotelClient _tcpSocketCommand;

		private bool _isStart;
		 
		public TcpProcessingService(ILocalLogger logger, IFileStorageSettings settings)
		{ 
			_localLogger = logger;
			_settings = settings;
			_tcpSocketCommand = new TcpSocketHotelClient(_settings, _localLogger);
			_tcpSocketCommand.EventRobotMessage += ReceivedRobotMessage;



		}
		
		

		private void ReceivedRobotMessage(TcpCommonMessage[] messages)
		{
			if (messages == null)
				return;
			var group = messages.GroupBy(x => x.Type).ToList();
			foreach (var g in group)
			{
				var message = g.LastOrDefault();
				if (message == null)
					continue;
				switch (message.Type)
				{
					default:
						EventExecute(message);
						break;
				}
			}

		}

		public void EventExecute(TcpCommonMessage message)
		{
			if (message == null)
				return;
			var messageType = message.Type;
			_localLogger?.Info($"----------------------- Got Message {messageType} ------------------------------.");

			switch (messageType)
			{
				case TcpMessageType.Write:
					break;
			}
		}

	 
		 
		public void Start()
		{
			_isStart = true;
			_localLogger?.Info($"Start connect to socket");
		}


		private readonly object _syncConnection = new object();




		private Stopwatch _stopwatch = new Stopwatch();



		private void Log(string message)
		{
			_localLogger?.Info(message);
		}






		public void Stop()
		{
			_isStart = false;
			Dispose();
		}

		public void Dispose()
		{ 
		}

	}
}
