# RPM spec of FileStorage.Api.Host as a systemd service.
# Built by Install/build-rpm.sh: the application is published beforehand and staged into the stagedir define;
# the spec only packages the staged tree. Defines passed by the script: pkgversion, pkgrequires, stagedir.

# prebuilt .NET binaries: no debug package, no stripping, no build-id links, no automatic dependency scan
%global debug_package %{nil}
%global __strip /bin/true
%global __os_install_post %{nil}
%define _build_id_links none

Name:           bsa-filestorage
Version:        %{pkgversion}
Release:        1%{?dist}
Summary:        BSA FileStorage API host - hierarchical file storage service
License:        BSD-2-Clause
URL:            http://bsasearch.ru/filestorage.html
AutoReqProv:    no
Requires:       %{pkgrequires}
Requires(pre):  shadow-utils
Requires(post): systemd
Requires(preun): systemd
Requires(postun): systemd

%description
HTTP API (ASP.NET Core) and TCP command server over the FileStorage.Core
hierarchical file storage: files and folders, chunked upload, partial reads,
change log for synchronization and TTL cleanup.
Runs as the systemd service bsa-filestorage.

%prep

%build

%install
mkdir -p %{buildroot}
cp -a %{stagedir}/. %{buildroot}/

%pre
getent group bsafilestorage >/dev/null || groupadd -r bsafilestorage
getent passwd bsafilestorage >/dev/null || \
	useradd -r -g bsafilestorage -d /var/bsa/filestorage -s /sbin/nologin -c "BSA FileStorage service" bsafilestorage
exit 0

%post
# $1: 1 - install, 2 - upgrade (the service is restarted in %%postun of the old package)
if [ -d /run/systemd/system ]; then
	systemctl daemon-reload >/dev/null 2>&1 || :
	if [ "$1" -eq 1 ]; then
		systemctl enable bsa-filestorage.service >/dev/null 2>&1 || :
		systemctl start bsa-filestorage.service || echo "bsa-filestorage: service did not start, see: journalctl -u bsa-filestorage"
	fi
fi
exit 0

%preun
# $1: 0 - removal, 1 - upgrade
if [ "$1" -eq 0 ] && [ -d /run/systemd/system ]; then
	# stopping flushes the change log of the storages; it may take up to TimeoutStopSec
	systemctl stop bsa-filestorage.service || :
	systemctl disable bsa-filestorage.service >/dev/null 2>&1 || :
fi
exit 0

%postun
if [ -d /run/systemd/system ]; then
	systemctl daemon-reload >/dev/null 2>&1 || :
	if [ "$1" -ge 1 ]; then
		systemctl try-restart bsa-filestorage.service || :
	fi
fi
if [ "$1" -eq 0 ] && [ -d /var/bsa/filestorage ]; then
	# stored files are never deleted automatically
	echo "bsa-filestorage: stored files are kept in /var/bsa/filestorage (delete them manually if not needed)"
fi
exit 0

%files
# the application does not write next to its binaries: owned by root
/opt/bsa-filestorage
%dir %attr(0750,root,bsafilestorage) /etc/bsa-filestorage
%config(noreplace) %attr(0640,root,bsafilestorage) /etc/bsa-filestorage/*
/usr/lib/systemd/system/bsa-filestorage.service
# /var/bsa and /var/log/bsa may be shared with other BSA services: owned by root
%dir %attr(0755,root,root) /var/bsa
%dir %attr(0750,bsafilestorage,bsafilestorage) /var/bsa/filestorage
%dir %attr(0755,root,root) /var/log/bsa
%dir %attr(0755,bsafilestorage,bsafilestorage) /var/log/bsa/FileStorage.Api.Host
