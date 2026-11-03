#!/usr/bin/env bash
set -euo pipefail
mkdir -p test-results/browser
docker compose up -d --wait --wait-timeout 180 postgres keycloak
dotnet dev-certs https --export-path "$RUNNER_TEMP/sonofanton.pfx" --password ci-only-password
export ASPNETCORE_Kestrel__Certificates__Default__Path="$RUNNER_TEMP/sonofanton.pfx"
export ASPNETCORE_Kestrel__Certificates__Default__Password=ci-only-password
dotnet run --project src/SonOfAnton.Backend -c Release --no-build --launch-profile https >test-results/browser/backend.log 2>&1 &
backend_pid=$!
dotnet run --project src/SonOfAnton.Frontend -c Release --no-build --launch-profile https >test-results/browser/frontend.log 2>&1 &
frontend_pid=$!
trap 'kill "$backend_pid" "$frontend_pid" 2>/dev/null || true; docker compose down' EXIT
ready=false
for attempt in {1..90}; do
  if curl -ksf https://localhost:5002/health/ready >/dev/null && curl -ksf https://localhost:5001 >/dev/null && curl -sf http://localhost:8080/realms/sonofanton/.well-known/openid-configuration >/dev/null; then
    ready=true
    break
  fi
  sleep 2
done
if [ "$ready" != true ]; then echo 'Services did not become ready'; exit 1; fi
node tests/browser/core.mjs
