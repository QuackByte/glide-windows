# Changelog

Neuigkeiten in jeder Version von Glide für Windows. Glide zeigt die neuen Einträge einmal nach einem Update an, und die Hinweise zu jedem Release auf GitHub stammen von hier. Dies ist eine Übersetzung von CHANGELOG.md, das Original ist die englische Datei.

## 0.10.1 — 2026-10-19

### Korrekturen

- **In der gekachelten Anordnung erstellen Win+[ und Win+] Registerkartengruppen.** Win+[ fügt das fokussierte Fenster einer Registerkartengruppe hinzu, einer Spalte mit Registerkarten, und Win+] löst einen Tab wieder heraus in eine eigene Kachel, wie es beim Scrollen schon war. Ein Fenster auf eine Registerkartenleiste zu ziehen funktioniert wie zuvor.

## 0.10.0 — 2026-10-19

### Neu

- **Gekachelt ist jetzt eine spiralförmige Anordnung.** Mit Win+Strg+T schaltest du einen Arbeitsbereich auf eine Anordnung um, in der alle Fenster auf den Bildschirm passen: Ein Fenster füllt ihn, zwei teilen ihn sich als Hälften, und ab dem dritten wird die letzte Spalte erneut geteilt, abwechselnd oben und unten mit links und rechts. Sie nutzt 2 Spalten oder 3 mit dem neuen Win+Strg+G, was Glide auch auf Ultrawide-Monitoren wählt. Wechselst du zurück zu Scrollend, bleiben deine Fenster in derselben Reihenfolge.
- **Bewege dich in einem gekachelten Arbeitsbereich nach Richtung.** Win+Pfeiltasten fokussieren die Kachel neben der fokussierten, und Win+Shift+Pfeiltasten tauschen sie mit diesem Nachbarn. Das Wechseln der Breite (Win+Strg+R) ändert, wie die fokussierte Kachel den Platz mit den folgenden Kacheln teilt, und Win+Strg+F zoomt eine Kachel auf den ganzen Bildschirm, bis du es erneut drückst.
- **Wähle, wie viele Spalten gekachelte Arbeitsbereiche nutzen,** unter Einstellungen › Allgemein › Gekachelte Spalten: Automatisch (3 auf Ultrawide-Monitoren, sonst 2), 2 oder 3.

### Änderungen

- **Ein Fenster in einen anderen Arbeitsbereich zu schicken, verschiebt nur dieses Fenster.** Win+Shift+1…9 und Win+Strg+Shift+←/→ nahmen früher die ganze Spalte mit, jetzt geht nur das fokussierte Fenster als eigene Spalte, und du bleibst im aktuellen Arbeitsbereich.

### Korrekturen

- **Brasilianisches Portugiesisch für die Einstellung Anordnung.** Ihre Beschreibung unter Einstellungen › Allgemein ist jetzt auf brasilianischem Portugiesisch formuliert („tela“, „você“) statt auf europäischem.

## 0.9.3 — 2026-10-19

### Neu

- **Gekachelte Anordnung.** Mit Win+Strg+T schaltest du den aktuellen Arbeitsbereich zwischen dem scrollenden Streifen und einer gekachelten Anordnung um, in der sich alle Spalten den Bildschirm teilen und gemeinsam Platz finden, ohne zu scrollen. Jeder Arbeitsbereich behält seine eigene Anordnung, auch nach einem Neustart von Glide. Wechselst du die Breite einer Spalte (Win+Strg+R), bekommt sie einen größeren oder kleineren Anteil, Ziehen an ihrem Rand ändert die Größe, und Win+Strg+F macht sie weiterhin volle Breite, bis du es erneut drückst.
- **Wähle die Anordnung, mit der neue Arbeitsbereiche starten,** unter Einstellungen › Allgemein › Anordnung: Scrollend wie bisher oder Gekachelt. Arbeitsbereiche, die du mit Win+Strg+T umgeschaltet hast, behalten ihre eigene.

## 0.9.2 — 2026-10-16

### Neu

