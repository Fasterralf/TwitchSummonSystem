# 🔐 SICHERE SERVER INSTALLATION

## ⚠️ WICHTIG: Systemd Service Sicherheit

Die `twitch-summon.service` Datei ist jetzt sicher und enthält KEINE Geheimnisse mehr!

## 📋 Server Setup Schritte:

### 1. Projekt auf Server kopieren
```bash
# Projekt klonen/kopieren
git clone https://github.com/Fasterralf/TwitchSummonSystem.git
cd TwitchSummonSystem/TwitchSummonSystem
```

### 2. Sichere .env Datei erstellen
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

### 4. Service starten
```bash
sudo systemctl start twitch-summon
sudo systemctl status twitch-summon
```

## 🛡️ Sicherheitsvorteile:

1. **Keine Geheimnisse in Service-Datei** - Systemd Service ist sauber
2. **Geschützte .env Datei** - Nur Owner kann Tokens lesen
3. **Nicht in Git** - .env.production wird niemals committed
4. **Einfache Updates** - Service-Datei kann öffentlich geteilt werden

## 🔧 Debugging:

```bash
# Service Logs anzeigen
sudo journalctl -u twitch-summon -f

# Service Status prüfen
sudo systemctl status twitch-summon

# Service neu starten
sudo systemctl restart twitch-summon
```

## ✅ Token Aktualisierung:

Wenn du Tokens erneuern musst:
```bash
# Nur .env Datei bearbeiten
nano ~/.env

# Service neu starten
sudo systemctl restart twitch-summon
```

**NIEMALS** Tokens direkt in die Service-Datei schreiben!
