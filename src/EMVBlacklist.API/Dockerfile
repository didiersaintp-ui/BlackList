FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files
COPY ["src/EMVBlacklist.API/EMVBlacklist.API.csproj", "src/EMVBlacklist.API/"]
COPY ["src/EMVBlacklist.Shared/EMVBlacklist.Shared.csproj", "src/EMVBlacklist.Shared/"]

# Restore dependencies
RUN dotnet restore "src/EMVBlacklist.API/EMVBlacklist.API.csproj"

# Copy source code
COPY . .

# Build
WORKDIR "/src/src/EMVBlacklist.API"
RUN dotnet build "EMVBlacklist.API.csproj" -c Release -o /app/build

# Publish
FROM build AS publish
RUN dotnet publish "EMVBlacklist.API.csproj" -c Release -o /app/publish

# Final stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "EMVBlacklist.API.dll"]
