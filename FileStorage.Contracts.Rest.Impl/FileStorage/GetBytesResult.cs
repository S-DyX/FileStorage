namespace FileStorage.Contracts.Rest.Impl.FileStorage
{
	internal class GetBytesResult
	{
		// сервер возвращает эти поля как nullable; null в long/int без '?' ломал разбор ответа
		public long? Offset { get; set; }
		public int? Size { get; set; }
		public byte[] Bytes { get; set; }

	}
}
