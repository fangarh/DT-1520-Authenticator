FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY backend ./backend

WORKDIR /src/backend
RUN dotnet restore ./OtpAuth.Worker/OtpAuth.Worker.csproj
RUN dotnet publish ./OtpAuth.Worker/OtpAuth.Worker.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish ./

USER $APP_UID

ENTRYPOINT ["dotnet", "OtpAuth.Worker.dll"]
