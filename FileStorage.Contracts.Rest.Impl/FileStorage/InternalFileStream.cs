using System;
using System.IO;
using System.Threading.Tasks;

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
		private readonly long _length;
		private long _position; 
		private readonly string _fileId;
		private readonly string _storageName;

		public override void Flush()
		{
			// поток только для чтения — сбрасывать нечего (раньше Flush бросал исключение,
			// и падали обёртки, которые вызывают его при закрытии)
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			// Раньше конец файла проверялся по смещению в buffer, а не по текущей позиции; данные копировались
			// в начало buffer, а не начиная с offset; позиция сдвигалась на запрошенное, а не на полученное
			// число байт; ответ длиннее count приводил к исключению.
			if (_position >= _length || count <= 0)
				return 0;
			if (_position + count > _length)
				count = (int)(_length - _position);
			var tmp = _func.Invoke(_fileId, _position, count, _storageName);
			if (tmp == null || tmp.Length == 0)
				return 0;
			var read = Math.Min(tmp.Length, count);
			Buffer.BlockCopy(tmp, 0, buffer, offset, read);
			_position += read;
			return read;
		}

		public override long Seek(long offset, SeekOrigin origin)
		{
			if (origin == SeekOrigin.Begin)
			{
				_position = offset;
			}
			else if (origin == SeekOrigin.End)
			{
				// смещение от конца обычно отрицательное; раньше считалось как _length - offset
				_position = _length + offset;
			}
			else if (origin == SeekOrigin.Current)
			{
				_position += offset;
			}

			return _position;
		}

		public override void SetLength(long value)
		{
			throw new NotSupportedException();
		}

		public override void Write(byte[] buffer, int offset, int count)
		{
			throw new NotSupportedException();
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

		public override ValueTask DisposeAsync()
		{

			return base.DisposeAsync();
		}
	}
}
