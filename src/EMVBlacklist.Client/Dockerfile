FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files
COPY ["src/EMVBlacklist.Client/EMVBlacklist.Client.csproj", "src/EMVBlacklist.Client/"]
COPY ["src/EMVBlacklist.Shared/EMVBlacklist.Shared.csproj", "src/EMVBlacklist.Shared/"]

# Restore dependencies
RUN dotnet restore "src/EMVBlacklist.Client/EMVBlacklist.Client.csproj"

# Copy source code
COPY . .

# Build
WORKDIR "/src/src/EMVBlacklist.Client"
RUN dotnet build "EMVBlacklist.Client.csproj" -c Release -o /app/build

# Publish
FROM build AS publish
RUN dotnet publish "EMVBlacklist.Client.csproj" -c Release -o /app/publish

# Final stage
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "EMVBlacklist.Client.dll"]
