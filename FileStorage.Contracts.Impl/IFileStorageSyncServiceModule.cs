using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using FileStorage.Core.Entities;
using FileStorage.Core.Interfaces;
using Newtonsoft.Json;

namespace FileStorage.Contracts.Impl
{
	public interface IFileStorageSyncServiceModule
	{
		void DownloadFiles();

		void DownloadAllFiles();
		void UploadFiles();
		void CheckDiff();
	}



	
}
