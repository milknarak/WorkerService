FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY Worker/*.csproj ./Worker/
RUN dotnet restore Worker/Worker.csproj

COPY Worker/ ./Worker/
RUN dotnet publish Worker/Worker.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime:9.0
WORKDIR /app

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Worker.dll"]
