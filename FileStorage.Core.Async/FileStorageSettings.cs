using FileStorage.Core.Interfaces.Settings;
using Microsoft.Extensions.Configuration;
using System.IO;

namespace FileStorage.Core
{
    /// <summary>
    /// Impl <see cref="IFileStorageSettings"/>
    /// </summary>
    public class FileStorageSettings : IFileStorageSettings
    {

        /// <summary>
        /// Ctor
        /// </summary>
        public FileStorageSettings()
            : this(null, 6, 3)
        {
            var builder = new ConfigurationBuilder()
                   .SetBasePath(Directory.GetCurrentDirectory())
                   .AddJsonFile("filestorage.json", optional: true, reloadOnChange: false);
            var configuration = builder.Build();

            RootDirectory = configuration.GetSection("RootDirectory")?.Value ?? RootDirectory;
            var rawElementsCount = configuration.GetSection("ElementsCount")?.Value;
            var rawDepthCount = configuration.GetSection("TreeDepth")?.Value;
            var rawNumberOfShards = configuration.GetSection("NumberOfShards")?.Value;
            NumberOfShards = 1;

            if (!string.IsNullOrEmpty(rawElementsCount))
            {
                ElementsCount = int.Parse(rawElementsCount);
            }
            if (!string.IsNullOrEmpty(rawDepthCount))
            {
                Depth = int.Parse(rawDepthCount);
            }
            if (!string.IsNullOrEmpty(rawNumberOfShards))
            {
                NumberOfShards = int.Parse(rawNumberOfShards);
            }
            var connection = configuration.GetSection("Connection")?.GetSection("Tcp");
            if (connection != null)
            {
                var host = connection.GetSection("Address")?.Value ?? "127.0.0.1";
                var port = int.Parse(connection.GetSection("Port")?.Value ?? "11581");
                Connection = new ServerConnectionConfig()
                {
                    Tcp = new TcpSettings() { Address = host, Port = port }
                };
            }

        }

        /// <summary>
        /// Ctor
        /// </summary>
        /// <param name="rootDirectory">FS root directory</param>
        /// <param name="elementsCount">how many hash elements are involved in creating a directory</param>
        /// <param name="depth">how many nested directories will be created should be less than <see cref="ElementsCount"/></param>
        public FileStorageSettings(string rootDirectory, int elementsCount = 6, int depth = 3)
        {
            if (string.IsNullOrEmpty(rootDirectory))
                rootDirectory = Directory.GetCurrentDirectory();
            RootDirectory = rootDirectory;
            if (!RootDirectory.EndsWith(Path.DirectorySeparatorChar.ToString()))
                RootDirectory += Path.DirectorySeparatorChar.ToString();

            ElementsCount = elementsCount;
            Depth = depth;
            Connection = new ServerConnectionConfig()
            {
                Tcp = new TcpSettings() { Address = "127.0.0.1", Port = 11581 }
            };
        }

        /// <summary>
        /// <see cref="IFileStorageSettings.RootDirectory"/>
        /// </summary>
        public string RootDirectory { get; set; }
        /// <summary>
        /// <see cref="IFileStorageSettings.ElementsCount"/>
        /// </summary>
        public int ElementsCount { get; set; }
        /// <summary>
        /// <see cref="IFileStorageSettings.Depth"/>
        /// </summary>
        public int Depth { get; set; }

        public int NumberOfShards { get; set; }

        public ServerConnectionConfig Connection { get; set; }
    }
}
