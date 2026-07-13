# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app

# Copy project files first (better caching)
COPY *.csproj ./
RUN dotnet restore

# Copy the rest
COPY . .
RUN dotnet publish API.csproj -c Release -o out

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0
ENV ASPNETCORE_HTTP_PORTS=27000
WORKDIR /app
COPY --from=build /app/out .

EXPOSE 27000
ENTRYPOINT ["dotnet", "API.dll"]
