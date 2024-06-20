namespace FileStorage.Contracts.Rest.Impl.FileStorage
{
	internal class GetBytesResult
	{
		public long Offset { get; set; }
		public int Size { get; set; }
		public byte[] Bytes { get; set; }

	}
}
