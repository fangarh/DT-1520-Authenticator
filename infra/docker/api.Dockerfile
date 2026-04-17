FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY backend ./backend

WORKDIR /src/backend
RUN dotnet restore ./OtpAuth.Api/OtpAuth.Api.csproj
RUN dotnet publish ./OtpAuth.Api/OtpAuth.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish ./

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

USER $APP_UID

ENTRYPOINT ["dotnet", "OtpAuth.Api.dll"]
