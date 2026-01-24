# Claude Status Monitor

A compact Windows overlay widget that displays Claude usage limits in real time.

## Features

- Always-on-top WPF overlay with three usage bars (Session, All models, Sonnet)
- Automatic refresh with configurable interval
- Native Claude login via WebView2 (no manual cookie handling)
- Context menu for lock/unlock, refresh, re-login, and exit
- Local-only storage of settings (no credentials in config)

## Requirements

- Windows 10/11
- .NET 8 SDK (for building) or .NET 8 Runtime (for running)
- WebView2 Runtime (typically preinstalled on Windows 10/11)

## Quick start

1. Build the app:

```bash
cd ClaudeStatusMonitor
dotnet build
```

2. Run the app:

```bash
dotnet run
```

3. Sign in when the login window appears. The main widget will update automatically.

## Configuration

The app reads `config.json` next to the executable. Only settings are stored there.

Example:

```json
{
  "refreshIntervalMinutes": 2
}
```

## Build & Publish

### Build (Debug)

```bash
cd ClaudeStatusMonitor
dotnet build
```

### Publish single-file (win-x64)

```bash
dotnet publish -c Release -r win-x64 \
  -p:PublishSingleFile=true \
  -p:SelfContained=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true
```

Output:

```
ClaudeStatusMonitor/bin/Release/net8.0-windows/win-x64/publish/ClaudeStatusMonitor.exe
```

## Usage

- Left click and drag to move the widget (unless locked)
- Right click to open the context menu
  - Lock/unlock position
  - Refresh now
  - Re-login
  - Exit

## Troubleshooting

- "Login required": use right click -> Re-login
- "Update failed": verify you are signed in and try refresh
- WebView2 initialization errors: ensure WebView2 Runtime is installed

## Security & Privacy

- Login happens inside WebView2 using the local browser profile
- The app does not store cookies or credentials in `config.json`
- All data stays on the local machine

## License

Personal use only.
