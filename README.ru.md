# FileStorage

[English version](README.md)

Иерархическое файловое хранилище для .NET: файлы адресуются по id и хранятся на диске в сбалансированном
дереве каталогов (листья — файлы), поэтому ни в одном каталоге не скапливаются миллионы файлов. База данных
не нужна: расположение файла вычисляется по его id.

В репозитории — библиотека хранилища, готовый сервер с HTTP- и TCP-API и клиенты для .NET.

| Компонент | Что это |
|---|---|
| [FileStorage.Core](https://www.nuget.org/packages/FileStorage.Core) | Библиотека хранилища (NuGet, .NET 9) |
| FileStorage.Core.Async | Предыдущая асинхронная версия библиотеки (.NET 6) |
| FileStorage.Contracts.Impl | Сервисы сервера: обработчики HTTP/TCP, синхронизация |
| FileStorage.Api.Host | Сервер: HTTP API (порт 11580) и TCP-сервер команд (порт 11581) |
| [FileStorage.Contracts.Rest.Impl](https://www.nuget.org/packages/FileStorage.Contracts.Rest.Impl) | Клиенты .NET: REST и TCP |
| Install | Пакеты deb и rpm со службой systemd |

## Возможности

- **Два вида хранилищ.** *Файловое* — файл по id. *Папочное* — файлы сгруппированы по id папки
  (например, все вложения документа), есть перенос между папками.
- **Именованные хранилища.** Один сервер держит несколько независимых хранилищ; выбор — параметром `name`.
- **Внешние id.** Ваши id переводятся во внутренние (SHA-1), ключом файла может быть любая строка.
- **Загрузка частями и чтение по смещению.** Загрузка блоками с сессиями, чтение по offset, HTTP Range.
- **Прозрачное сжатие.** Файлы хорошо сжимаемых типов (текст, csv, json, xml, doc/xls, bmp, wav…) хранятся
  как `<id>.zip` и распаковываются при чтении. Включено по умолчанию, настраивать ничего не нужно.
- **Определение типа файла** по сигнатуре (магическим байтам) при скачивании.
- **Журнал изменений** для синхронизации и **очистка по TTL** старых файлов.
- **Адресация по содержимому.** `SaveAsHash` сохраняет файл под хешем содержимого (без дублей).

## Как хранятся файлы

Путь задаёт внутренний id (обычно SHA-1 от вашего id). При `TreeDepth = 3` и `ElementsCount = 6`
файл с id `a1b2c3d4…` лежит здесь:

```
<RootDirectory>/<имя хранилища>/a/1/b/2c3/a1b2c3d4…        (или a1b2c3d4….zip, если сжат)
```

Файл — это либо `<id>`, либо `<id>.zip`; если нет ни того, ни другого, файла нет. `TreeDepth` и
`ElementsCount` задают раскладку на диске: меняйте их только для нового, пустого хранилища
(для существующего — через API `changeDepth`).

## Быстрый старт

### Docker

```bash
docker run -d --name filestorage \
  -p 11580:11580 -p 11581:11581 \
  -v filestorage-data:/var/bsa/filestorage \
  bsacore/filestorageapihost
curl http://localhost:11580/api/v1/version
```

### Пакеты Linux (deb, rpm)

```bash
sudo apt install ./bsa-filestorage_<версия>_amd64.deb      # Debian, Ubuntu, Astra Linux
sudo dnf install ./bsa-filestorage-<версия>-1.x86_64.rpm   # RHEL, Rocky, Alma, Fedora, РЕД ОС
systemctl status bsa-filestorage
```

Сборка пакетов и раскладка файлов — в [Install/README.md](Install/README.md).

### Из исходников

```bash
dotnet run --project FileStorage.Api.Host
```

## Настройка

`filestorage.json` рядом с сервером (у пакетов — `/etc/bsa-filestorage/filestorage.json`):

```json
{
  "RootDirectory": "/var/bsa/filestorage",
  "ElementsCount": 6,
  "TreeDepth": 3,
  "Connection": { "Tcp": { "Address": "127.0.0.1", "Port": 11581 } }
}
```

Любой ключ переопределяется переменной окружения с префиксом `FILESTORAGE_` (`__` разделяет вложенные ключи),
например `FILESTORAGE_Connection__Tcp__Address=0.0.0.0`. Адрес HTTP задаётся в `appsettings.json`:
`FileStorage:Host` (по умолчанию `http://localhost:11580`) или переменной `FileStorage__Host`.

### Сжатие (необязательно)

Сжатие включено со значениями ниже; секция `Compression` переопределяет только указанные в ней ключи.

```json
"Compression": {
  "Enabled": true,          // сжимать новые файлы; уже сжатые читаются в любом случае
  "MinSize": 4096,          // байт
  "MaxSize": 268435456,     // байт
  "MaxRatio": 0.9,          // архив остаётся, только если он не больше 90% от файла
  "Level": "Optimal",       // Optimal, Fastest, SmallestSize
  "CompressUnknown": false, // сжимать файлы неизвестного типа
  "Extensions": ["txt", "csv", "json", "xml", "html", "svg", "rtf", "ps", "doc", "xls", "ppt", "msg",
                 "bmp", "tif", "ico", "psd", "wav", "tar", "sqlite", "exe", "wasm", "ttf", "otf"]
}
```

Архивы больше 1 МБ при чтении распаковываются во временный каталог и хранятся там 5 минут после последнего
чтения, поэтому чтение частями не распаковывает архив заново.

## HTTP API

Базовые пути: `/api/v1/fileStorage` и `/api/v1/folderStorage`. Все методы принимают `name=<хранилище>`.
Методы папочного хранилища вместо `id` принимают `folderId` и `fileId`.

| Метод | Путь | Назначение |
|---|---|---|
| `POST` | `upload` | загрузка multipart; имя файла — внешний id (файловое хранилище) |
| `POST` | `savebytes` | запись блока: `{ id, folderId, bytes, sessionId, close }` |
| `GET` | `stream?id=` | скачивание, поддерживает `Range` |
| `GET` | `stream/external?id=` | скачивание по внешнему id |
| `GET` | `getbytes?id=&offset=&count=` | чтение части файла |
| `GET` | `exists?id=`, `exists/External?id=` | есть ли файл |
| `GET` | `size?id=` | размер файла |
| `GET` | `external?id=` | внутренний id по внешнему |
| `GET` | `delete?id=`, `delete/external?id=` | удаление |
| `GET` | `move?folderId=&toFolderId=&fileId=` | перенос файла в другую папку (папочное хранилище) |
| `GET` | `clearTtl?daysAgo=` | удаление файлов старше N дней (папочное хранилище) |
| `GET` | `count`, `list` | число файлов, имена хранилищ |
| `GET` | `path?id=` | полный путь на диске сервера |
| `GET` | `changeDepth?newDepth=` | смена глубины дерева хранилища |
| `GET` | `/api/v1/version` | версия сервера |

API не проверяет доступ: открывайте порты 11580 и 11581 только в доверенной сети.

## Клиенты .NET

```bash
dotnet add package FileStorage.Contracts.Rest.Impl
```

```csharp
var settings = new RestClientResponseSettings { Host = "http://localhost:11580/" };
IFileStorageRestService files = new FileStorageRestService(settings);

using (var input = File.OpenRead("report.csv"))
{
    var id = files.Upload("report-2026.csv", input, "reports");   // один multipart-запрос
}

using (var stream = files.OpenReadByExternalId("report-2026.csv", "reports"))
using (var output = File.Create("copy.csv"))
{
    stream.CopyTo(output);
}

IFolderStorageRestService folders = new FolderStorageRestService(settings);
folders.Write("document-42", "scan.pdf", "attachments", File.ReadAllBytes("scan.pdf"), null);
var size = folders.GetSize("document-42", "scan.pdf", "attachments");
```

TCP-клиенты (`TcpFileStorageService`, `TcpFolderStorageService`) реализуют те же интерфейсы поверх
TCP-протокола команд. С [Service.Registry](https://www.nuget.org/packages/Service.Registry) клиенты создаются
по имени: `new ServiceRegistryFactory().CreateRest<IFileStorageRestService>()`.

## Библиотека напрямую

```bash
dotnet add package FileStorage.Core
```

```csharp
var settings = new FileStorageSettings("/data/storage");
using var factory = new FileStorageFactory(settings, new FileStorageVirtual(), null);
var storage = factory.Get("docs");

using (var input = File.OpenRead("contract.docx"))
{
    storage.Save(input, "contract-17");
}

if (storage.Exists("contract-17"))
{
    using var stream = storage.GetStream("contract-17");   // сжатые файлы распаковываются прозрачно
}

var token = storage.SaveAsHash(File.OpenRead("photo.jpg")); // id = SHA-1 содержимого
```

## Лицензия

[BSD-2-Clause](LICENSE). Страница проекта: http://bsasearch.ru/filestorage.html
