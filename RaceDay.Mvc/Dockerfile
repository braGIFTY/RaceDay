# ---- Build stage: restore packages and publish the app ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy just the project file first, so the restore step is cached until packages change
COPY RaceDay.Mvc.csproj ./
RUN dotnet restore

COPY . ./
RUN dotnet publish -c Release -o /app/publish --no-restore

# ---- Runtime stage: a small image holding only what is needed to run ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish ./

# The site listens on 8080 inside the container, and never runs as root
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
USER app

ENTRYPOINT ["dotnet", "RaceDay.Mvc.dll"]
