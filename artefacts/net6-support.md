# Сборка FileStorage.Core и REST-клиента под .NET 6

## Задача
Собрать проекты под .NET 6: ошибки `Count` у `ReadOnlySpan<byte>`, `Stream.ReadAtLeast`, `Stream.ReadExactly`,
`Utf8.IsValid`; FileStorage.Api.Host.Test (net6.0) не мог ссылаться на FileStorage.Contracts.Rest.Impl (net9.0).

## Исправление
- FileStorage.Core и FileStorage.Contracts.Rest.Impl собираются под `net6.0;net9.0`, `LangVersion latest`
  (u8-литералы и прочие новые конструкции языка работают и на .NET 6).
- Пакеты по платформе: Microsoft.Extensions.Configuration.* 6.0 для net6.0 и 9.0.7 для net9.0;
  Service.Registry 6.0.47 для net6.0 (9.x собран только под net9.0) и 9.0.47 для net9.0.
- `FileSignatureDetector`: свои `ReadAtLeast`, `IsValidUtf8` (через `Rune.DecodeFromUtf8`) и `CountOf`
  вместо API .NET 7–8.
- `FileArchive.OpenRead`: копирование записи архива в `MemoryStream` вместо `ReadExactly`.
- REST-клиенты: конструктор без фабрики передаёт `DefaultHttpClientFactory` — Service.Registry 6.x оставлял
  `ClientFactory` пустым, и любой запрос падал с NullReferenceException.

## Проверка
dotnet в среде нет, сборка не запускалась. Собрать решение: FileStorage.Core и
FileStorage.Contracts.Rest.Impl — под обе платформы, FileStorage.Api.Host.Test — под net6.0.

## Файлы
- FileStorage.Core/FileStorage.Core.csproj
- FileStorage.Core/Helpers/FileSignatureDetector.cs
- FileStorage.Core/Helpers/FileArchive.cs
- FileStorage.Contracts.Rest.Impl/FileStorage.Contracts.Rest.Impl.csproj
- FileStorage.Contracts.Rest.Impl/FileStorage/DefaultHttpClientFactory.cs (новый)
- FileStorage.Contracts.Rest.Impl/FileStorage/FileStorageRestService.cs
- FileStorage.Contracts.Rest.Impl/FileStorage/FolderStorageRestService.cs
