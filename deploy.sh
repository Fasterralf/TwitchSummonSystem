#!/bin/bash

echo "🚀 Deploying Twitch Summon System..."

# Service stoppen
echo "⏹️ Stopping service..."
sudo systemctl stop twitch-summon || true

# Git pull
echo "📥 Pulling latest code..."
git pull origin main

# Dependencies aktualisieren
echo "📦 Restoring packages..."
dotnet restore

# Build
echo "🔨 Building application..."
dotnet publish -c Release -o ./publish

# Service-Datei kopieren (nur beim ersten Mal nötig)
if [ ! -f "/etc/systemd/system/twitch-summon.service" ]; then
    echo "📄 Installing systemd service..."
    # USERNAME automatisch ersetzen
    sed "s/\[USERNAME\]/$USER/g" twitch-summon.service > /tmp/twitch-summon.service
    sudo cp /tmp/twitch-summon.service /etc/systemd/system/
    sudo systemctl daemon-reload
    sudo systemctl enable twitch-summon
    echo "✅ Service installed and enabled"
fi

# Service starten
echo "▶️ Starting service..."
sudo systemctl start twitch-summon

# Status prüfen
sleep 3
echo "📊 Service status:"
sudo systemctl status twitch-summon --no-pager --lines=10

echo ""
echo "✅ Deployment completed!"
echo "🔗 Health Check: curl http://localhost:5000/health"
echo "📊 View logs: sudo journalctl -u twitch-summon -f"
