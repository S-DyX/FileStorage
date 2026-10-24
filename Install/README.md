# Пакеты deb и rpm для FileStorage.Api.Host

Пакет `bsa-filestorage` ставит API в `/opt/bsa-filestorage` и запускает его как службу systemd `bsa-filestorage`
от системного пользователя `bsafilestorage`.

## Сборка

Нужны `dpkg-deb` и .NET SDK целевой версии проекта (сейчас `net9.0`). Проще всего собирать в контейнере SDK
(образ на Debian, `dpkg-deb` в нём есть), из PowerShell:

```powershell
cd D:\Work\FileStorage
docker run --rm -v "${PWD}:/src" -w /src mcr.microsoft.com/dotnet/sdk:9.0 bash Install/build-deb.sh
```

Или в Linux / WSL (Ubuntu, не служебный дистрибутив `docker-desktop`):

```bash
cd /mnt/d/Work/FileStorage
./Install/build-deb.sh                          # framework-dependent, linux-x64
SELF_CONTAINED=true ./Install/build-deb.sh      # с рантаймом .NET внутри пакета
RID=linux-arm64 ./Install/build-deb.sh          # для arm64
```

Готовый пакет: `Install/out/bsa-filestorage_<версия>_<arch>.deb`. Версия берётся из `<Version>` в
`FileStorage.Api.Host.csproj` (или из переменной `VERSION`).

Если публикация уже сделана (например, в Visual Studio под `linux-x64`), её можно передать без повторного `dotnet publish`:

```bash
PUBLISH_DIR=/mnt/d/Work/FileStorage/FileStorage.Api.Host/bin/Release/net9.0/linux-x64/publish ./Install/build-deb.sh
```

## Установка

```bash
sudo apt install ./bsa-filestorage_6.0.60_amd64.deb   # подтянет aspnetcore-runtime-<версия .NET проекта>
systemctl status bsa-filestorage
journalctl -u bsa-filestorage -f
```

HTTP API: `http://<сервер>:11580/api/v1/filestorage/...` и `.../api/v1/folderStorage/...`;
TCP-сервер команд — порт `11581`.

## Где что лежит

| Путь | Что |
|---|---|
| `/opt/bsa-filestorage` | приложение; конфиги здесь — ссылки на `/etc/bsa-filestorage` |
| `/etc/bsa-filestorage/appsettings.json` | адрес и порт HTTP (`FileStorage:Host`, по умолчанию `http://+:11580`) |
| `/etc/bsa-filestorage/filestorage.json` | хранилище: `RootDirectory` = `/var/bsa/filestorage`, глубина дерева, адрес и порт TCP (`Connection:Tcp`, по умолчанию `0.0.0.0:11581`) |
| `/etc/bsa-filestorage/nlog.config` | логирование (серверный вариант из `Install/config/nlog.config`) |
| `/var/bsa/filestorage` | файлы хранилищ (домашняя папка пользователя `bsafilestorage`) |
| `/var/log/bsa/FileStorage.Api.Host` | логи NLog: `Info.log`, `Errors.log` и их архивы (по кругу, 10 МБ × 50 файлов) |

Файлы в `/etc/bsa-filestorage` — conffiles: при обновлении пакета dpkg сохраняет локальные изменения
(и спрашивает, если в новой версии файл тоже изменился). После правки конфигурации:
`sudo systemctl restart bsa-filestorage`.

## Обновление и удаление

- `apt install ./новый.deb` — служба останавливается (журнал изменений хранилищ сбрасывается на диск), пакет обновляется, служба запускается.
- `apt remove bsa-filestorage` — удаляет приложение и службу; конфигурация, файлы хранилищ и логи остаются.
- `apt purge bsa-filestorage` — дополнительно удаляет конфигурацию, логи и пользователя `bsafilestorage`.
  **Файлы хранилищ в `/var/bsa/filestorage` не удаляются никогда** — только вручную.

## Замечания

- Приложение пишет только в `RootDirectory` и в логи, поэтому `/opt/bsa-filestorage` принадлежит root.
- `/var/bsa` и `/var/log/bsa` могут быть общими с другими службами BSA: пакет меняет владельца только у своих папок
  `/var/bsa/filestorage` и `/var/log/bsa/FileStorage.Api.Host`.
