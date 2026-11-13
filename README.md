<div align="center">

<img src="docs/luma-mark.svg" width="96" alt="Luma Logo" />

# ✨ Luma

### Weniger Licht. Mehr Spiel.

Dein Xbox-Controller muss nachts kein Flutlicht sein. 🎮🌙

**Windows 11 · WinUI 3 · .NET 10 · MIT**

</div>

---

## 🎮 Kleine App, angenehmeres Gaming

Luma regelt die Helligkeit des Xbox-Logos direkt aus einer nativen Windows-Oberfläche oder dem Tray. Entstanden als Vibe-Coding-Projekt mit Codex, gebaut für die Abende, an denen selbst eine einzelne LED zu hell ist.

| Funktion | Was du bekommst |
| --- | --- |
| 🌙 Helligkeit | Regler von 0–100 % und Presets für Nacht, entspanntes Spielen und volle Helligkeit |
| 💡 Lichtmodi | Ein/Aus, Dauerlicht, Pulsieren und Blinken |
| 🍃 Tray | Eigenes grünes Luma-Icon mit Schnellaktionen per Rechtsklick |
| 🔁 Wiederverbinden | Letzte Einstellung automatisch auf neu erkannte Controller anwenden |
| 🚀 Autostart | Optional mit Windows im Tray starten |
| 🖥️ Native UI | WinUI 3 mit dunkler Oberfläche und LED-Vorschau |

> **Verbindung:** USB oder Microsoft Xbox Wireless Adapter. Über Bluetooth lässt sich die Logo-LED auf diesem Weg nicht steuern.

## 🧱 Unter der Haube

