docker run --rm -v "${PWD}:/src" -w /src -e SELF\_CONTAINED=true mcr.microsoft.com/dotnet/sdk:9.0 bash Install/build-deb.sh

docker run --rm -v "${PWD}:/src" -w /src mcr.microsoft.com/dotnet/sdk:9.0 bash Install/build-rpm.sh

