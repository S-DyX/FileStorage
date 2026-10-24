# REST-клиент FileStorage: методы по API

## Задача
Привести FileStorage.Contracts.Rest.Impl в соответствие API, исправить ошибки, добавить полезные методы.

## Исправление
- В интерфейсы добавлены (реализация по умолчанию — NotSupportedException, TCP-клиенты не меняются):
  файлы — `GetStorageNames`, `OpenRead`, `OpenReadByExternalId`, `Upload` (multipart одним запросом),
  `GetFullName`, `ChangeDepth`, `GetVersion`; папки — `GetStorageNames`, `OpenRead`, `GetFullName`,
  `ClearTtl`, `ChangeDepth`, `GetVersion`. `FolderStorageRestServiceOld` передаёт их основному клиенту.
- `WriteToStream` зацикливался на пустом ответе.
- Сервер: `stream/external` терял `NotFound` (отдавал 204); `upload` не возвращал id и не принимал файлы
  больше 30 МБ.

## Проверка
dotnet в среде нет, проект не компилировался. Проверить вызовы новых методов против сервера.

## Файлы
- FileStorage.Contracts.Rest.Impl/FileStorage/FileStorageRestService.cs
- FileStorage.Contracts.Rest.Impl/FileStorage/FolderStorageRestService.cs
- FileStorage.Contracts.Rest.Impl/FileStorage/FolderStorageRestServiceOld.cs
- FileStorage.Contracts.Rest.Impl/FileStorage/RestHelper.cs
- FileStorage.Contracts.Rest.Impl/IFolderStorageRestService.cs
- FileStorage.Api.Host/Controllers/FileStorageController.cs, FolderStorageController.cs
