namespace FileStorage.Core.Interfaces.Settings
{
	/// <summary>
	/// FS settings
	/// </summary>
	public interface IFileStorageSettings
	{
		/// <summary>
		/// FS root directory
		/// </summary>
		string RootDirectory { get; set; }
		/// <summary>
		/// how many hash elements are involved in creating a directory
		/// </summary>
		int ElementsCount { get; set; }

		/// <summary>
		/// how many nested directories will be created should be less than <see cref="ElementsCount"/>
		/// </summary>
		int Depth { get; set; }
	}
}