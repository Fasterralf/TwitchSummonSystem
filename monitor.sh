#!/bin/bash

echo "🔍 Twitch Summon System - Monitoring Dashboard"
echo "=============================================="
echo ""

# Service Status
echo "📊 Service Status:"
systemctl is-active --quiet twitch-summon && echo "✅ Service: RUNNING" || echo "❌ Service: STOPPED"
echo ""

# Health Check
echo "❤️ Health Check:"
if curl -s http://localhost:5000/health > /dev/null 2>&1; then
    echo "✅ Health Check: HEALTHY"
    curl -s http://localhost:5000/health | jq . 2>/dev/null || echo "Response OK"
else
    echo "❌ Health Check: UNHEALTHY"
fi
echo ""

# Memory & CPU
echo "💻 Resource Usage:"
ps aux | grep -E "(dotnet|TwitchSummonSystem)" | grep -v grep | head -5
echo ""

# Recent Logs
echo "📋 Recent Logs (last 10 lines):"
sudo journalctl -u twitch-summon --since "5 minutes ago" --no-pager -n 10
echo ""

# Uptime
echo "⏰ Uptime:"
sudo systemctl show twitch-summon --property=ActiveEnterTimestamp | cut -d= -f2
echo ""

echo "🔧 Quick Commands:"
echo "  Restart: sudo systemctl restart twitch-summon"
echo "  Logs:    sudo journalctl -u twitch-summon -f"
echo "  Stop:    sudo systemctl stop twitch-summon"
echo "  Start:   sudo systemctl start twitch-summon"
