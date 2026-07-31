#!/bin/sh
# Regenerates wwwroot/appsettings.json from env vars at container start.
# Mirrors what build.sh does for the Netlify build (injecting DASHBOARD_API_URL
# at publish time) — but done at runtime here so the same image works for any
# deployment without rebuilding, and no API URL is ever baked into the image.
#
# Leaving API_URL unset/empty makes the app fall back to mock data,
# same as the public Netlify demo.
set -e

cat > /usr/share/nginx/html/appsettings.json <<EOF
{"ApiUrl":"${API_URL:-}","DisplayName":"${DISPLAY_NAME:-}"}
EOF