- **Auch Neuigkeiten sprechen deine Sprache.** Die Hinweise, die Glide nach einem Update und unter „Neuigkeiten“ zeigt, erscheinen jetzt in derselben Sprache wie der Rest von Glide.

## 0.9.1 — 2026-10-16

### Neu

- **Glide spricht deine Sprache.** Glide gibt es jetzt außer auf Englisch und Spanisch auch auf Französisch, Deutsch, Japanisch, Chinesisch (vereinfacht), Koreanisch und Portugiesisch (Brasilien), einschließlich des Installationsprogramms. Es richtet sich nach der Anzeigesprache von Windows, oder du wählst unter Einstellungen › Allgemein › Sprache eine aus.

## 0.9.0 — 2026-10-15

### Neu

- **Sieh, wo ein gezogenes Fenster landet.** Beim Ziehen eines Fensters zeigt ein Umriss in der Farbe des Fokusrahmens die Spalte, der es beitreten würde, samt Tableiste. Über einem anderen Monitor zeigt ein Balken, wo es zwischen den Spalten dieses Monitors eingefügt würde. Ohne Hervorhebung springt es zurück.
- **Ein umfangreicheres Menü im Infobereich.** Es listet die verwendeten Arbeitsbereiche auf und zeigt, wie viele Fenster jeder enthält; wähle einen, um dorthin zu wechseln. Unter **Tastenkürzel** findest du jede Aktion mit ihrem Tastenkürzel, um sie direkt aus dem Menü auszuführen. Ist „Windows-Tastenkürzel übernehmen“ ausgeschaltet, listet das Menü auch die Tastenkürzel auf, die bei Windows bleiben.
- **Alle Tastenkürzel auf einer Seite.** Win+Strg+/ (die Taste mit ? auf einer US-Tastatur) zeigt sie alle an; drücke es noch einmal oder Esc, um die Ansicht zu schließen. Du findest sie auch im Menü im Infobereich unter „Tastenkürzel“.
- **Problem melden** im Menü im Infobereich und unter Einstellungen › Allgemein. Glide packt sein Protokoll und seine Einstellungen ohne Fenstertitel in eine ZIP-Datei und öffnet auf GitHub ein neues Issue, in dem du beschreibst, was passiert ist, und die ZIP-Datei anhängst. Von selbst wird nichts gesendet.
- **Glide spricht Spanisch.** Ist Windows auf Spanisch eingestellt, ist es Glide auch, einschließlich des Installationsprogramms. „Neuigkeiten“ bleibt auf Englisch.

### Fehlerbehebungen

- **Vollbild bleibt Vollbild.** Ein Video, ein Spiel oder F11 in einem Browser wird nicht mehr in seine Spalte zurückgeholt. Glide lässt es in Ruhe, blendet Fokusrahmen, Abdunkeln und Tableiste darüber aus und lässt das horizontale Mausrad und Touchpad-Wischgesten zu ihm durch.
- Wenn du zu einem anderen der virtuellen Desktops von Windows und wieder zurück wechselst, bleibt die Reihenfolge deiner Spalten erhalten.
- Das Schließen eines Fensters entzieht den eigenen Fenstern von Glide, etwa „Neuigkeiten“ oder den Einstellungen, nicht mehr den Fokus.

## 0.8.1 — 2026-10-14

### Fehlerbehebungen

- Ein Klick auf **Weiter** im Fenster „Neuigkeiten“ hat Glide beendet. Das Schließen eines Fensters lässt Glide jetzt weiterlaufen.
- Das Symbol im Infobereich zeigt jeden verwendeten Arbeitsbereich: ein 3×3-Raster der neun Arbeitsbereiche, die verwendeten leuchtend und der aktive am hellsten. Ist nur einer in Verwendung, zeigt es wie bisher dessen Nummer.

## 0.8.0 — 2026-10-14

### Neu

