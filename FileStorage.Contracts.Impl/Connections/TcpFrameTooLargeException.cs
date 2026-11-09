using System;

namespace FileStorage.Contracts.Impl.Connections
{
	/// <summary>
	/// Клиент прислал кадр больше допустимого размера (обычно файл целиком одним сообщением)
	/// </summary>
	public sealed class TcpFrameTooLargeException : Exception
	{
		public TcpFrameTooLargeException(int frameSize, int maxFrameSize)
			: base($"TCP frame of {frameSize} bytes exceeds the limit of {maxFrameSize} bytes: send the file in chunks")
		{
			FrameSize = frameSize;
			MaxFrameSize = maxFrameSize;
		}

		public int FrameSize { get; }

		public int MaxFrameSize { get; }
	}
}
