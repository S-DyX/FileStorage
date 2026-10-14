using System;
using System.Collections.Generic;
using System.Text;

namespace FileStorage.Contracts.Impl.Connections
{
	/// <summary>
	/// Собирает кадры протокола ('@' + длина Int32 + UTF-8 данные + '#') из потока байт.
	/// Неполный кадр остаётся в буфере до следующего чтения. Раньше кадр считался полным, когда данных
	/// было больше длины, хотя он занимает длину + 6 байт: буфер очищался, и неполная команда терялась.
	/// </summary>
	public sealed class TcpFrameReader
	{
		private const int HeaderSize = 5;
		private const int InitialBufferSize = 64 * 1024;
		private const int MaxFrameSize = 512 * 1024 * 1024;

		private byte[] _buffer = new byte[InitialBufferSize];
		private int _count;

		/// <summary>
		/// Добавляет прочитанные байты и возвращает команды из всех полностью полученных кадров
		/// </summary>
		public List<string> Append(byte[] data, int count)
		{
			EnsureCapacity(_count + count);
			Buffer.BlockCopy(data, 0, _buffer, _count, count);
			_count += count;

			var commands = new List<string>();
			var position = 0;
			while (true)
			{
				// ищем начало кадра
				while (position < _count && _buffer[position] != (byte)'@')
					position++;
				if (_count - position < HeaderSize)
					break;

				var size = BitConverter.ToInt32(_buffer, position + 1);
				if (size < 0 || size > MaxFrameSize)
				{
					// не заголовок — ищем следующий '@'
					position++;
					continue;
				}

				var end = position + HeaderSize + size;
				if (end >= _count)
					break; // кадр ещё не дочитан

				if (_buffer[end] != (byte)'#')
				{
					position++;
					continue;
				}

				var command = Encoding.UTF8.GetString(_buffer, position + HeaderSize, size);
				if (!string.IsNullOrWhiteSpace(command))
					commands.Add(command);
				position = end + 1;
			}

			// сдвигаем неразобранный остаток в начало буфера
			if (position > 0)
			{
				Buffer.BlockCopy(_buffer, position, _buffer, 0, _count - position);
				_count -= position;
			}
			// после большого кадра не держим большой буфер
			if (_count == 0 && _buffer.Length > InitialBufferSize * 16)
				_buffer = new byte[InitialBufferSize];

			return commands;
		}

		private void EnsureCapacity(int required)
		{
			if (required <= _buffer.Length)
				return;
			var size = _buffer.Length;
			while (size < required)
				size = size > int.MaxValue / 2 ? required : size * 2;
			Array.Resize(ref _buffer, size);
		}
	}
}
