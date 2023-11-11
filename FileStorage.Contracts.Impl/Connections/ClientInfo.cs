using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;

namespace FileStorage.Contracts.Impl.Connections
{
	public enum UdpRequestTypes
	{
		Unknown = 0,
		Ping = 1,
		RobotRegister = 2,
		UserRegister = 3,
		Act = 4
	}

	public sealed class UdpRequest
	{
		public UdpRequestTypes Type { get; set; }
		public ClientInfo Robot { get; set; }

		public ClientInfo User { get; set; }
	}

	public enum ConnectionTypes { Unknown, LAN, WAN }

	[Serializable]
	public class ClientInfo 
	{


		public bool IsConnected { get; set; }
		public string Name { get; set; }
		public long Id { get; set; }
		public PtpAddress ExternalEndpoint { get; set; }
		public PtpAddress InternalEndpoint { get; set; }

		public int ErrorCount { get; set; }

		public ConnectionTypes ConnectionType { get; set; }

		public List<string> InternalAddresses = new List<string>();

		[NonSerialized] //server use only
		public TcpClient Client;

		[NonSerialized] //server use only
		public bool Initialized;

		public bool Update(ClientInfo clientInfo)
		{
			if (Id == clientInfo.Id)
			{
				foreach (PropertyInfo P in clientInfo.GetType().GetProperties())
					if (P.GetValue(clientInfo) != null)
						P.SetValue(this, P.GetValue(clientInfo));

				if (clientInfo.InternalAddresses.Count > 0)
				{
					InternalAddresses.Clear();
					InternalAddresses.AddRange(clientInfo.InternalAddresses);
				}
			}

			return (Id == clientInfo.Id);
		}

		public override string ToString()
		{
			var name = $"{Id},{Name}";
			if (ExternalEndpoint != null)
				return name + " (" + ExternalEndpoint.Address + ")";
			else
				return name + " (UDP Endpoint Unknown)";
		}

		public ClientInfo Simplified()
		{
			return new ClientInfo()
			{
				Name = this.Name,
				Id = this.Id,
				InternalEndpoint = this.InternalEndpoint,
				ExternalEndpoint = this.ExternalEndpoint
			};
		}
	}
	public sealed class PtpAddress
	{
		public string Address { get; set; }
		public int Port { get; set; }

		public PtpAddress()
		{
		}

		public PtpAddress(string address, int port)
		{
			Address = address;
			Port = port;
		}
		public PtpAddress(IPEndPoint endPoint)
		{
			Address = endPoint.Address.ToString();
			Port = endPoint.Port;
		}
		public IPEndPoint ToEndPoint()
		{
			if (string.IsNullOrEmpty(Address))
				return null;
			return new IPEndPoint(IPAddress.Parse(Address), Port);
		}
		public override string ToString()
		{
			return $"{Address ?? String.Empty}:{Port}";
		}
	}
}
