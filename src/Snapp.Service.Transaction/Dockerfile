FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY Directory.Build.props ./
COPY src/Snapp.Shared/Snapp.Shared.csproj src/Snapp.Shared/
COPY src/Snapp.Service.Transaction/Snapp.Service.Transaction.csproj src/Snapp.Service.Transaction/

RUN dotnet restore src/Snapp.Service.Transaction/Snapp.Service.Transaction.csproj

COPY src/Snapp.Shared/ src/Snapp.Shared/
COPY src/Snapp.Service.Transaction/ src/Snapp.Service.Transaction/

RUN dotnet publish src/Snapp.Service.Transaction/Snapp.Service.Transaction.csproj \
    -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Snapp.Service.Transaction.dll"]
