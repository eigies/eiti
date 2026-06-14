# syntax=docker/dockerfile:1
# Build context = repo root (C:\eiti\eiti). Build with:
#   docker build -f eiti.Api/Dockerfile -t eiti-api .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore primero solo con los .csproj para cachear la capa de restore
COPY eiti.Domain/eiti.Domain.csproj eiti.Domain/
COPY eiti.Application/eiti.Application.csproj eiti.Application/
COPY eiti.Infrastructure/eiti.Infrastructure.csproj eiti.Infrastructure/
COPY eiti.Api/eiti.Api.csproj eiti.Api/
RUN dotnet restore eiti.Api/eiti.Api.csproj

# Copiar el resto y publicar
COPY . .
RUN dotnet publish eiti.Api/eiti.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Railway/contenedores inyectan PORT; por defecto escuchamos en 8080
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "eiti.Api.dll"]
