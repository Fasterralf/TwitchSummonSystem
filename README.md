# 🎮 TwitchSummonSystem

Ein ASP.NET Core System für Twitch-Channel-Point-Rewards mit OBS-Integration und Discord-Benachrichtigungen.

## 🚀 Features

- **Twitch Integration**: Automatische Behandlung von Channel Point Rewards
- **OBS Browser Source**: Schöne Animationen für Gold/Normal Summons
- **Discord Benachrichtigungen**: Automatische Meldungen bei Gold-Summons und Fehlern
- **Pity System**: Erhöhte Chancen nach mehreren normalen Summons
- **Real-time Updates**: SignalR für Live-Updates im Browser
- **Token Management**: Automatische Refresh-Logik für Twitch-Tokens
- **Rate Limiting**: Schutz vor API-Missbrauch
- **Health Checks**: Monitoring der System-Gesundheit
- **Backup System**: Automatische Backups der Lottery-Daten

## 📋 Voraussetzungen

- .NET 8.0 SDK
- Twitch Developer Account
- Discord Webhook (optional)

## 🛠️ Installation

1. **Repository klonen**
   ```bash
   git clone <repository-url>
   cd TwitchSummonSystem
   ```

2. **Konfiguration erstellen**
   - Kopiere `.env.example` zu `.env`
   - Fülle alle Twitch- und Discord-Credentials aus

3. **Dependencies installieren**
   ```bash
   dotnet restore
   ```

4. **Starten**
   ```bash
   dotnet run
   ```

## 🔧 Konfiguration

### Twitch Setup
1. Erstelle eine App auf [dev.twitch.tv](https://dev.twitch.tv/console/apps)
2. Hole dir Client ID und Secret
3. Erstelle einen Channel Point Reward in deinem Twitch-Dashboard
4. Trage alle Werte in die `.env` Datei ein

### Discord (Optional)
1. Erstelle Webhooks in deinem Discord-Server
2. Trage die URLs in die `.env` Datei ein

## 📊 API Endpoints

- `GET /health` - System Health Check
- `GET /api/summon/stats` - Lottery Statistiken
- `POST /api/summon/perform` - Manueller Summon
- `POST /api/summon/pity/reset` - Pity Counter zurücksetzen

## 🖥️ OBS Integration

Füge als Browser Source hinzu:
```
http://localhost:5173/obs.html
```

**Empfohlene Einstellungen:**
- Breite: 1920px
- Höhe: 1080px
- FPS: 30

## 🐳 Docker

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
└── Pages/             # Razor Pages
```

## 🔒 Sicherheit

- **Nie Credentials in Code committen**
- Verwende `.env` Dateien für sensible Daten
- Rate Limiting ist aktiviert
- Global Exception Handling für bessere Fehlerbehandlung

## 📈 Monitoring

- Health Checks unter `/health`
- Automatische Discord-Benachrichtigungen bei Fehlern
- Strukturiertes Logging mit Emojis
- Automatische Backups alle 30 Minuten

## 🤝 Contributing

1. Fork das Repository
2. Erstelle einen Feature Branch
3. Committe deine Änderungen
4. Pushe den Branch
5. Öffne einen Pull Request

## 📄 Lizenz

[Lizenz hier einfügen]

## 🆘 Support

Bei Problemen oder Fragen öffne ein Issue im Repository.
