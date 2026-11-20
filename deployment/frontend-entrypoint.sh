#!/bin/sh
set -eu
: "${PUBLIC_OIDC_AUTHORITY:?Configure the production HTTPS OIDC issuer}"
: "${PUBLIC_OIDC_CLIENT_ID:?Configure the public PKCE client ID}"
case "$PUBLIC_OIDC_AUTHORITY" in https://*) ;; *) echo 'OIDC authority must use HTTPS' >&2; exit 1;; esac
jq -n --arg authority "$PUBLIC_OIDC_AUTHORITY" --arg client "$PUBLIC_OIDC_CLIENT_ID" \
  '{BackendUrl:"/",Oidc:{Authority:$authority,ClientId:$client,ResponseType:"code",DefaultScopes:["openid","profile","email"]}}' \
  > /usr/share/nginx/html/appsettings.json
exec nginx -g 'daemon off;'
