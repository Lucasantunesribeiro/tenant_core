FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY src/TenantCore.Api/TenantCore.Api.csproj src/TenantCore.Api/
COPY src/TenantCore.Application/TenantCore.Application.csproj src/TenantCore.Application/
COPY src/TenantCore.Domain/TenantCore.Domain.csproj src/TenantCore.Domain/
COPY src/TenantCore.Infrastructure/TenantCore.Infrastructure.csproj src/TenantCore.Infrastructure/

RUN dotnet restore src/TenantCore.Api/TenantCore.Api.csproj

COPY src ./src

RUN dotnet publish src/TenantCore.Api/TenantCore.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "TenantCore.Api.dll"]
