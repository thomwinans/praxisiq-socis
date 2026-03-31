FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy Directory.Build.props for shared build settings (TargetFramework, etc.)
COPY Directory.Build.props ./

# Copy project files for restore
COPY src/Snapp.Shared/Snapp.Shared.csproj src/Snapp.Shared/
COPY src/Snapp.Service.Intelligence/Snapp.Service.Intelligence.csproj src/Snapp.Service.Intelligence/

# Restore
RUN dotnet restore src/Snapp.Service.Intelligence/Snapp.Service.Intelligence.csproj

# Copy source
COPY src/Snapp.Shared/ src/Snapp.Shared/
COPY src/Snapp.Service.Intelligence/ src/Snapp.Service.Intelligence/

# Build & publish
RUN dotnet publish src/Snapp.Service.Intelligence/Snapp.Service.Intelligence.csproj \
    -c Release -o /app/publish

# Runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Snapp.Service.Intelligence.dll"]
