using FileStorage.Core.Interfaces.Settings;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;

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
                   .AddJsonFile("filestorage.json", optional: true, reloadOnChange: false)
                   // Переменные окружения с префиксом FILESTORAGE_ переопределяют filestorage.json,
                   // например FILESTORAGE_Connection__Tcp__Address=0.0.0.0 в контейнере
                   .AddEnvironmentVariables("FILESTORAGE_");
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

            Compression = LoadCompression(configuration.GetSection("Compression"));

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
            Compression = new FileCompressionSettings();
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

        /// <summary>
        /// <see cref="IFileStorageSettings.Compression"/>
        /// </summary>
        public FileCompressionSettings Compression { get; set; }

        private static FileCompressionSettings LoadCompression(IConfigurationSection section)
        {
            // Секция необязательна: на установленных серверах конфигурацию не обновить,
            // поэтому сжатие включено по умолчанию, а секция только переопределяет значения.
            var result = new FileCompressionSettings();
            if (!section.Exists())
                return result;

            if (bool.TryParse(section["Enabled"], out var enabled))
                result.Enabled = enabled;
            if (bool.TryParse(section["CompressUnknown"], out var unknown))
                result.CompressUnknown = unknown;
            if (long.TryParse(section["MinSize"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var minSize))
                result.MinSize = minSize;
            if (long.TryParse(section["MaxSize"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var maxSize))
                result.MaxSize = maxSize;
            if (double.TryParse(section["MaxRatio"], NumberStyles.Float, CultureInfo.InvariantCulture, out var ratio))
                result.MaxRatio = ratio;
            if (Enum.TryParse<CompressionLevel>(section["Level"], true, out var level))
                result.Level = level;

            var extensions = section.GetSection("Extensions").GetChildren()
                .Select(x => x.Value?.Trim().TrimStart('.'))
                .Where(x => !string.IsNullOrEmpty(x))
                .ToList();
            if (extensions.Count > 0)
                result.Extensions = new HashSet<string>(extensions, StringComparer.OrdinalIgnoreCase);

            return result;
        }
    }
}
