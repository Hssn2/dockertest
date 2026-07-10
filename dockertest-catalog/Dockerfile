FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8090
ENV ASPNETCORE_URLS=http://+:8090

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["dockertest-catalog.csproj", "./"]
RUN dotnet restore "dockertest-catalog.csproj"
COPY . .
RUN dotnet build "dockertest-catalog.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "dockertest-catalog.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
ARG APP_VERSION=dev
ENV APP_VERSION=$APP_VERSION
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "dockertest-catalog.dll"]
