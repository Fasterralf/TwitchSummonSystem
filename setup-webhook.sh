#!/bin/bash
echo "🔍 Suche ngrok URL..."

# Warte bis ngrok bereit ist
sleep 5

# Hole ngrok URL
NGROK_URL=$(curl -s http://localhost:4040/api/tunnels | grep -o 'https://[^"]*\.ngrok-free\.app')

if [ -z "$NGROK_URL" ]; then
    echo "❌ Keine ngrok URL gefunden!"
    exit 1
fi

echo "✅ ngrok URL gefunden: $NGROK_URL"

# Setup Webhook
WEBHOOK_URL="$NGROK_URL/api/twitch/webhook"
echo "🔗 Richte Webhook ein: $WEBHOOK_URL"

curl -X POST http://localhost:5000/api/twitch/setup-webhook \
  -H "Content-Type: application/json" \
  -d "{\"callbackUrl\":\"$WEBHOOK_URL\"}"

echo ""
echo "✅ Webhook Setup abgeschlossen!"
echo "📺 OBS URL: http://$(curl -s ifconfig.me):5000/obs.html"
echo "🎮 Admin URL: http://$(curl -s ifconfig.me):5000/admin.html"
