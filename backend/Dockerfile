# 1. Aşama: Derleme (Build)
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Önce bağımlılık dosyalarını kopyalayıp restore ediyoruz (Docker önbellek optimizasyonu)
COPY VehicleTracker.Domain/VehicleTracker.Domain.csproj VehicleTracker.Domain/
COPY VehicleTracker.Application/VehicleTracker.Application.csproj VehicleTracker.Application/
COPY VehicleTracker.Infrastructure/VehicleTracker.Infrastructure.csproj VehicleTracker.Infrastructure/
COPY VehicleTracker.WebApi/VehicleTracker.WebApi.csproj VehicleTracker.WebApi/

RUN dotnet restore VehicleTracker.WebApi/VehicleTracker.WebApi.csproj

# Tüm kaynak kodları kopyala ve derle
COPY . .
WORKDIR /src/VehicleTracker.WebApi
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# 2. Aşama: Çalıştırma (Runtime)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

EXPOSE 5086
ENV ASPNETCORE_URLS=http://+:5086
ENV ASPNETCORE_ENVIRONMENT=Development

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "VehicleTracker.WebApi.dll"]