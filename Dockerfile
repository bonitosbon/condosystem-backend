# Use the official .NET SDK image to build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project file and restore dependencies
COPY ["CondoSystem_Backend.csproj", "./"]
RUN dotnet restore "CondoSystem_Backend.csproj"

# Copy everything else and build
COPY . .
RUN dotnet build "CondoSystem_Backend.csproj" -c Release -o /app/build

# Publish
FROM build AS publish
RUN dotnet publish "CondoSystem_Backend.csproj" -c Release -o /app/publish

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Railway sets PORT environment variable
# Expose default port (Railway will map to this)
EXPOSE 8080

ENTRYPOINT ["dotnet", "CondoSystem_Backend.dll"]

