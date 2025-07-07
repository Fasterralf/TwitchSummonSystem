# 🎮 TwitchSummonSystem

Ein production-ready ASP.NET Core System für Twitch-Channel-Point-Reward mit OBS-Integration, Discord-Benachrichtigungen und modernem Admin-Dashboard.

## ⚠️ **WICHTIG: Sichere Konfiguration!**

Dieses System nutzt **Environment Variables** für alle API-Keys und Secrets. Alle sensiblen Daten bleiben bei dir und werden **niemals in Git committet**. 🔐

✅ **Systemd Service ist sicher** - Keine Geheimnisse im Service-File  
✅ **Geschützte .env Datei** - Nur für Server Owner lesbar  
✅ **Automatisches Token Management** - Refresh-Logik integriert

## 🚀 Features

- **🎁 Twitch Integration**: Automatische Channel Point Reward Behandlung
- **📺 OBS Browser Source**: Schöne Summon-Animationen mit Gold/Normal Effekten  
- **📱 Discord Benachrichtigungen**: Auto-Meldungen bei Gold-Summons und System-Fehlern
- **🎯 Pity System**: Erhöhte Gold-Chancen nach normalen Summons
- **⚡ Real-time Updates**: SignalR für Live-Browser-Updates
- **🔄 Smart Token Management**: Automatische Twitch-Token Erneuerung
- **🛡️ Rate Limiting & Security**: Schutz vor API-Missbrauch
- **❤️ Health Monitoring**: System-Überwachung mit Status-Checks
- **💾 Auto-Backup System**: Automatische Lottery-Daten Sicherung
- **🖥️ Modern Admin Dashboard**: Schönes Web-Interface zur Systemkontrolle
- **🔧 Systemd Service**: Stabiler Linux-Server-Betrieb mit Auto-Restart

## 📋 Voraussetzungen

- **.NET 8.0 SDK** (oder .NET 7.0+)
- **Twitch Developer Account** ([dev.twitch.tv](https://dev.twitch.tv))
- **Discord Webhook** (optional, aber empfohlen)
- **Linux Server** für Production (Ubuntu/Debian empfohlen)

## 🛠️ Quick Start

### **1. Repository klonen**
```bash
git clone https://github.com/Fasterralf/TwitchSummonSystem.git
cd TwitchSummonSystem/TwitchSummonSystem
```

### **2. Dependencies installieren**
```bash
dotnet restore
```

### **3. Environment Variables einrichten**
```bash
cp .env.example .env
# Bearbeite .env mit deinen echten Twitch/Discord Werten
```

### **4. Lokal testen**
```bash
dotnet run
# App läuft auf http://localhost:5173
# Admin Panel: http://localhost:5173/admin.html
```

### **5. Production Deployment**
Siehe **`SERVER_SETUP.md`** für detaillierte Server-Installation mit sicherer Konfiguration.

## 🔧 Konfiguration

### Twitch Developer Setup
1. **App erstellen**: [dev.twitch.tv/console/apps](https://dev.twitch.tv/console/apps)
2. **Client ID & Secret** notieren
3. **Access Tokens** generieren ([Twitch Auth Guide](https://dev.twitch.tv/docs/authentication))
4. **Channel Point Reward** erstellen (exakten Namen notieren!)

### Discord Webhooks (optional)
1. Discord Server → Integrations → Webhooks
2. Erstelle 2 Webhooks: Normal & Error
3. URLs in `.env` eintragen

## 🌐 Webhook Setup (Twitch EventSub)

Für Channel Point Rewards benötigst du eine **öffentlich erreichbare Webhook-URL**:

### **Option 1: ngrok (Testing)**
```bash
ngrok http 5173
./setup-webhook.sh  # In anderem Terminal
```

### **Option 2: Server mit öffentlicher IP**
```bash
sudo ufw allow 5173
# Webhook URL: http://DEINE_SERVER_IP:5173/api/twitch/webhook
```

### **Option 3: Domain mit Nginx (Production)**
```bash
# Nginx Reverse Proxy einrichten
# Webhook URL: https://deine-domain.com/api/twitch/webhook
```

⚠️ **Ohne öffentliche Webhook-URL funktionieren Channel Point Rewards nicht!**

## � System URLs

- **🏠 Main App**: `http://localhost:5173/`
- **⚙️ Admin Dashboard**: `http://localhost:5173/admin.html`
- **📺 OBS Browser Source**: `http://localhost:5173/obs.html`  
- **❤️ Health Check**: `http://localhost:5173/health`
- **� API Docs**: `http://localhost:5173/swagger` (Development)

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

## 🔒 Sicherheit & Best Practices

- ✅ **Keine Credentials im Code** - Alles über Environment Variables
- ✅ **Sichere Systemd-Konfiguration** - Secrets in geschützter `.env` Datei
- ✅ **Rate Limiting** aktiviert (falls .NET 8+)
- ✅ **Global Exception Handling** mit Discord-Benachrichtigungen
- ✅ **Automatic Backups** alle 30 Minuten
- ✅ **Health Monitoring** mit `/health` Endpoint
- ✅ **Production-ready Logging** mit strukturiertem Format

## 📈 Monitoring & Admin

- **🖥️ Admin Dashboard**: `/admin.html` - Modernes Web-Interface
- **❤️ Health Checks**: `/health` - System-Status API
- **📊 Real-time Stats**: Lottery-Statistiken und Token-Status
- **🔄 Auto-Reconnect**: Intelligente Chat-Bot Wiederverbindung
- **📱 Discord Alerts**: Automatische Fehler-Benachrichtigungen
- **📈 Systemd Integration**: Server-Monitoring mit `journalctl`

## 🤝 Contributing

1. Fork das Repository
2. Erstelle einen Feature Branch (`git checkout -b feature/AmazingFeature`)
3. Committe deine Änderungen (`git commit -m 'Add some AmazingFeature'`)
4. Pushe den Branch (`git push origin feature/AmazingFeature`)
5. Öffne einen Pull Request

## 📄 Lizenz

Distributed under the MIT License. See `LICENSE` for more information.

## 🆘 Troubleshooting & FAQ

**❓ "Summon Reward nicht gefunden"**
- Prüfe, ob der Reward-Name in `.env` exakt mit dem Twitch Reward übereinstimmt
- Verwende das Admin Dashboard zur Diagnose

**❓ "Chat Bot verbindet nicht"**  
- Prüfe Chat OAuth Token Berechtigung (`chat:read`, `chat:edit`)
- Admin Panel → Chat Tab für detaillierte Status-Info

**❓ "Discord Benachrichtigungen funktionieren nicht"**
- Teste Webhook: `curl -X POST WEBHOOK_URL -d '{"content":"Test"}'`
- Prüfe URLs in `.env` Konfiguration

**❓ "Tokens sind abgelaufen"**
- Admin Dashboard zeigt Token-Status und Ablaufzeiten
- System erneuert Tokens automatisch (falls Refresh-Token gültig)

**❓ Weitere Probleme?**
- Öffne ein GitHub Issue mit detaillierten Logs
- Verwende `./monitor.sh` für System-Diagnose
- Logs: `sudo journalctl -u twitch-summon -f`

---

**🎮 Viel Spaß mit deinem Twitch Summon System!** 

Made with ❤️ for the Twitch Community by [Fasterralf](https://github.com/Fasterralf)
