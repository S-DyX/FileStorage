using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileStorage.Contracts.Rest.Impl.FileStorage
{
	public sealed class MixedMemoryStream : Stream
	{
		private readonly bool _deleteFile;
		private readonly string _fileName;
		private Stream _stream;
		private bool isFileStream;
		private readonly int _maxMemorySize = int.MaxValue >> 3;
		public MixedMemoryStream(bool deleteFile = false)
		{
			_deleteFile = deleteFile;
			var directory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Temp");
			if (!Directory.Exists(directory))
				Directory.CreateDirectory(directory);

			_fileName = Path.Combine(directory, Guid.NewGuid().ToString());
			_stream = new MemoryStream();


		}

		public override void Flush()
		{
			_stream.Flush();
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			return _stream.Read(buffer, offset, count);
		}

		public override long Seek(long offset, SeekOrigin origin)
		{
			return _stream.Seek(offset, origin);
		}

		public override void SetLength(long value)
		{
			_stream.SetLength(value);
		}

		public override void Write(byte[] buffer, int offset, int count)
		{
			if (!isFileStream && _stream.Length + count > _maxMemorySize)
			{
				var fileStream = new FileStream(_fileName, FileMode.CreateNew, FileAccess.ReadWrite);
				_stream.Position = 0;
				_stream.CopyTo(fileStream);
				using (_stream)
				{

				}
				_stream = fileStream;
				isFileStream = true;

			}
			_stream.Write(buffer, offset, count);
		}

		public override bool CanRead => _stream.CanRead;
		public override bool CanSeek => _stream.CanSeek;
		public override bool CanWrite => _stream.CanWrite;
		public override long Length => _stream.Length;

		public override long Position
		{
			get => _stream.Position;
			set => _stream.Position = value;
		}

		protected override void Dispose(bool disposing)
		{
			_stream.Close();
			_stream.Dispose();
			if (_deleteFile && disposing && isFileStream)
			{
				if (File.Exists(_fileName))
				{
					File.Delete(_fileName);
				}
			}
		}
	}
}
