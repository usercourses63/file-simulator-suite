# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy csproj files and restore (layer caching)
COPY ["FileSimulator.ControlApi/FileSimulator.ControlApi.csproj", "FileSimulator.ControlApi/"]
COPY ["FileSimulator.Client/FileSimulator.Client.csproj", "FileSimulator.Client/"]
RUN dotnet restore "FileSimulator.ControlApi/FileSimulator.ControlApi.csproj"

# Copy source and build
COPY . .
WORKDIR "/src/FileSimulator.ControlApi"
RUN dotnet publish -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Install curl for health checks
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

# Create non-root user for security
RUN addgroup --gid 1000 appgroup && \
    adduser --uid 1000 --gid 1000 --disabled-password --gecos "" appuser

# Copy published app
COPY --from=build /app/publish .

# Set ownership and switch to non-root user
RUN chown -R appuser:appgroup /app
USER appuser

# Expose port (Kestrel default)
EXPOSE 5000

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:5000/health || exit 1

# Entry point
ENTRYPOINT ["dotnet", "FileSimulator.ControlApi.dll"]
