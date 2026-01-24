# Claude Status Monitor

Kompaktes Windows-Overlay zur Anzeige deiner Claude MAX Usage-Limits in Echtzeit.

## Features

- Always-on-top Overlay mit 3 Balken (Session, All models, Sonnet)
- Auto-Refresh (Standard: 2 Minuten, konfigurierbar)
- Rechtsklick-Menue: Sperren, Refresh, Neu anmelden, Beenden
- Login direkt in WebView2 (keine Cookies manuell noetig)

## Voraussetzungen

- Windows 10/11
- .NET 8 SDK (zum Bauen) oder .NET 8 Runtime (zum Ausfuehren)
- WebView2 Runtime (meist bereits installiert)

## Nutzung

1. App starten
2. Im Login-Fenster bei claude.ai anmelden
3. Fenster schliesst automatisch, Daten werden aktualisiert

## Konfiguration

`config.json` liegt neben der EXE und enthaelt nur Settings:

```json
{
  "refreshIntervalMinutes": 2
}
```

## Build

```bash
cd ClaudeStatusMonitor
dotnet build
```

## Single-EXE Publish (win-x64)

```bash
dotnet publish -c Release -r win-x64 \
  -p:PublishSingleFile=true \
  -p:SelfContained=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true
```

Output:
- `bin\\Release\\net8.0-windows\\win-x64\\publish\\ClaudeStatusMonitor.exe`

## Troubleshooting

- "Anmeldung erforderlich": Rechtsklick -> Neu anmelden
- "Aktualisierung fehlgeschlagen": Login pruefen, danach Refresh

## Lizenz

Nur fuer persoenlichen Gebrauch.
