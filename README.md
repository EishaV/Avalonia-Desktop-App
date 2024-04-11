# Avalonia-Desktop-App
Eine portable Anwendung mit der man M�her des Herstellers Positec von einem Desktop-PC beobachten und steuern kann.
Dazu geh�ren die Marken Worx Landroid, Kress Mission, Landxcape und Ferrex Smartmower.

## Installation
Voraussetzung ist die Installation der .NET Runtime 8 auf dem Desktop-System. Hinweise dazu bitte direkt bei Microsoft [Download .NET 8.0](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) nachlesen.
Die ZIP-Datei aus dem Release bereich muss in einem f�r den Nutzer schreibbaren Ordner entpackt werden. In den Unterordnern Data, Trace und Plugins werden Anemlde-Daten, Protokolle usw. abgelegt.

## Kurzer �berblick �ber die Applikation

| Tab / Register | Beschreibung |
| --- | --- |
| ![Status-Tab](./README/Status.webp) | Hier werden die vom M�her gelieferten Informationen anschaulich dargestellt. Die Rohdaten sieht man �brigens im Tab **_Mqtt_**.<br /> Der Zeitpunkt der Letzten Aktualisierung wird unterhalb des M�hers angezeigt. Eine Aktualisierung kann �ber den Poll-Button angestossen werden.<br/> In der Zeile _M�hzeit_ wird diese seit dem letzten Messerwechsel angezeigt. �ber den Button **_Setze 0_** kann dieser best�tigt werden (gemerkt in der Cloud).<br/>Die Zeile Statisik enth�lt die Gesamtwerte von Distanz und Arbeits-/M�hdauer.<br/>Die Buttons **_Start_**, **_Stopp_** und **_Heim_** dienen zur Steuerung des M�hers. Weitere Funktionen sind ggf. �ber den Button <img src="/AvaDeskApp/Assets/Menu.webp" height=16 /> verf�gbar.|
| --- | --- |
| ![Konfig-Tab](./README/Config.webp) | Im Tab **_Konfig_** erfolgt die Konfiguration des M�hers.<br />In der Klappe **_M�hzeitplan_** werden pro Tag Kantenschnitt, Startzeit und Dauer direkt eingegeben werden. Die Endzeit wird aufgrund des Korrekturfaktors berechnet. �ber die Korrektur wird der M�hplan gesamtheitlich ver�ndert. Bei -100% wird nicht gearbeitet, bei +100% doppelt soviel. Die Buttons darunter erlauben �nderungen in 50%-Schritte.<br/>�ber die Klappe **_Multizonen_** kann man Starpunkte f�r 10 umlaufende Ausfahrten konfigurieren. Der M�her f�hrt die vorgegeben Meterr am BK entlang und biegt dann in die Fl�che ab. Um wirkliche Zonen zu erzeugen muss man das BK an den �berg�ngen verengen.<br/>In der Klappe **_Weitere Einstellungen_** enth�lt die _Regenzeit_ welche der M�her nach abtrocknen des Sensors noch in der Station verbleibt. Bei einem Wert von 0 ignoriert der M�her den Regen. Bei neueren Modell kann hier auch das __Drehmoment_ angepasst werden.<br/>Der Button **_Sichern_** �bertr�gt die Konfiguration zum M�her. Die aufpoppende Meldung mit dem Json-Inhalt dient zu Diagnosezwecken.|

