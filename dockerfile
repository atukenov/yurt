# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and project files
COPY backend/*.sln .
COPY backend/src/Yurt.Domain/Yurt.Domain.csproj src/Yurt.Domain/
COPY backend/src/Yurt.Application/Yurt.Application.csproj src/Yurt.Application/
COPY backend/src/Yurt.Infrastructure/Yurt.Infrastructure.csproj src/Yurt.Infrastructure/
COPY backend/src/Yurt.WebApi/Yurt.WebApi.csproj src/Yurt.WebApi/

# Restore dependencies
RUN dotnet restore src/Yurt.WebApi/Yurt.WebApi.csproj

# Copy source code
COPY backend/src/Yurt.Domain src/Yurt.Domain
COPY backend/src/Yurt.Application src/Yurt.Application
COPY backend/src/Yurt.Infrastructure src/Yurt.Infrastructure
COPY backend/src/Yurt.WebApi src/Yurt.WebApi

# Build
RUN dotnet build -c Release --no-restore src/Yurt.WebApi/Yurt.WebApi.csproj

# Publish
RUN dotnet publish -c Release --no-build -o /app/publish src/Yurt.WebApi/Yurt.WebApi.csproj

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

# Install curl for health checks
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

# Copy published app
COPY --from=build /app/publish .

# Expose port
EXPOSE 8080

# Set environment
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Run the app
ENTRYPOINT ["dotnet", "Yurt.WebApi.dll"]
