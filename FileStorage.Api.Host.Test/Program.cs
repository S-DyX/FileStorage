using FileStorage.Contracts.Rest.Impl.FileStorage;
using Service.Registry.Common;
using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using FileStorage.Contracts.Rest.Impl;

namespace FileStorage.Api.Host.Test
{
    internal class Program
    {
        static void Main(string[] args)
        {
            DemoUpload();
            ExampleTcpFile();

            ProcessTcp();
            FileProcessTcp();
            ProcessHttp();

            Console.WriteLine("Hello World!");
            Console.ReadLine();
        }

        static void DemoUpload()
        {
            try
            {
                var fileInfo = new FileInfo(@"C:\Work\test_1.pdf");

                var payloadData = new
                {
                    Authorization = "fd54bbef-47a7-427e-ac18-227d3310ecd8"
                };

                var content = new MultipartFormDataContent();

                //Add data model to multiForm Content   
                content.Add(new StringContent(payloadData.Authorization), "Authorization"); 

                //Add file StreamContent to multiForm Content 
                var fileContent = new StreamContent(new FileStream(fileInfo.FullName, FileMode.Open));
                fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf"); 
               
                fileContent.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data")

                {
                    Name = "File",
                    FileName = fileInfo.Name,
                    FileNameStar = fileInfo.Name
                };
                content.Add(fileContent);

                var client = new HttpClient();
                client.BaseAddress = new Uri("http://46.148.238.130:38033");
                var result = client.PostAsync("/api/v1/Documents/Upload", content).Result; 
                //var fileResult = result.Content.ReadAsAsync<UploadFileResult>().Result;

                Console.WriteLine("***************");
                Console.WriteLine("Status: " + result.StatusCode); 
                Console.WriteLine("***************");
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }
        /// <summary>
        /// Example use Service registry http call
        /// </summary>
        private static void ExampleRestFile()
{
    var serviceRegistryFactory = new ServiceRegistryFactory();
    var service =
        serviceRegistryFactory.CreateTcp<IFileStorageRestService, FileStorageRestService>();
    var storageName = "test2";
    var fileName = $"1.txt";
    var sessionId = Guid.NewGuid().ToString();
    var fileId = service.GetIdByExternal(fileName);
    if (service.Exists(fileId, storageName))
    {
        service.Delete(fileId, storageName);
    }

    using (var stream = new FileStream(fileName, FileMode.Open))
    {
        service.SaveBytes(fileId, storageName, stream, sessionId);
    }
}

/// <summary>
/// Example use Service registry tcp socket call
/// </summary>
private static void ExampleTcpFile()
{
    var serviceRegistryFactory = new ServiceRegistryFactory();
    var service =
        serviceRegistryFactory.CreateTcp<ITcpFileStorageService, TcpFileStorageService>("ITcpFileStorageService");
    var storageName = "test2";
    var fileName = $"1.txt";
    var sessionId = Guid.NewGuid().ToString();
    if (service.IsExists(fileName, storageName))
    {
        service.Delete(fileName, storageName);
    }

    using (var stream = new FileStream(fileName, FileMode.Open))
    {
        service.Write(fileName, storageName, stream, sessionId);
    }
}
/// <summary>
/// Example use Service registry tcp socket call. Store files into folder
/// </summary>
private static void ExampleTcpFolder()
{
    var serviceRegistryFactory = new ServiceRegistryFactory();
    var service =
        serviceRegistryFactory.CreateTcp<ITcpFolderStorageService, TcpFolderStorageService>();
    var storageName = "test2";
    var fileName = $"1.txt";
    var folder = "folderName";
    var sessionId = Guid.NewGuid().ToString();
    if (service.IsExists(folder,fileName, storageName))
    {
        service.Delete(folder, fileName, storageName);
    }

    using (var stream = new FileStream(fileName, FileMode.Open))
    {
        service.Write(folder, fileName, storageName, stream, sessionId);
    }
}

        private static void ProcessTcp()
        {
            var serviceRegistryFactory = new ServiceRegistryFactory();
            var service = serviceRegistryFactory.CreateTcp<IFolderStorageRestService, TcpFolderStorageService>("TcpFolderStorageService");
            for (int i = 0; i < 10; i++)
            {
                try
                {
                    var capacity = 300000;
                    var sb = new StringBuilder(capacity);
                    for (int j = 0; j < capacity; j++)
                    {
                        sb.Append($"{i}");
                    }

                    var bytes = Encoding.UTF8.GetBytes(sb.ToString());
                    var externalFileId = $"{i}.txt";
                    var storageName = "test";
                    var externalFolderId = "3";
                    if (service.IsExists(externalFolderId, externalFileId, storageName))
                    {
                        service.Delete(externalFolderId, externalFileId, storageName);
                    }
                    //Thread.Sleep(20000);
                    service.Delete(externalFolderId, externalFileId, storageName);


                    using (var stream = new MemoryStream())
                    {
                        stream.Write(bytes);
                        stream.Position = 0;
                        service.Write(externalFolderId, externalFileId, storageName, stream, Guid.NewGuid().ToString());
                    }

                    var load = service.GetStream(externalFolderId, externalFileId, storageName);

                    if (load?.Length != capacity)
                    {
                        ;
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
        }

        private static void FileProcessTcp()
        {
            var serviceRegistryFactory = new ServiceRegistryFactory();
            var service = serviceRegistryFactory.CreateTcp<ITcpFileStorageService, TcpFileStorageService>("ITcpFileStorageService");

            for (int i = 0; i < 10; i++)
            {
                try
                {
                    var capacity = 2300000;
                    var sb = new StringBuilder(capacity);
                    for (int j = 0; j < capacity; j++)
                    {
                        sb.Append($"{i}");
                    }

                    var bytes = Encoding.UTF8.GetBytes(sb.ToString());
                    var externalFileId = $"{i}.txt";
                    var storageName = "test2";
                    if (service.IsExists( externalFileId, storageName))
                    {
                        service.Delete(externalFileId, storageName);
                    }
                    //Thread.Sleep(20000);
                    service.Delete(externalFileId, storageName);


                    using (var stream = new MemoryStream())
                    {
                        stream.Write(bytes);
                        stream.Position = 0;
                        service.Write(externalFileId, storageName, stream, Guid.NewGuid().ToString());
                    }

                    var load = service.GetStream(externalFileId, storageName);

                    if (load?.Length != capacity)
                    {
                        ;
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
        }
        private static void ProcessHttp()
        {
            var serviceRegistryFactory = new ServiceRegistryFactory();
            var service = serviceRegistryFactory.CreateRest<IFolderStorageRestService, FolderStorageRestService>();
            for (int i = 0; i < 10; i++)
            {
                try
                {
                    var capacity = 300000;
                    var sb = new StringBuilder(capacity);
                    for (int j = 0; j < capacity; j++)
                    {
                        sb.Append($"{i}");
                    }

                    var bytes = Encoding.UTF8.GetBytes(sb.ToString());
                    var externalFileId = $"{i}.txt";
                    var storageName = "test";
                    var externalFolderId = "1";
                    if (service.IsExists(externalFolderId, externalFileId, storageName))
                    {
                        service.Delete(externalFolderId, externalFileId, storageName);
                    }

                    service.Delete(externalFolderId, externalFileId, storageName);


                    using (var stream = new MemoryStream())
                    {
                        stream.Write(bytes);
                        stream.Position = 0;
                        service.Write(externalFolderId, externalFileId, storageName, stream, Guid.NewGuid().ToString());
                    }

                    var load = service.GetStream(externalFolderId, externalFileId, storageName);

                    if (load.Length != capacity)
                    {
                        ;
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
        }
    }
}
