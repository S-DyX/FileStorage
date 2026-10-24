# Хранение сжимаемых файлов архивами

## Задача
Хранить файлы хорошо сжимаемых типов в zip-архивах и распаковывать их при запросе, без базы и
дополнительных полей: файл `hash` или архив `hash.zip`; если нет ни того, ни другого — файла нет.

## Исправление
- `FileArchive` (Core/Helpers): сжатие во временный архив с одной записью (имя записи = id), чтение,
  распаковка, размер содержимого из каталога архива. Архив оставляется, только если он не больше
  `MaxRatio` от исходного размера, иначе файл хранится как есть.
- Тип файла определяется по сигнатуре: `FileSignatureDetector` перенесён в Core
  (`FileStorage.Core.Helpers`), в Api.Host остался тонкий адаптер для `GetContentType`.
- `FileStorage` и `FolderStorage`:
  - запись (`Save`, `SaveToFile` при закрытии, `SaveAsHash`) — сжатие при переносе временного файла на место;
    прежняя версия в другом виде удаляется;
  - `Exists`, `GetSize` (размер исходного содержимого), `GetStream` (поток с перемоткой, только чтение),
    `Delete`, `Move` (архив переносится без распаковки) учитывают `.zip`;
  - `Append` сначала распаковывает архив на место файла;
  - `ChangeDepthDirectory`, `GetIds`, журнал, TTL и `RefreshLog` получают id без `.zip`.
- Чтение: содержимое до 1 МБ распаковывается в память, больше — в кеш распакованных копий во временном
  каталоге (`<tmp>/filestorage-archives/<pid>`), копия удаляется через 5 минут без чтений или при изменении
  архива. Без кеша постраничное чтение (getbytes, TCP, Range) распаковывало бы архив с начала на каждый запрос.
- Сжатие включено по умолчанию в `FileStorageSettings` и не требует конфигурации: на установленных серверах
  конфиг не обновить. Необязательная секция `Compression` в `filestorage.json` только переопределяет значения
  (`Enabled`, `MinSize`, `MaxSize`, `MaxRatio`, `Level`, `CompressUnknown`, `Extensions`); в поставляемые
  конфиги она не добавляется. Свойство `IFileStorageSettings.Compression` у сторонних реализаций по умолчанию
  null — сжатия нет.

## Проверка
dotnet в среде нет, проект не компилировался. Проверить: сохранить txt/csv/json больше 4 КБ — на диске
`<id>.zip`; `exists`, `size`, `getbytes` с offset, `stream` с Range возвращают исходное содержимое;
jpg/pdf/docx хранятся без сжатия; `move`, `delete`, `changeDepth`, `clearTtl` работают со сжатыми файлами.

## Ограничения
- Если процесс прервётся между установкой архива и удалением прежней несжатой версии, читается прежняя
  версия (несжатый файл проверяется первым), архив останется лишним.
- `path` (`GetFullFileName`) по-прежнему возвращает путь без `.zip`.
- id, оканчивающиеся на `.zip`, считаются архивами файла без этого окончания.
- FileStorage.Core.Async не менялся.

## Файлы
- FileStorage.Core/Helpers/FileArchive.cs (новый)
- FileStorage.Core/Helpers/FileSignatureDetector.cs (перенесён из Api.Host)
- FileStorage.Core/Interfaces/Settings/FileCompressionSettings.cs (новый)
- FileStorage.Core/Interfaces/Settings/IFileStorageSettings.cs, FileStorage.Core/FileStorageSettings.cs
- FileStorage.Core/FileStorage.cs, FileStorage.Core/FolderStorage.cs
- FileStorage.Api.Host/Helpers/FileSignatureDetector.cs (адаптер)
- Install/README.md
