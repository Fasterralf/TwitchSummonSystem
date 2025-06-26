# 🎮 TwitchSummonSystem

Ein production-ready ASP.NET Core System für Twitch-Channel-Point-Rewards mit OBS-Integration und Discord-Benachrichtigungen.

## ⚠️ **WICHTIG: Eigene Credentials erforderlich!**

Dieses Projekt verwendet **Environment Variables** für alle API-Keys und Secrets. Du musst deine **eigenen Twitch/Discord Credentials** einrichten! 

**Keine Sorge** - alle sensiblen Daten bleiben bei dir und werden nie in Git committet. �

## �🚀 Features

- **🎁 Twitch Integration**: Automatische Behandlung von Channel Point Rewards
- **📺 OBS Browser Source**: Schöne Animationen für Gold/Normal Summons  
- **📱 Discord Benachrichtigungen**: Automatische Meldungen bei Gold-Summons und Fehlern
- **🎯 Pity System**: Erhöhte Chancen nach mehreren normalen Summons
- **⚡ Real-time Updates**: SignalR für Live-Updates im Browser
- **🔄 Token Management**: Automatische Refresh-Logik für Twitch-Tokens
- **🛡️ Rate Limiting**: Schutz vor API-Missbrauch
- **❤️ Health Checks**: Monitoring der System-Gesundheit
- **💾 Backup System**: Automatische Backups der Lottery-Daten
- **🔧 Systemd Service**: Stabiler Server-Betrieb mit Auto-Restart

## 📋 Voraussetzungen

