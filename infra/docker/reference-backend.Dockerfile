FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY lib ./lib
COPY rdb_stand ./rdb_stand

WORKDIR /src/rdb_stand
RUN dotnet restore ./src/ReferenceBackend/ReferenceBackend.csproj
RUN dotnet publish ./src/ReferenceBackend/ReferenceBackend.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish ./

ENV ASPNETCORE_URLS=http://+:5188
EXPOSE 5188

USER $APP_UID

ENTRYPOINT ["dotnet", "ReferenceBackend.dll"]