- **Inaktive Fenster abdunkeln.** Die Fenster hinter dem fokussierten auf demselben Monitor werden dunkler, damit das fokussierte Fenster hervorsticht. Standardmäßig aus; schalte es unter Einstellungen › Darstellung ein und lege dort fest, wie dunkel.
- **Jeder verwendete Arbeitsbereich wird aufgelistet.** Der Tooltip und das Menü des Symbols im Infobereich zeigen alle Arbeitsbereiche, in denen Fenster liegen. Das Symbol zeigt weiterhin den aktiven.
- **Die Arbeitsbereichsnummer auf dem Bildschirm beim Wechseln** lässt sich unter Einstellungen › Darstellung ausschalten.
- **Updates automatisch installieren.** Standardmäßig aus. Schalte es unter Einstellungen › Updates ein, und Glide installiert eine neue Version, sobald es eine findet, und startet dann neu.

### Änderungen

- Releases sind noch nicht per Code signiert, daher warnt Windows SmartScreen beim ersten Start von Glide eventuell: Wähle **Weitere Informationen › Trotzdem ausführen**.
- Die Datenschutzerklärung im Installationsprogramm verweist nicht mehr auf eine Richtlinie zur Codesignierung.

## 0.7.0 — 2026-10-14

### Neu

- **Glide hält sich selbst aktuell.** Zweimal täglich sucht es nach einer neuen Version und bietet sie über das Symbol im Infobereich an. Jeder Download wird vor der Installation geprüft, und Glide stellt deine Fenster wieder her, aktualisiert sich und startet neu. Schalte es unter Einstellungen › Updates oder während der Installation aus.
- **Neuigkeiten.** Nach einem Update zeigt Glide, was sich geändert hat, so wie hier. Du findest die Hinweise jederzeit im Menü im Infobereich.
- **Apps kehren dorthin zurück, wo du sie haben willst.** Glide merkt sich für jede App Monitor, Arbeitsbereich, Spaltenbreite und Position auf dem Band. Das Fenster einer erneut geöffneten App öffnet sich dort, und nach einem Neustart werden deine geöffneten Fenster genauso angeordnet. Schalte es unter Einstellungen › Allgemein aus oder lass Glide alles vergessen.
- **Glide ist Open Source** unter der MIT-Lizenz: [github.com/QuackByte/glide-windows](https://github.com/QuackByte/glide-windows).

### Änderungen

- Das Installationsprogramm zeigt die Datenschutzerklärung von Glide und lässt dich wählen, ob Glide nach Updates sucht.

## 0.6.0 — 2026-10-14

### Neu

- **App-Symbole** neben den Fenstertiteln in der Übersicht und in Tableisten.
- **Die Übersicht scrollt**, wenn ein Band für den Bildschirm zu lang ist, statt Kacheln so lange zu verkleinern, bis sie unlesbar sind. Das Mausrad scrollt die Zeile unter dem Zeiger.

### Fehlerbehebungen

- Tableisten gleiten mit ihrer Spalte mit, statt ihr vorauszuspringen.
- Nach dem Stapeln oder Lösen konnte der Fokusrahmen dort erscheinen, wo das Fenster vorher war. Jetzt folgt er dem Fenster.

## 0.5.0 — 2026-10-14

### Neu

- **Mehrere Monitore**: Jeder Monitor hat sein eigenes Band und seine eigenen neun Arbeitsbereiche. Mit Win+Alt+←/→ wechselst du den Fokus zwischen Monitoren; mit Win+Alt+Shift+←/→ schickst du die fokussierte Spalte hinüber.
- **Ziehen und Ablegen**: Lege ein Fenster auf einer Tableiste ab, um es zu den Tabs hinzuzufügen, auf einem anderen Fenster, um es dort zu stapeln, oder auf einem anderen Monitor, um es zu verschieben.
- **Ein Fokusrahmen für jede App**, auch für Electron-Apps, mit einstellbarer Stärke.
- **Dreifinger-Wischgesten auf dem Touchpad** gleiten am Band entlang.
- „Glide bei der Anmeldung starten“ im Menü im Infobereich.

## 0.4.1 — 2026-10-14

Das erste Release von Glide für Windows.