- **.NET 8.0 SDK** (oder .NET 7.0+)
- **Twitch Developer Account** ([dev.twitch.tv](https://dev.twitch.tv))
- **Discord Webhook** (optional, aber empfohlen)
- **Linux Server** für Production (Ubuntu/Debian empfohlen)

## 🛠️ Installation & Setup

### **1. Repository klonen**
```bash
git clone https://github.com/DEIN_USERNAME/TwitchSummonSystem.git
cd TwitchSummonSystem
```

### **2. Twitch Developer App erstellen**
1. Gehe zu [dev.twitch.tv/console/apps](https://dev.twitch.tv/console/apps)
2. Erstelle eine neue App
3. Notiere dir **Client ID** und **Client Secret**
4. Hole dir **Access Tokens** (siehe [Twitch Auth Guide](https://dev.twitch.tv/docs/authentication))

### **3. Channel Point Reward erstellen**
1. Gehe zu deinem Twitch Creator Dashboard
2. Einstellungen → Community → Channel Point Rewards
3. Erstelle ein neues Custom Reward (z.B. "Summon")
4. Notiere dir den **exakten Namen**

### **4. Discord Webhooks erstellen** (optional)
1. Discord Server → Server Settings → Integrations → Webhooks
2. Erstelle 2 Webhooks: einen für normale Meldungen, einen für Fehler
3. Kopiere die Webhook-URLs

### **5. Environment Variables einrichten**

**Für Development (.env Datei):**
```bash
cp .env.example .env
# Bearbeite .env mit deinen echten Werten
```

**Für Production (Systemd Service):**
Siehe `SERVER_SETUP.md` für detaillierte Anweisungen.

### **6. Dependencies installieren & starten**
```bash
dotnet restore
dotnet run
```

## 🌐 Webhook Setup (für Twitch EventSub)

Twitch Channel Point Rewards benötigen **öffentlich erreichbare Webhooks**. Du hast mehrere Optionen:

### **Option 1: ngrok (Einfach für Testing)**
```bash
# ngrok installieren und starten
ngrok http 5000

# In anderem Terminal:
./setup-webhook.sh
```

### **Option 2: Öffentliche Server-IP**
```bash
# Port 5000 öffnen
sudo ufw allow 5000

# Webhook manuell in Twitch konfigurieren:
# http://DEINE_SERVER_IP:5000/api/twitch/webhook
```

### **Option 3: Domain mit Nginx (Production)**
```bash
# Nginx Reverse Proxy einrichten
# Webhook URL: https://deine-domain.com/api/twitch/webhook
```

⚠️ **Wichtig**: Ohne öffentliche Webhook-URL funktionieren Channel Point Rewards nicht!

## 🔧 Konfiguration

Kopiere `.env.example` zu `.env` und fülle alle Werte aus:

```bash
# Twitch Configuration  
TWITCH_CLIENT_ID=deine_client_id_hier
TWITCH_CLIENT_SECRET=dein_client_secret_hier
TWITCH_ACCESS_TOKEN=dein_access_token_hier
TWITCH_REFRESH_TOKEN=dein_refresh_token_hier
TWITCH_CHANNEL_ID=deine_channel_id_hier
TWITCH_CHANNEL_NAME=dein_channel_name_hier
TWITCH_SUMMON_REWARD_NAME=dein_reward_name_hier

# Bot Configuration
TWITCH_BOT_USERNAME=dein_bot_username_hier
TWITCH_BOT_CLIENT_ID=deine_bot_client_id_hier
TWITCH_BOT_CLIENT_SECRET=dein_bot_client_secret_hier
TWITCH_CHAT_OAUTH_TOKEN=dein_chat_oauth_token_hier
TWITCH_CHAT_REFRESH_TOKEN=dein_chat_refresh_token_hier

# Discord Webhooks (optional)
DISCORD_WEBHOOK_URL=deine_discord_webhook_url_hier
DISCORD_ERROR_WEBHOOK_URL=deine_discord_error_webhook_url_hier
```

## 🚀 Production Deployment

**Vollständige Anleitung:** Siehe `SERVER_SETUP.md`

**Quick Start:**
```bash
# Auf dem Server:
./deploy.sh

# Monitoring:
./monitor.sh

# Logs anschauen:
sudo journalctl -u twitch-summon -f
```

## 📊 API Endpoints

- `GET /health` - System Health Check
- `GET /api/summon/stats` - Lottery Statistiken  
- `POST /api/summon/perform` - Manueller Summon
- `POST /api/summon/pity/reset` - Pity Counter zurücksetzen
- `GET /api/config/status` - Konfigurationsstatus

## 🖥️ OBS Integration

**Browser Source hinzufügen:**
```
URL: http://localhost:5173/obs.html
Breite: 1920px
Höhe: 1080px  
FPS: 30
```

## 🐳 Docker Support

```bash
docker build -t twitch-summon-system .
docker run -p 5173:8080 --env-file .env twitch-summon-system
```

## 📁 Projektstruktur

```
├── Controllers/          # API Controller
├── Services/            # Business Logic  
├── Hubs/               # SignalR Hubs
├── Models/             # Data Models
├── Middleware/         # Custom Middleware
├── Configuration/      # Konfigurationsklassen
├── wwwroot/           # Static Files (OBS Frontend)
├── Pages/             # Razor Pages
├── deploy.sh          # Deployment Script
├── monitor.sh         # Monitoring Script
└── twitch-summon.service # Systemd Service
```

## 🔒 Sicherheit

- ✅ **Keine Credentials in Code** - Alles über Environment Variables
- ✅ **Rate Limiting** aktiviert (falls .NET 8+)
- ✅ **Global Exception Handling** 
- ✅ **Automatic Backups** alle 30 Minuten
- ✅ **Discord Error Notifications**

## 📈 Monitoring

- **Health Checks** unter `/health`
- **Admin Panel** unter `/admin.html`
- **Automatische Discord-Benachrichtigungen** bei Fehlern
- **Strukturiertes Logging** mit Emojis
- **Systemd Integration** für Server-Monitoring

## 🤝 Contributing

1. Fork das Repository
2. Erstelle einen Feature Branch (`git checkout -b feature/AmazingFeature`)
3. Committe deine Änderungen (`git commit -m 'Add some AmazingFeature'`)
4. Pushe den Branch (`git push origin feature/AmazingFeature`)
5. Öffne einen Pull Request

## 📄 Lizenz

Distributed under the MIT License. See `LICENSE` for more information.

## 🆘 Support & FAQ

**❓ "Summon Reward nicht gefunden"**
- Prüfe, ob der Reward-Name in der Konfiguration exakt mit dem Twitch Reward übereinstimmt

**❓ "Chat Bot verbindet nicht"**  
- Prüfe Chat OAuth Token Berechtigung (chat:read, chat:edit)
- Verwende das Admin Panel für Diagnose

**❓ "Discord Benachrichtigungen funktionieren nicht"**
- Prüfe Webhook URLs in der Konfiguration
- Teste mit `curl -X POST WEBHOOK_URL -d '{"content":"Test"}'`

**❓ Bei weiteren Problemen:**
- Öffne ein Issue mit detaillierten Logs
- Verwende `./monitor.sh` für System-Diagnose

---

**🎮 Viel Spaß mit deinem Twitch Summon System!** 

Erstellt mit ❤️ für die Twitch Community
