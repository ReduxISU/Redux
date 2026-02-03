# Build stage
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /app

# Copy project files first (better caching)
COPY *.csproj ./
RUN dotnet restore

# Copy the rest
COPY . .
RUN dotnet publish -c Release -o out

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:6.0
WORKDIR /app
COPY --from=build /app/out .

EXPOSE 27000
ENTRYPOINT ["dotnet", "API.dll"]