Eine native Windows-11-App auf Basis von **WinUI 3, Windows App SDK 2.6.5 und .NET 10**. Das XboxGIP-Protokoll basiert auf [xbledctl](https://github.com/Leclowndu93150/xbledctl) von Leclowndu93150 (MIT).

## 🚀 Schnellstart

Das komplette Archiv `Luma-Windows-x64.zip` entpacken und `Luma.exe` öffnen. Die Dateien neben der EXE werden benötigt. .NET und Windows-App-SDK-Laufzeit sind enthalten; es muss kein Entwickler-SDK installiert werden.

Controller per USB oder Microsoft Xbox Wireless Adapter verbinden. Bluetooth unterstützt die hier verwendete LED-Steuerung nicht. Steam wird nicht benötigt. Andere LED-Steuerungsprogramme möglichst schließen, damit sie die Einstellung nicht überschreiben.

## 🎛️ Bedienung & Hinweise

- Controller auswählen, Helligkeit verschieben oder ein Preset wählen.
- Der Schalter „Xbox-Logo“ schaltet nur die LED aus; die eingestellte Helligkeit bleibt erhalten.
- 0–100 % entsprechen dem unterstützten Protokollbereich 0–47. Die Vorschau zeigt die gewählte Einstellung, keine vom Controller zurückgelesene Helligkeit.
- Lichtmodus: dauerhaft, pulsieren, blinken, Ladeanimation oder Einblenden. Die Unterstützung einzelner Animationen hängt vom Controller ab.
- „In den Tray“ blendet das Fenster aus. Das hellgrüne **L** im Infobereich öffnet es wieder. Rechtsklick bietet LED ein/aus, drei Presets und Beenden.
- Windows kann neue Tray-Symbole zunächst im Überlauf unter dem Pfeil verbergen. Das Luma-Symbol lässt sich von dort in den sichtbaren Infobereich ziehen.
- „Beim Verbinden anwenden“ lädt den zuletzt gespeicherten Standardwert bei neu erkannten Controllern. Die Erkennung wird alle acht Sekunden geprüft.
- „Mit Windows starten“ ist optional und zunächst deaktiviert. Es erzeugt einen Eintrag ausschließlich im Autostart des aktuellen Windows-Benutzers. Bei einem Umzug des App-Ordners den Schalter einmal aus- und wieder einschalten.
- „Beim Schließen im Tray weiterlaufen“ lässt die App beim Schließen aktiv. Vollständig beenden: Tray-Menü oder Einstellungen → Luma beenden.

Einstellungen liegen in `%LOCALAPPDATA%\LumaXboxLed\settings.json`. Es gibt bewusst einen gemeinsamen gespeicherten Standardwert, da XboxGIP-Geräte-IDs nach erneutem Verbinden wechseln können. Manuelle Änderungen werden nur an den ausgewählten Controller gesendet; beim automatischen Wiederverbinden erhalten neu erkannte Geräte den Standardwert.

## 🛠️ Selbst bauen

Voraussetzung: Windows x64 und .NET 10 SDK. Die Windows-Buildwerkzeuge werden per NuGet bezogen.

```powershell
.\build.ps1          # Release-Build
.\build.ps1 -Publish # Eigenständige portable Version
dotnet run --project .\Tests\Luma.Tests.csproj -c Release
```

`Luma.exe --demo` öffnet eine klar gekennzeichnete Vorschau ohne Hardwarebefehle oder gespeicherte Änderungen. `Luma.exe --tray` startet im Infobereich.

Die Protokolltests verwenden ein unabhängiges 23-Byte-Referenzpaket und prüfen Off-/Nullwerte, Modusvalidierung, vollständige 64-Bit-Geräte-IDs, Grenzen und monotone Helligkeitsumrechnung. Mit `--discover` hinter den Testprogrammargumenten lässt sich zusätzlich die Geräteerkennung testen, ohne LED-Befehle zu senden.

Das Backend öffnet die bestehende Microsoft-Schnittstelle `\\.\XboxGIP`. Es installiert keine Treiber und verarbeitet oder emuliert keine Spieleingaben. Der zugrunde liegende Treiberzugriff wurde vom xbledctl-Projekt durch Reverse Engineering erschlossen. Eine erfolgreiche Übertragung bestätigt nicht, dass jede Firmware jeden Animationsmodus umsetzt.

## 🧪 Geprüft

Der Release-Build wurde lokal ohne Fehler und Warnungen gebaut. Die Protokollprüfung umfasst 1.626 Assertions; die Geräteerkennung wurde mit einem angeschlossenen Xbox-Controller geprüft. Tray-Icon und Rechtsklick-Menü wurden vom Nutzer bestätigt. Die LED-Vorschau ist eine Darstellung der gewählten Einstellung und keine Messung der tatsächlichen Leuchtstärke.

## 💚 Danke an das Originalprojekt

Ein großes Dankeschön an **[Leclowndu93150](https://github.com/Leclowndu93150)** und **[xbledctl](https://github.com/Leclowndu93150/xbledctl)**!

xbledctl hat die entscheidende Vorarbeit für die LED-Steuerung unter Windows geleistet. Luma übernimmt daraus die Idee und den durch Reverse Engineering erschlossenen Zugriff auf den Microsoft-XboxGIP-Treiber, einschließlich des LED-Kommandos und des Paketformats. Das Backend wurde dafür in C# übertragen; WinUI-Oberfläche und Bedienung wurden für Luma entwickelt.

Die ursprüngliche Arbeit gehört ihren Autoren. Der MIT-Lizenztext und Copyright-Hinweis von xbledctl liegen vollständig in [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt), und die Herkunft ist in [GipController.cs](GipController.cs) vermerkt. Wer eine kompakte C/C++-Implementierung oder die technische Recherche sucht, sollte sich unbedingt das Original ansehen.

Danke auch an die Teams hinter **WinUI**, **Windows App SDK** und **.NET**, auf denen diese Oberfläche aufbaut.

## 📄 Lizenz

MIT; siehe `LICENSE` und `THIRD-PARTY-NOTICES.txt`. Die enthaltenen Microsoft-Laufzeitkomponenten unterliegen ihren jeweiligen Lizenzen.
