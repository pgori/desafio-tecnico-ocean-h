# Build em uma imagem com SDK, execucao em uma imagem so com runtime.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY LabDesk.sln ./
COPY LabDesk.Domain/*.csproj LabDesk.Domain/
COPY LabDesk.Infrastructure/*.csproj LabDesk.Infrastructure/
COPY LabDesk.Api/*.csproj LabDesk.Api/
COPY LabDesk.Tests/*.csproj LabDesk.Tests/

# Restaurar antes de copiar o codigo aproveita o cache quando so o codigo muda.
RUN dotnet restore LabDesk.Api/LabDesk.Api.csproj

COPY . .
RUN dotnet publish LabDesk.Api/LabDesk.Api.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "LabDesk.Api.dll"]
