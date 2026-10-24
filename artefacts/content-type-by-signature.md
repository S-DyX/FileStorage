# Определение типа файла по сигнатуре

## Задача
Переписать `GetContentType(this Stream stream)` так, чтобы он распознавал заголовки всех
основных типов файлов, а сравнение шло по байтам, а не по hex-строкам.

## Исправление
- Определение вынесено в `FileSignatureDetector` (internal static), `GetContentType` вызывает его.
- Читается до 8 КБ в буфер из `ArrayPool`, сигнатуры сравниваются как `ReadOnlySpan<byte>`
  (`SequenceEqual`, `IndexOf`), без `BitConverter.ToString` и строк.
- Порядок: однозначные сигнатуры → контейнеры (ZIP, RIFF, ISO BMFF, Matroska, Ogg, OLE) →
  короткие сигнатуры → MPEG audio/TS, BMP → текстовые форматы.
- Форматы: png, jpg, gif, bmp, tif, webp, heic, avif, ico, svg, psd, djvu; pdf, ps, rtf, doc/xls/ppt/msg
  (по каталогу OLE), docx/xlsx/pptx, odt/ods/odp, epub; zip, jar, apk, rar, 7z, gz, bz2, xz, zst,
  tar, cab, iso; mp3, aac, m4a, wav, flac, ogg, mid, amr; mp4, mov, 3gp, avi, mkv, webm, flv, wmv,
  mpg, ts, ogv; woff, woff2, otf, ttf; exe, wasm, sqlite; txt, csv, html, xml, json.
- Исправлены ошибки старой версии: кириллица в hex-строке PDF, BMP определялся как png,
  rar — как zip с типом `application/zip`, xlsx — по случайным байтам и с типом `text/xlsx`,
  чтение без проверки числа прочитанных байт.
- Типы приведены к стандартным: `application/msword`, `audio/mpeg`, `video/x-msvideo`,
  `application/vnd.rar`, OOXML-типы; расширение jpeg → jpg.
- Позиция потока восстанавливается на исходную (раньше всегда 0); у несикабельного потока не трогается.

## Проверка
dotnet в среде нет, код не компилировался. Логика проверена переносом на Python на реальных
файлах (png, jpg, gif, bmp, tif, webp, ico, svg, pdf, doc, xls, docx, xlsx, odt, ods, zip, tar, gz,
bz2, xz, mp3, aac, m4a, wav, flac, ogg, mp4, mov, mkv, webm, avi, ts, txt, csv, json, html, xml) —
все определились верно. Нужно собрать проект и проверить скачивание с `type=load`.

## Файлы
- FileStorage.Api.Host/Helpers/FileSignatureDetector.cs (новый)
- FileStorage.Api.Host/Helpers/CommonHelper.cs (GetContentType делегирует в детектор)
