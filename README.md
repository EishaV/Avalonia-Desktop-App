# PosiMowApp (Avalonia-Desktop-App)
Eine portable Anwendung mit der man M�her des Herstellers Positec von einem Desktop-PC beobachten und steuern kann.
Dazu geh�ren die Marken Worx Landroid, Kress Mission, Landxcape und Ferrex Smartmower.
Weiterhin ist eine rudiment�re Unterst�tzung f�r Worx Vision und Kress RTK animplemntiert.

## Installation
Voraussetzung ist die Installation der .NET Runtime 8 auf dem Desktop-System. Hinweise dazu bitte direkt bei Microsoft [Download .NET 8.0](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) nachlesen.

Die ZIP-Datei aus dem Release-Bereich muss in einem f�r den Nutzer schreibbaren Ordner entpackt werden. In den Unterordnern Data, Trace und Plugins werden Anmelde-Daten, Protokolle usw. abgelegt.
Unter Windows x64 kann die Anwendung �ber Doppelklick auf die `AvaApp.exe` gestartet werden. F�r andere Betreibssysteme einschlie�lch Windows x86/ARM muss �ber eine Konsole die Anwendung mittels `dotnet AvaApp.dll` ausgef�hrt werden.
Einige Betriebssystem warnen oder verhindern den Start der PosiMowApp. Unter Windows dann halt trotzdem ausf�hren w�hlen und unter Mac OS Einstellungen > Datenschutz & Sicherheit > Entwickler-Werkzeuge - das Programm `Terminal.app` als Ausnahme hinzuf�gen.

## Kurzer �berblick �ber die Applikation

| Tab / Register | Beschreibung |
| --- | --- |
| ![Status-Tab](./README/Status.webp) | Hier werden die vom M�her gelieferten Informationen anschaulich dargestellt. Die Rohdaten sieht man �brigens im Tab **_Mqtt_**.<br /> Der Zeitpunkt der Letzten Aktualisierung wird unterhalb des M�hers angezeigt. Eine Aktualisierung kann �ber den Poll-Button angestossen werden.<br/> In der Zeile _M�hzeit_ wird diese seit dem letzten Messerwechsel angezeigt. �ber den Button **_Setze 0_** kann dieser best�tigt werden (gemerkt in der Cloud).<br/>Die Zeile Statisik enth�lt die Gesamtwerte von Distanz und Arbeits-/M�hdauer.<br/>Die Buttons **_Start_**, **_Stopp_** und **_Heim_** dienen zur Steuerung des M�hers. Weitere Funktionen sind ggf. �ber den Button <img src="/AvaDeskApp/Assets/Menu.webp" height=16 /> verf�gbar.|
| ![Konfig-Tab](./README/Config.webp) | Im Tab **_Konfig_** erfolgt die Konfiguration des M�hers.<br />In der Klappe **_M�hzeitplan_** werden pro Tag Kantenschnitt, Startzeit und Dauer direkt eingegeben werden. Die Endzeit wird aufgrund des Korrekturfaktors berechnet. �ber die Korrektur wird der M�hplan gesamtheitlich ver�ndert. Bei -100% wird nicht gearbeitet, bei +100% doppelt soviel. Die Buttons darunter erlauben �nderungen in 50%-Schritte.<br/>�ber die Klappe **_Multizonen_** kann man Starpunkte f�r 10 umlaufende Ausfahrten konfigurieren. Der M�her f�hrt die vorgegeben Meterr am BK entlang und biegt dann in die Fl�che ab. Um wirkliche Zonen zu erzeugen muss man das BK an den �berg�ngen verengen.<br/>In der Klappe **_Weitere Einstellungen_** enth�lt die _Regenzeit_ welche der M�her nach abtrocknen des Sensors noch in der Station verbleibt. Bei einem Wert von 0 ignoriert der M�her den Regen. Bei neueren Modell kann hier auch das __Drehmoment_ angepasst werden.<br/>Der Button **_Sichern_** �bertr�gt die Konfiguration zum M�her. Die aufpoppende Meldung mit dem Json-Inhalt dient zu Diagnosezwecken.|
| ![Mqtt-Tab](/README/Mqtt.webp)| Die Rohdaten welche zwischen M�her, Cloud und App ausgetauscht werden kommen im Tab **_Mqtt_** zur Anzeige.<br /> **Expertenmodus:** Im Feld _CmdIn-String_ kann ein spezifisches Kommando an den M�her gesendet werden. Einige Vorbelegungen sind �ber AutoComplete erreichbar indem man das ein Anf�hrungszeichen " eingibt. |
| ![Aktivit�t-Tab](/README/ActLog.webp)| Der Tab **_Aktivit�t_** zeigt das in der Cloud mitgeschriebene Aktivit�tsprotokoll des M�hers an.<br />Die Aktivit�tseintr�ge enthalten nicht den kompletten Status, sondern nur relevante Dinge, wie eine Zeitstempel, den Status-/Fehlercode und den Ladezustand (C). |
