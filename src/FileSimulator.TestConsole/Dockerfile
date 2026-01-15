FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build
WORKDIR /src

# Copy csproj files and restore
COPY FileSimulator.Client/FileSimulator.Client.csproj FileSimulator.Client/
COPY FileSimulator.TestConsole/FileSimulator.TestConsole.csproj FileSimulator.TestConsole/
RUN dotnet restore FileSimulator.TestConsole/FileSimulator.TestConsole.csproj

# Copy source and build
COPY FileSimulator.Client/ FileSimulator.Client/
COPY FileSimulator.TestConsole/ FileSimulator.TestConsole/
RUN dotnet publish FileSimulator.TestConsole/FileSimulator.TestConsole.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/runtime:9.0-alpine AS runtime
WORKDIR /app

# Install NFS client tools for NFS testing
RUN apk add --no-cache nfs-utils

COPY --from=build /app .

# Default to running cross-protocol tests
ENTRYPOINT ["dotnet", "FileSimulator.TestConsole.dll"]
CMD ["--cross-protocol"]
