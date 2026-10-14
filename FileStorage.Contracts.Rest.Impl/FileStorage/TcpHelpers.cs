using System;
using System.Collections.Generic;
using System.Text;

namespace FileStorage.Contracts.Rest.Impl.FileStorage
{
	/// <summary>
	/// Разбор ответов TCP-сервера
	/// </summary>
	internal static class TcpResponse
	{
		/// <summary>
		/// Размер блока при чтении файла по TCP
		/// </summary>
		public const int ReadChunkSize = 1024 * 1024;

		public static bool IsOk(TcpCommonMessage data)
		{
			return data != null
				&& data.Error == null
				&& data.Type != TcpMessageType.BadRequest
				&& data.Type != TcpMessageType.Unauthorized
				&& data.Type != TcpMessageType.InternalServerError;
		}

		/// <summary>
		/// Исключение, если сервер не ответил или ответил ошибкой (раньше такие ответы игнорировались)
		/// </summary>
		public static void EnsureOk(TcpCommonMessage data, string operation)
		{
			if (data == null)
				throw new InvalidOperationException($"FileStorage: no response to {operation}");
			if (!IsOk(data))
				throw new InvalidOperationException($"FileStorage: {operation} failed: {data.Error?.Error ?? data.Type.ToString()}");
		}

		/// <summary>
		/// Часть файла из ответа на команду Read. Новый сервер возвращает только запрошенную часть (IsPartial),
		/// старый — файл целиком; тогда нужная часть вырезается здесь.
		/// </summary>
		public static byte[] GetRange(ReadMessage read, long offset, long size)
		{
			var content = read?.Content;
			if (content == null)
				return null;
			if (read.IsPartial)
				return content;
			if (offset >= content.Length)
				return new byte[0];
			var count = (int)Math.Min(size, content.Length - offset);
			if (offset == 0 && count == content.Length)
				return content;
			var result = new byte[count];
			Buffer.BlockCopy(content, (int)offset, result, 0, count);
			return result;
		}
	}

	/// <summary>
	/// Собирает кадры протокола ('@' + длина Int32 + UTF-8 данные + '#') из потока байт.
	/// Неполный кадр остаётся в буфере до следующего чтения.
	/// </summary>
	internal sealed class TcpFrameReader
	{
		private const int HeaderSize = 5;
		private const int InitialBufferSize = 64 * 1024;
		private const int MaxFrameSize = 512 * 1024 * 1024;

		private byte[] _buffer = new byte[InitialBufferSize];
		private int _count;

		public void Clear()
		{
			_count = 0;
			if (_buffer.Length > InitialBufferSize * 16)
				_buffer = new byte[InitialBufferSize];
		}

		/// <summary>
		/// Добавляет полученные байты и возвращает команды из всех полностью полученных кадров
		/// </summary>
		public List<string> Append(byte[] data, int count)
		{
			if (_count + count > _buffer.Length)
			{
				var size = _buffer.Length;
				while (size < _count + count)
					size = size > int.MaxValue / 2 ? _count + count : size * 2;
				Array.Resize(ref _buffer, size);
			}
			Buffer.BlockCopy(data, 0, _buffer, _count, count);
			_count += count;

			var commands = new List<string>();
			var position = 0;
			while (true)
			{
				while (position < _count && _buffer[position] != (byte)'@')
					position++;
				if (_count - position < HeaderSize)
					break;

				var size = BitConverter.ToInt32(_buffer, position + 1);
				if (size < 0 || size > MaxFrameSize)
				{
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

			if (position > 0)
			{
				Buffer.BlockCopy(_buffer, position, _buffer, 0, _count - position);
				_count -= position;
			}
			return commands;
		}
	}
}
