FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

COPY VehicleTracker.Domain/VehicleTracker.Domain.csproj VehicleTracker.Domain/
COPY VehicleTracker.Application/VehicleTracker.Application.csproj VehicleTracker.Application/
COPY VehicleTracker.Infrastructure/VehicleTracker.Infrastructure.csproj VehicleTracker.Infrastructure/
COPY VehicleTracker.WebApi/VehicleTracker.WebApi.csproj VehicleTracker.WebApi/

RUN dotnet restore VehicleTracker.WebApi/VehicleTracker.WebApi.csproj

COPY . .

WORKDIR /src/VehicleTracker.WebApi

RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

WORKDIR /app

EXPOSE 5086

ENV ASPNETCORE_URLS=http://+:5086
ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "VehicleTracker.WebApi.dll"]
