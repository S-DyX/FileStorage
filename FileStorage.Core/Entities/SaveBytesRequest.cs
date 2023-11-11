namespace FileStorage.Core.Entities
{
	public sealed class SaveBytesRequest
	{
		public string Id { get; set; }
		public byte[] Bytes { get; set; }
		public bool Close { get; set; }
		public string SessionId { get; set; }

        public string FolderId { get; set; }
		
	}
}
