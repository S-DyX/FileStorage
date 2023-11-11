using System.Configuration;
using System.IO;
using FileStorage.Core.Interfaces.Settings;

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
			: this(Directory.GetCurrentDirectory(), 6, 3)
		{
		}

		/// <summary>
		/// Ctor
		/// </summary>
		/// <param name="rootDirectory">folder for FS</param>
		/// <param name="elementsCount">How may symbols on folder name</param>
		/// <param name="depth">how many folders will be generated</param>
		public FileStorageSettings(string rootDirectory, int elementsCount, int depth)
		{
			RootDirectory = rootDirectory;
			if (!RootDirectory.EndsWith(Path.DirectorySeparatorChar.ToString()))
				RootDirectory += Path.DirectorySeparatorChar.ToString();

			ElementsCount = elementsCount;
			Depth = depth;
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

	}
}
