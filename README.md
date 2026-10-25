# FileStorage

[Русская версия](README.ru.md)

Hierarchical file storage for .NET: files are addressed by id and stored on disk in a balanced tree of
folders (the leaves are files), so no directory grows to millions of entries. No database is needed:
the location of a file is computed from its id.

The repository contains the storage library, a ready-to-run server with HTTP and TCP APIs, and .NET clients.

| Component | What it is |
|---|---|
| [FileStorage.Core](https://www.nuget.org/packages/FileStorage.Core) | Storage library (NuGet, .NET 9) |
| FileStorage.Core.Async | Previous async version of the library (.NET 6) |
| FileStorage.Contracts.Impl | Server services: HTTP/TCP handlers, synchronization |
| FileStorage.Api.Host | Server: HTTP API (port 11580) and TCP command server (port 11581) |
| [FileStorage.Contracts.Rest.Impl](https://www.nuget.org/packages/FileStorage.Contracts.Rest.Impl) | .NET clients: REST and TCP |
| Install | deb and rpm packages with a systemd service |

## Features

- **Two kinds of storages.** *File storage* — a file per id. *Folder storage* — files grouped by a folder
  id (e.g. all attachments of a document), with move between folders.
- **Named storages.** One server hosts several independent storages; select one with the `name` parameter.
- **External ids.** Your own ids are mapped to internal ids (SHA-1), so any string can be a file key.
- **Chunked upload and partial reads.** Upload in chunks with sessions, read by offset, HTTP Range requests.
- **Transparent compression.** Files of well compressible types (text, csv, json, xml, doc/xls, bmp, wav…)
  are stored as `<id>.zip` and unpacked on read. On by default, no configuration needed.
- **Content type detection** by file signature (magic bytes) for downloads.
- **Change log** for synchronization and **TTL cleanup** of old files.
- **Content addressing.** `SaveAsHash` stores a file under the hash of its content (deduplication).

## How files are stored

The internal id (usually SHA-1 of your id) defines the path. With `TreeDepth = 3` and `ElementsCount = 6`
the id `a1b2c3d4…` is stored as:

```
<RootDirectory>/<storage name>/a/1/b/2c3/a1b2c3d4…        (or a1b2c3d4….zip when compressed)
```

A file is either `<id>` or `<id>.zip`; if neither exists, there is no such file. `TreeDepth` and
`ElementsCount` define the layout on disk: change them only for a new, empty storage
(for an existing one use the `changeDepth` API).

## Quick start

### Docker

```bash
docker run -d --name filestorage \
  -p 11580:11580 -p 11581:11581 \
  -v filestorage-data:/var/bsa/filestorage \
  bsacore/filestorageapihost
curl http://localhost:11580/api/v1/version
```

### Linux packages (deb, rpm)

```bash
sudo apt install ./bsa-filestorage_<version>_amd64.deb      # Debian, Ubuntu, Astra Linux
sudo dnf install ./bsa-filestorage-<version>-1.x86_64.rpm   # RHEL, Rocky, Alma, Fedora, RED OS
systemctl status bsa-filestorage
```

Building the packages and the file layout: [Install/README.md](Install/README.md) (in Russian).

### From source

```bash
dotnet run --project FileStorage.Api.Host
```

## Configuration

`filestorage.json` next to the server (or `/etc/bsa-filestorage/filestorage.json` for the packages):

```json
{
  "RootDirectory": "/var/bsa/filestorage",
  "ElementsCount": 6,
  "TreeDepth": 3,
  "Connection": { "Tcp": { "Address": "127.0.0.1", "Port": 11581 } }
}
```

Any key can be overridden by an environment variable with the `FILESTORAGE_` prefix
(`__` separates nested keys), e.g. `FILESTORAGE_Connection__Tcp__Address=0.0.0.0`.
The HTTP address is set in `appsettings.json`: `FileStorage:Host` (default `http://localhost:11580`),
or the `FileStorage__Host` environment variable.

### Compression (optional)

Compression is enabled with the defaults below; the `Compression` section overrides only the keys it contains.

```json
"Compression": {
  "Enabled": true,          // compress new files; existing archives are read anyway
  "MinSize": 4096,          // bytes
  "MaxSize": 268435456,     // bytes
  "MaxRatio": 0.9,          // keep the archive only if it is not larger than 90% of the file
  "Level": "Optimal",       // Optimal, Fastest, SmallestSize
  "CompressUnknown": false, // compress files of an unknown type
  "Extensions": ["txt", "csv", "json", "xml", "html", "svg", "rtf", "ps", "doc", "xls", "ppt", "msg",
                 "bmp", "tif", "ico", "psd", "wav", "tar", "sqlite", "exe", "wasm", "ttf", "otf"]
}
```

Archives larger than 1 MB are unpacked on read into a temporary folder and kept there for 5 minutes
after the last read, so partial reads do not unpack the archive again.

## HTTP API

Base paths: `/api/v1/fileStorage` and `/api/v1/folderStorage`. Every method accepts `name=<storage>`.
Folder storage methods take `folderId` and `fileId` instead of `id`.

| Method | Path | Purpose |
|---|---|---|
| `POST` | `upload` | multipart upload; the file name is the external id (file storage) |
| `POST` | `savebytes` | write a chunk: `{ id, folderId, bytes, sessionId, close }` |
| `GET` | `stream?id=` | download, supports `Range` |
| `GET` | `stream/external?id=` | download by external id |
| `GET` | `getbytes?id=&offset=&count=` | read a part of a file |
| `GET` | `exists?id=`, `exists/External?id=` | does the file exist |
| `GET` | `size?id=` | file size |
| `GET` | `external?id=` | internal id for an external id |
| `GET` | `delete?id=`, `delete/external?id=` | delete |
| `GET` | `move?folderId=&toFolderId=&fileId=` | move a file to another folder (folder storage) |
| `GET` | `clearTtl?daysAgo=` | delete files older than N days (folder storage) |
| `GET` | `count`, `list` | number of files, names of storages |
| `GET` | `path?id=` | full path on the server disk |
| `GET` | `changeDepth?newDepth=` | change the tree depth of a storage |
| `GET` | `/api/v1/version` | server version |

The API has no authentication: expose ports 11580 and 11581 only to trusted networks.

## .NET clients

```bash
dotnet add package FileStorage.Contracts.Rest.Impl
```

```csharp
var settings = new RestClientResponseSettings { Host = "http://localhost:11580/" };
IFileStorageRestService files = new FileStorageRestService(settings);

using (var input = File.OpenRead("report.csv"))
{
    var id = files.Upload("report-2026.csv", input, "reports");   // one multipart request
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

TCP clients (`TcpFileStorageService`, `TcpFolderStorageService`) implement the same interfaces over the
TCP command protocol. With [Service.Registry](https://www.nuget.org/packages/Service.Registry) the clients are
created by name: `new ServiceRegistryFactory().CreateRest<IFileStorageRestService>()`.

## Using the library directly

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
    using var stream = storage.GetStream("contract-17");   // compressed files are unpacked transparently
}

var token = storage.SaveAsHash(File.OpenRead("photo.jpg")); // id = SHA-1 of the content
```

## License

[BSD-2-Clause](LICENSE). Project page: http://bsasearch.ru/filestorage.html
