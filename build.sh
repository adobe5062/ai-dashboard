#!/bin/bash
set -e

# Inject API URL from Netlify env var (empty string = app falls back to mock)
echo "{\"ApiUrl\":\"${DASHBOARD_API_URL:-}\"}" > frontend-blazor/wwwroot/appsettings.json

cd frontend-blazor
dotnet publish -c Release -o dist
