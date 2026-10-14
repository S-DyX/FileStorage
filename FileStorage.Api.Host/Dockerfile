# See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
# HTTP API (FileStorage:Host в appsettings.json) и TCP-сервер команд (Connection:Tcp:Port в filestorage.json).
# Раньше были открыты 80 и 443, на которых приложение не слушает.
EXPOSE 11580
EXPOSE 11581
# В контейнере слушаем все интерфейсы: адреса localhost и 127.0.0.1 из настроек снаружи недоступны.
ENV FileStorage__Host=http://+:11580 \
    FILESTORAGE_Connection__Tcp__Address=0.0.0.0
# Каталоги данных (RootDirectory в filestorage.json) и логов NLog (nlog.config);
# приложение работает от непривилегированного пользователя образа (APP_UID).
RUN mkdir -p /var/bsa/filestorage /var/log/bsa/FileStorage.Api.Host \
    && chown -R $APP_UID /var/bsa/filestorage /var/log/bsa
VOLUME ["/var/bsa/filestorage"]
USER $APP_UID

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["FileStorage.Api.Host/FileStorage.Api.Host.csproj", "FileStorage.Api.Host/"]
COPY ["FileStorage.Core/FileStorage.Core.csproj", "FileStorage.Core/"]
COPY ["FileStorage.Contracts.Impl/FileStorage.Contracts.Impl.csproj", "FileStorage.Contracts.Impl/"]
RUN dotnet restore "FileStorage.Api.Host/FileStorage.Api.Host.csproj"
COPY . .
WORKDIR "/src/FileStorage.Api.Host"
RUN dotnet build "FileStorage.Api.Host.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "FileStorage.Api.Host.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "FileStorage.Api.Host.dll"]
