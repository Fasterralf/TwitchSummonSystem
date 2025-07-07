# 🚀 Server Setup & Deployment Guide

## 🔐 WICHTIG: Sichere Installation

Die `twitch-summon.service` Datei ist jetzt sicher und enthält **KEINE Geheimnisse** mehr!
Alle sensiblen Daten werden über eine geschützte `.env` Datei geladen.

## 📋 Erste Installation auf dem Server:

### 1. Projekt klonen
```bash
git clone https://github.com/Fasterralf/TwitchSummonSystem.git
cd TwitchSummonSystem/TwitchSummonSystem
```

### 2. 🔐 Sichere .env Datei erstellen
```bash
# Erstelle sichere .env Datei in HOME Verzeichnis
cp .env.production ~/.env

# WICHTIG: Setze sichere Berechtigung (nur Owner kann lesen)
chmod 600 ~/.env

# Bearbeite die Datei mit deinen aktuellen Tokens
nano ~/.env
```

### 3. Systemd Service installieren
```bash
# Service-Datei anpassen (USERNAME ersetzen)
sudo cp twitch-summon.service /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable twitch-summon
```

### 4. Erstes Deployment
```bash
chmod +x deploy.sh monitor.sh
./deploy.sh
```

## 🔄 Bei Updates - Code aktualisieren:

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

## 🛡️ Sicherheitsvorteile:

1. **Keine Geheimnisse in Service-Datei** - Systemd Service ist sauber
2. **Geschützte .env Datei** - Nur Owner kann Tokens lesen (`chmod 600`)
3. **Nicht in Git** - `.env.production` wird niemals committed
4. **Einfache Updates** - Service-Datei kann öffentlich geteilt werden

## ✅ Token Aktualisierung:

Wenn du Tokens erneuern musst:
```bash
# Nur .env Datei bearbeiten
nano ~/.env

# Service neu starten
sudo systemctl restart twitch-summon
```

**NIEMALS** Tokens direkt in die Service-Datei schreiben!

## 🔧 Debugging:

```bash
# Service Logs anzeigen
sudo journalctl -u twitch-summon -f

# Service Status prüfen
sudo systemctl status twitch-summon

# Service neu starten
sudo systemctl restart twitch-summon
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
