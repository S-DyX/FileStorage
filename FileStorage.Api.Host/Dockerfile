#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/core/aspnet:3.1-buster-slim AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/core/sdk:3.1-buster AS build
WORKDIR /src
COPY ["FileStorage.Api.Host/FileStorage.Api.Host.csproj", "FileStorage.Api.Host/"]
COPY ["FileStorage.Core/FileStorage.Core.csproj", "FileStorage.Core/"]
COPY ["FileStorage.Contracts.Impl/FileStorage.Contracts.Impl.csproj", "FileStorage.Contracts.Impl/"]
RUN dotnet restore "FileStorage.Api.Host/FileStorage.Api.Host.csproj"
COPY . .
WORKDIR "/src/FileStorage.Api.Host"
RUN dotnet build "FileStorage.Api.Host.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "FileStorage.Api.Host.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "FileStorage.Api.Host.dll"]