# 🚀 Quick Server Setup Guide

## Auf dem Contabo Server ausführen:

### 1. Nach Git Push - Code aktualisieren:
```bash
cd ~/TwitchSummonSystem/TwitchSummonSystem
git pull
```

### 2. Deployment (automatisch):
```bash
chmod +x deploy.sh monitor.sh
./deploy.sh
```

### 3. Monitoring:
```bash
./monitor.sh
```

### 4. Logs überwachen:
```bash
sudo journalctl -u twitch-summon -f
```

## 🔧 Troubleshooting:

### Service neu starten:
```bash
sudo systemctl restart twitch-summon
```

### Service Status prüfen:
```bash
sudo systemctl status twitch-summon
```

### Konfiguration prüfen:
```bash
curl http://localhost:5000/health
```

### Fehler-Logs anzeigen:
```bash
sudo journalctl -u twitch-summon --since "1 hour ago" | grep ERROR
```

## 🌐 URLs (nach Deployment):

- **Health Check**: `http://YOUR_SERVER_IP:5000/health`
- **OBS Browser Source**: `http://YOUR_SERVER_IP:5000/obs.html`  
- **API Stats**: `http://YOUR_SERVER_IP:5000/api/summon/stats`

## ⚠️ Wichtige Hinweise:

1. **Firewall**: Port 5000 muss geöffnet sein
2. **Nginx**: Für HTTPS einen Reverse Proxy einrichten
3. **Backups**: Automatische Backups laufen alle 30 Min
4. **Monitoring**: Discord-Benachrichtigungen bei Fehlern
