#!/bin/bash
echo "🧹 Lösche alle alten Webhooks..."

# Hole App Access Token
CLIENT_ID="wwsw64nopy5scsjfn0ab4vbgvo0jd2"
CLIENT_SECRET="wiw93k8jgxu8di9d56rntxck2jigmv"

TOKEN_RESPONSE=$(curl -s -X POST "https://id.twitch.tv/oauth2/token" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "client_id=$CLIENT_ID&client_secret=$CLIENT_SECRET&grant_type=client_credentials")

ACCESS_TOKEN=$(echo $TOKEN_RESPONSE | grep -o '"access_token":"[^"]*' | cut -d'"' -f4)

if [ -z "$ACCESS_TOKEN" ]; then
    echo "❌ Konnte kein Access Token erhalten"
    exit 1
fi

echo "✅ Access Token erhalten"

# Hole alle Subscriptions
SUBS_RESPONSE=$(curl -s -X GET "https://api.twitch.tv/helix/eventsub/subscriptions" \
  -H "Client-ID: $CLIENT_ID" \
  -H "Authorization: Bearer $ACCESS_TOKEN")

echo "📋 Aktuelle Subscriptions:"
echo $SUBS_RESPONSE

# Lösche alle Subscriptions
SUBSCRIPTION_IDS=$(echo $SUBS_RESPONSE | grep -o '"id":"[^"]*' | cut -d'"' -f4)

for SUB_ID in $SUBSCRIPTION_IDS; do
    echo "🗑️ Lösche Subscription: $SUB_ID"
    curl -s -X DELETE "https://api.twitch.tv/helix/eventsub/subscriptions?id=$SUB_ID" \
      -H "Client-ID: $CLIENT_ID" \
      -H "Authorization: Bearer $ACCESS_TOKEN"
done

echo "✅ Cleanup abgeschlossen"
