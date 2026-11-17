FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore first so the layer is reused until a project file changes.
COPY Directory.Build.props VehicleTracking.sln ./
COPY src/VehicleTracking.Domain/VehicleTracking.Domain.csproj src/VehicleTracking.Domain/
COPY src/VehicleTracking.Application/VehicleTracking.Application.csproj src/VehicleTracking.Application/
COPY src/VehicleTracking.Infrastructure/VehicleTracking.Infrastructure.csproj src/VehicleTracking.Infrastructure/
COPY src/VehicleTracking.Api/VehicleTracking.Api.csproj src/VehicleTracking.Api/
RUN dotnet restore src/VehicleTracking.Api/VehicleTracking.Api.csproj

COPY src/ src/
RUN dotnet publish src/VehicleTracking.Api/VehicleTracking.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
# The spool volume is mounted here; create it first so it is owned by the non-root user.
RUN mkdir -p /var/lib/vehicle-tracking/spool && chown -R $APP_UID /var/lib/vehicle-tracking
USER $APP_UID
ENTRYPOINT ["dotnet", "VehicleTracking.Api.dll"]
