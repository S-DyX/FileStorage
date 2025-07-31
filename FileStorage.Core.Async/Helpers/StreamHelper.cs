using System.IO;

namespace FileStorage.Core.Helpers
{
	public static class StreamHelper
	{
		public static void CopyStream(Stream instream, Stream outstream)
		{
			instream.CopyToAsync(outstream);
		
		}
	}
}
