using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace FileStorage.Core.Helpers
{
	public static class NetworkHelpers
	{
		public static string GetMacAddress()
		{
			var macAddress = string.Empty;
			foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
			{
				OperationalStatus ot = nic.OperationalStatus;
				if (nic.OperationalStatus == OperationalStatus.Up)
				{
					macAddress = nic.GetPhysicalAddress().ToString();
					break;
				}
			}
			return macAddress;
		}

		public static string GetMachineName()
		{
			return System.Environment.MachineName;
		
		}
		public static string GetUserName()
		{
			return System.Environment.UserName ?? System.Environment.UserDomainName;
			//var current = System.Security.Principal.WindowsIdentity.GetCurrent();
			//if (current != null)
			//	return current.Name;
			//return string.Empty;
		}

		public static string GetIpAddress()
		{
			string localIP = "?";
			var host = Dns.GetHostEntry(Dns.GetHostName());
			foreach (IPAddress ip in host.AddressList)
			{
				if (ip.AddressFamily == AddressFamily.InterNetwork)
				{
					localIP = ip.ToString();
				}
			}
			return localIP;
		}
	}
}
