# Codex Usage Widget

Widget Windows WPF affichant les limites Codex restantes sur 5 heures et sur
une semaine, ainsi que les credits disponibles.

## Developpement

```powershell
dotnet test
dotnet run --project .\CodexUsageWidget
```

## Publication autonome Windows x64

```powershell
dotnet publish .\CodexUsageWidget\CodexUsageWidget.csproj `
  -c Release -r win-x64 --self-contained true -o .\dist
```

L'executable final est genere dans `dist\CodexUsageWidget.exe`.

Le widget utilise l'authentification existante de Codex CLI via
`codex app-server`. Aucun token ni aucune cle API ne sont stockes.

## Creer l'installateur

Installer Inno Setup 6 une seule fois :

```powershell
winget install --exact --id JRSoftware.InnoSetup
```

Puis executer :

```powershell
.\installer\build-installer.ps1
```

Le wizard autonome est genere dans `installer-output`.
