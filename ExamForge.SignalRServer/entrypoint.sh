#!/usr/bin/env bash
set -e

echo "🔧 ExamForge SignalR Server - Starting..."

# Write Firestore credentials from environment variable
if [ -n "$SERVICE_ACCOUNT_JSON" ]; then
  echo "✅ Configuring Firestore credentials..."
  echo "$SERVICE_ACCOUNT_JSON" > /tmp/google-credentials.json
  export GOOGLE_APPLICATION_CREDENTIALS=/tmp/google-credentials.json
else
  echo "⚠️  No SERVICE_ACCOUNT_JSON provided"
fi

# Configure ASP.NET Core to bind to Render's PORT
export ASPNETCORE_URLS="http://*:${PORT:-8080}"

echo "🚀 Starting SignalR server on port ${PORT:-8080}"
exec dotnet ExamForge.SignalRServer.dll