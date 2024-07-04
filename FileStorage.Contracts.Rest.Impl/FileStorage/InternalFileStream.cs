using System;
using System.IO;

namespace FileStorage.Contracts.Rest.Impl.FileStorage
{
	internal sealed class InternalFileStream : Stream
	{
		public InternalFileStream()
		{
		}

		public InternalFileStream(Func<string, long, long, string, byte[]> func, long length,  string fileId, string storageName)
		{
			_func = func;
			_length = length; 
			_fileId = fileId;
			_storageName = storageName;
		}

		private readonly Func<string, long, long, string, byte[]> _func;
		private long _length;
		private long _position; 
		private readonly string _fileId;
		private readonly string _storageName;

		public override void Flush()
		{
			throw new NotImplementedException();
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			if (offset + count > _length)
			{
				count = (int)(_length - offset);
			}
			var tmp = _func.Invoke(_fileId, _position, count, _storageName);
			_position += count;
			tmp.CopyTo(buffer, 0);
			return tmp.Length;
		}

		public override long Seek(long offset, SeekOrigin origin)
		{
			if (origin == SeekOrigin.Begin)
			{
				_position = offset;
			}
			else if (origin == SeekOrigin.End)
			{
				_position = _length - offset;
			}
			else if (origin == SeekOrigin.Current)
			{
				_position += offset;
			}

			return _position;
		}

		public override void SetLength(long value)
		{
			throw new NotImplementedException();
		}

		public override void Write(byte[] buffer, int offset, int count)
		{
			throw new NotImplementedException();
		}

		public override bool CanRead => true;
		public override bool CanSeek => true;
		public override bool CanWrite { get; }
		public override long Length { get => _length; }
		public override long Position
		{
			get
			{
				return _position;
			}
			set
			{
				_position = value;
			}
		}
	}
}
