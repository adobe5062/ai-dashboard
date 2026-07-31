# Build context is the REPO ROOT (not frontend-blazor/), because
# Dashboard.Frontend.csproj references ../backend/src/Dashboard.Shared.
# See docker-compose.yml: context: .., dockerfile: frontend-blazor/Dockerfile

# Stage 1 — build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY backend/src/Dashboard.Shared/ backend/src/Dashboard.Shared/
COPY frontend-blazor/ frontend-blazor/
WORKDIR /src/frontend-blazor
RUN dotnet publish -c Release -o /app/publish

# Stage 2 — serve static output with nginx
FROM nginx:alpine
COPY --from=build /app/publish/wwwroot /usr/share/nginx/html
COPY frontend-blazor/nginx.conf /etc/nginx/nginx.conf

# Runs automatically before nginx starts (built into the nginx:alpine image).
# Regenerates appsettings.json from env vars on every container start, so the
# API URL is never baked into the image itself.
COPY frontend-blazor/docker-entrypoint.d/40-inject-config.sh /docker-entrypoint.d/40-inject-config.sh
RUN chmod +x /docker-entrypoint.d/40-inject-config.sh

EXPOSE 80
