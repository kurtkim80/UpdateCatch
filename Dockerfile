# Multi-stage Dockerfile for UpdateCatch Web
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app

# Copy csproj and restore
COPY Directory.Build.props ./
COPY src/UpdateCatch.Core/UpdateCatch.Core.csproj src/UpdateCatch.Core/
COPY src/UpdateCatch.Web/UpdateCatch.Web.csproj src/UpdateCatch.Web/
RUN dotnet restore src/UpdateCatch.Web/UpdateCatch.Web.csproj

# Copy full source and publish
COPY targets.json ./
COPY data/ ./data/
COPY src/UpdateCatch.Core/ src/UpdateCatch.Core/
COPY src/UpdateCatch.Web/ src/UpdateCatch.Web/
RUN dotnet publish src/UpdateCatch.Web/UpdateCatch.Web.csproj -c Release -o /out

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /out ./
COPY --from=build /app/targets.json ./targets.json
COPY --from=build /app/data ./data

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "UpdateCatch.Web.dll"]