- HTTP API и TCP-сервер не проверяют доступ и слушают все интерфейсы. Доступ к портам 11580 и 11581
  стоит ограничить межсетевым экраном; чтобы TCP-сервер был доступен только локально, укажите
  `"Address": "127.0.0.1"` в `filestorage.json`.
- Сжатие включено по умолчанию, настраивать ничего не нужно: файлы хорошо сжимаемых типов (текст, csv, json, xml,
  doc/xls, bmp, wav и др.) хранятся архивом `<id>.zip`, если архив не больше 90% исходного размера. Необязательная
  секция `Compression` в `filestorage.json` переопределяет значения (`Enabled`, `MinSize`, `MaxSize`, `MaxRatio`,
  `Level`, `CompressUnknown`, `Extensions`).
  При чтении архив распаковывается прозрачно; крупные файлы (больше 1 МБ) распаковываются во временный каталог
  (`/tmp/filestorage-archives`) и держатся там 5 минут после последнего чтения. Выключение (`"Enabled": false`)
  действует только на новые файлы — уже сжатые продолжают читаться.
- `TreeDepth` и `ElementsCount` задают раскладку файлов на диске: меняйте их только для нового, пустого хранилища.
  Для существующего — через `changeDepth` API (глубина сохраняется в `depth.config` в каталоге хранилища).
- Логирование в пакете (и в Docker-образе) — `Install/config/nlog.config`, а не `nlog.config` проекта (там уровень Trace
  для отладки): в `Info.log` пишется Info и выше (в том числе строка на каждый HTTP-запрос), в `Errors.log` — ошибки;
  в журнал systemd — только предупреждения, ошибки и запуск/остановка. Служебные сообщения ASP.NET Core
  ниже Warning отбрасываются. Для отладки поднимите подробность в `/etc/bsa-filestorage/nlog.config`
  (`minlevel="Trace"`): NLog перечитывает файл сам, перезапуск не нужен.
- Логи пишутся по кругу: файл до 10 МБ, затем он уходит в архив `Info.00.log` (самый новый) … `Info.48.log`;
  на каждый лог не больше 50 файлов, то есть не больше 500 МБ. Чистить их вручную не нужно.
- Зависимость пакета — `aspnetcore-runtime-<версия>` по `<TargetFramework>` проекта (для `net9.0` — `aspnetcore-runtime-9.0`).
  Он есть в Ubuntu 24.04+ (backports) и в репозитории Microsoft (`packages.microsoft.com`). Для систем без него —
  сборка с `SELF_CONTAINED=true` (рантайм внутри пакета).

## Пакет rpm (RHEL, Rocky, Alma, Fedora, РЕД ОС)

Файлы: `build-rpm.sh` и `rpm/bsa-filestorage.spec`. Раскладка, служба, пользователь и конфигурация те же, что у deb
(конфиги — `%config(noreplace)`, сохраняются при обновлении; юнит — `/usr/lib/systemd/system`).

```powershell
cd D:\Work\FileStorage
docker run --rm -v "${PWD}:/src" -w /src mcr.microsoft.com/dotnet/sdk:9.0 bash Install/build-rpm.sh
```

Скрипт сам ставит `rpmbuild` в контейнер (пакет `rpm`). Результат: `Install\out\bsa-filestorage-<версия>-1.x86_64.rpm`.
Те же переменные: `SELF_CONTAINED=true`, `RID=linux-arm64`, `VERSION`, `PUBLISH_DIR`.

```bash
sudo dnf install ./bsa-filestorage-6.0.60-1.x86_64.rpm   # подтянет aspnetcore-runtime-9.0
systemctl status bsa-filestorage
```

- `dnf remove bsa-filestorage` — служба останавливается и удаляется; изменённые конфиги сохраняются как `*.rpmsave`,
  файлы хранилищ в `/var/bsa/filestorage` и логи остаются.
- Зависимость `aspnetcore-runtime-9.0` есть в AppStream RHEL 8/9 и в Fedora; иначе — `SELF_CONTAINED=true`.
