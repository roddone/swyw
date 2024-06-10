FROM mcr.microsoft.com/dotnet/aspnet:8.0-bookworm-slim AS base
WORKDIR /app
EXPOSE 80

WORKDIR /app
COPY Swyw/ .
ENTRYPOINT ["dotnet", "Swyw.Api.dll"]
