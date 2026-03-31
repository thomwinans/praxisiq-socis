FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy Directory.Build.props for shared build settings (TargetFramework, etc.)
COPY Directory.Build.props ./

# Copy project files for restore
COPY src/Snapp.Shared/Snapp.Shared.csproj src/Snapp.Shared/
COPY src/Snapp.Service.DigestJob/Snapp.Service.DigestJob.csproj src/Snapp.Service.DigestJob/

# Restore
RUN dotnet restore src/Snapp.Service.DigestJob/Snapp.Service.DigestJob.csproj

# Copy source
COPY src/Snapp.Shared/ src/Snapp.Shared/
COPY src/Snapp.Service.DigestJob/ src/Snapp.Service.DigestJob/

# Build & publish
RUN dotnet publish src/Snapp.Service.DigestJob/Snapp.Service.DigestJob.csproj \
    -c Release -o /app/publish

# Runtime
FROM mcr.microsoft.com/dotnet/runtime:9.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Snapp.Service.DigestJob.dll", "--now"]
