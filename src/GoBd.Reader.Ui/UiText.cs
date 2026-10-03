using System.Globalization;
using GoBd.Validation.Localisation;

namespace GoBd.Reader.Ui;

/// <summary>
/// Strongly typed, compile-time verified message catalogue for user interface text in English and German.
/// </summary>
public static class UiText
{
    /// <summary>German digit grouping, written out rather than taken from a culture.</summary>
    /// <remarks>
    /// The reader runs in invariant globalization, where every culture formats as the invariant
    /// one does, so asking for "de-DE" would still group thousands with a comma — which a German
    /// reader takes for a decimal point.
    /// </remarks>
    private static readonly NumberFormatInfo GermanNumbers = new()
    {
        NumberGroupSeparator = ".",
        NumberDecimalSeparator = ",",
    };

    private static bool IsDe(ReportLanguage language) => language == ReportLanguage.German;

    /// <summary>How numbers are written in a language.</summary>
    internal static IFormatProvider Numbers(ReportLanguage l) => IsDe(l) ? GermanNumbers : CultureInfo.InvariantCulture;

    /// <summary>A count, with its thousands grouped as the language groups them.</summary>
    public static string Count(ReportLanguage l, long count) => count.ToString("N0", Numbers(l));

    // Window & Common
    public static string SummaryTab(ReportLanguage l) => IsDe(l) ? "Übersicht" : "Summary";
    public static string SettingsTitle(ReportLanguage l) => IsDe(l) ? "Einstellungen" : "Settings";
    public static string Close(ReportLanguage l) => IsDe(l) ? "Schließen" : "Close";
    public static string CloseTab(ReportLanguage l, string name) => IsDe(l) ? $"Tab für '{name}' schließen" : $"Close tab for {name}";
    public static string TableTab(ReportLanguage l, string name) => IsDe(l) ? $"Tabellen-Tab {name}" : $"Table tab {name}";
    public static string References(ReportLanguage l) => IsDe(l) ? "Verweist auf" : "References";
    public static string ReferencedBy(ReportLanguage l) => IsDe(l) ? "Referenziert von" : "Referenced by";
    public static string PickArchiveTitle(ReportLanguage l) => IsDe(l) ? "GoBD-Export öffnen (ZIP-Archiv)" : "Open a GoBD export (ZIP archive)";
    public static string ZipArchive(ReportLanguage l) => IsDe(l) ? "ZIP-Archiv" : "ZIP archive";
    public static string PickFolderTitle(ReportLanguage l) => IsDe(l)
        ? "GoBD-Export öffnen (Ordner mit index.xml)"
        : "Open a GoBD export (folder containing index.xml)";

    // Empty State
    public static string NoExportOpened(ReportLanguage l) => IsDe(l) ? "Kein Export geöffnet" : "No export opened";
    public static string OpenPrompt(ReportLanguage l) => IsDe(l)
        ? "Öffnen Sie einen GoBD-Export über Datei → Archiv öffnen… oder Ordner öffnen…"
        : "Open a GoBD export with File → Open Archive… or Open Folder…";
    public static string RefusalTitle(ReportLanguage l) => IsDe(l) ? "Dieser Export konnte nicht geöffnet werden" : "This export could not be opened";
    public static string KeptOpen(ReportLanguage l) => IsDe(l)
        ? "Das konnte nicht geöffnet werden, daher bleibt der geöffnete Export geöffnet."
        : "That could not be opened, so the open export stays open.";
    public static string StoreReadableByOthers(ReportLanguage l, string directory) => IsDe(l)
        ? $"Der Leser legt die Daten eines Exports in {directory} ab, aber andere Benutzerkonten können dieses Verzeichnis lesen. "
            + "Entfernen Sie es, oder starten Sie den Leser mit TMPDIR auf ein eigenes Verzeichnis gesetzt."
        : $"The reader keeps an export's data in {directory}, but other accounts can read that directory. "
            + "Remove it, or start the reader with TMPDIR set to a directory of your own.";
    public static string StoreSymbolicLink(ReportLanguage l, string directory) => IsDe(l)
        ? $"Der Leser legt die Daten eines Exports in {directory} ab, aber das ist ein symbolischer Link, der die Daten "
            + "überallhin leiten könnte. Entfernen Sie ihn, oder starten Sie den Leser mit TMPDIR auf ein eigenes Verzeichnis gesetzt."
        : $"The reader keeps an export's data in {directory}, but that is a symbolic link, which could lead the data "
            + "anywhere. Remove it, or start the reader with TMPDIR set to a directory of your own.";
    public static string StoreUnusable(ReportLanguage l, string directory) => IsDe(l)
        ? $"Der Leser legt die Daten eines Exports in {directory} ab, kann es aber nicht verwenden: Das Verzeichnis gehört "
            + "einem anderen Benutzerkonto, oder an seiner Stelle steht etwas anderes. Starten Sie den Leser mit TMPDIR auf ein "
            + "eigenes Verzeichnis gesetzt."
        : $"The reader keeps an export's data in {directory}, but it cannot use that: the directory belongs to another "
            + "account, or something else is in its place. Start the reader with TMPDIR set to a directory of your own.";
    public static string StoreSpace(ReportLanguage l, long neededMegabytes, string volume, long freeMegabytes) => IsDe(l)
        ? string.Create(Numbers(l), $"Der Speicher braucht etwa {neededMegabytes:N0} MB, und {volume} hat {freeMegabytes:N0} MB frei.")
        : string.Create(Numbers(l), $"The store needs about {neededMegabytes} MB and {volume} has {freeMegabytes} MB free.");
    public static string MemoryBacked(ReportLanguage l) => IsDe(l)
        ? "Dieses Volume liegt im Arbeitsspeicher statt auf einem Datenträger, daher gehört der Speicher woandershin."
        : "That volume is memory rather than disk, so the store belongs elsewhere.";

    // Summary Dashboard
    public static string TablesHeading(ReportLanguage l) => IsDe(l) ? "Tabellen" : "Tables";
    public static string FindingsHeading(ReportLanguage l) => IsDe(l) ? "Feststellungen" : "Findings";
    public static string NoticesHeading(ReportLanguage l) => IsDe(l) ? "Was dieser Leser nicht verarbeiten kann" : "What this reader cannot do with it";
    public static string ColTable(ReportLanguage l) => IsDe(l) ? "Tabelle" : "Table";
    public static string ColState(ReportLanguage l) => IsDe(l) ? "Status" : "State";
    public static string ColRecords(ReportLanguage l) => IsDe(l) ? "Datensätze" : "Records";
    public static string ColFindings(ReportLanguage l) => IsDe(l) ? "Feststellungen" : "Findings";
    public static string ExportGroup(ReportLanguage l) => IsDe(l) ? "Der Export" : "The export";
    public static string NumberTooLarge(ReportLanguage l, string column) => IsDe(l)
        ? $"'{column}' enthält Zahlen mit mehr Stellen, als dieser Leser exakt berechnet, daher bietet er für diese Spalte "
            + "keinen Filter, keine Sortierung und keine Kennzahl an. Jeder Wert wird weiterhin so angezeigt, wie die Datei ihn schreibt."
        : $"'{column}' holds numbers with more digits than this reader computes with exactly, so it offers no filter, "
            + "sort or figure for that column. Every value is still shown as the file writes it.";
    public static string ValuesNotTimes(ReportLanguage l, string column, long count) => IsDe(l)
        ? $"'{column}' ist als Uhrzeit deklariert, und {Count(l, count)} seiner Werte lassen sich nicht als solche lesen. "
            + "Filter, Sortierung und Kennzahlen behandeln sie als fehlend."
        : $"'{column}' declares a time, and {Count(l, count)} of its values cannot be read as one. "
            + "Filtering, sorting and figures treat those as absent.";
    public static string Conformant(ReportLanguage l) => IsDe(l) ? "Konform" : "Conformant";
    public static string NonConformant(ReportLanguage l) => IsDe(l) ? "Nicht konform" : "Not conformant";
    public static string TruncatedNotice(ReportLanguage l) => IsDe(l)
        ? "Die Analyse dieser Tabelle wurde an ihrem Limit gestoppt. Sie kann weitere nicht geprüfte Mängel enthalten."
        : "Analysis of this table stopped at its limit. It may hold further defects that were not looked for.";
    public static string ReadingExport(ReportLanguage l) => IsDe(l) ? "Export wird eingelesen" : "Reading the export";
    public static string ReadSummary(ReportLanguage l, int tables, string size, int errors, int warnings) => IsDe(l)
        ? string.Create(CultureInfo.InvariantCulture, $"{tables} Tabelle(n), {size} eingelesen. {errors} Fehler, {warnings} Warnung(en).")
        : string.Create(CultureInfo.InvariantCulture, $"{tables} table(s), {size} read. {errors} error(s), {warnings} warning(s).");
    public static string ReadProgress(ReportLanguage l, string? table, string read, string whole) => IsDe(l)
        ? (table is null ? "Vorbereitung. " : $"Lese '{table}'. ") + $"{read} von {whole} eingelesen."
        : (table is null ? "Preparing. " : $"Reading '{table}'. ") + $"{read} of {whole} read.";
    public static string ReadingProgressName(ReportLanguage l) => IsDe(l) ? "Lesefortschritt" : "Reading progress";
    public static string ReadingPercent(ReportLanguage l, int percent) => IsDe(l)
        ? string.Create(CultureInfo.InvariantCulture, $"Export zu {percent} % eingelesen.")
        : string.Create(CultureInfo.InvariantCulture, $"Export {percent}% read.");
    public static string TableFinished(ReportLanguage l, string table, string state) => $"'{table}': {state}.";
    public static string ReadingFinished(ReportLanguage l, int tables, int errors, int warnings) => IsDe(l)
        ? string.Create(CultureInfo.InvariantCulture, $"{tables} Tabelle(n) eingelesen. {errors} Fehler, {warnings} Warnung(en).")
        : string.Create(CultureInfo.InvariantCulture, $"{tables} table(s) read. {errors} error(s), {warnings} warning(s).");
    public static string StateWaiting(ReportLanguage l) => IsDe(l) ? "wartend" : "waiting";
    public static string StateReading(ReportLanguage l, double fraction) => IsDe(l)
        ? string.Create(CultureInfo.InvariantCulture, $"liest {fraction * 100:F0}%")
        : string.Create(CultureInfo.InvariantCulture, $"reading {fraction * 100:F0}%");
    public static string StateRead(ReportLanguage l) => IsDe(l) ? "gelesen" : "read";
    public static string StateDefective(ReportLanguage l) => IsDe(l) ? "nicht konform" : "does not conform";
    public static string StateUnreadable(ReportLanguage l) => IsDe(l) ? "konnte nicht gelesen werden" : "could not be read";

    // Table Tab
    public static string DataSubtitle(ReportLanguage l, long records, int columns) => IsDe(l)
        ? string.Create(Numbers(l), $"{records:N0} Datensätze · {columns} Spalten")
        : string.Create(Numbers(l), $"{records:N0} records · {columns} columns");
    public static string FindingsCount(ReportLanguage l, int count) => IsDe(l)
        ? string.Create(CultureInfo.InvariantCulture, $"{count} Feststellung(en)")
        : string.Create(CultureInfo.InvariantCulture, $"{count} finding(s)");
    public static string RecordColumn(ReportLanguage l) => IsDe(l) ? "Datensatz" : "Record";
    public static string TableNotReady(ReportLanguage l) => IsDe(l)
        ? "Diese Tabelle wird noch eingelesen. Sie wird angezeigt, sobald sie fertig ist."
        : "This table is still being read. It will be shown as soon as it has been.";
    public static string TableUnreadable(ReportLanguage l) => IsDe(l)
        ? "Diese Tabelle konnte nicht gelesen werden, daher werden ihre Daten nicht angezeigt. Der Rest des Exports bleibt unberührt."
        : "This table could not be read, so its data is not shown. The rest of the export is unaffected.";
    public static string TableNotConformant(ReportLanguage l) => IsDe(l)
        ? "Diese Tabelle entspricht nicht ihrer Deklaration, daher werden ihre Daten nicht angezeigt."
        : "This table does not conform to its declaration, so its data is not shown.";

    // Toolbar & Chips
    public static string FiltersLabel(ReportLanguage l) => IsDe(l) ? "Filter" : "Filters";
    public static string SortLabel(ReportLanguage l) => IsDe(l) ? "Sortierung" : "Sort";
    public static string FiguresLabel(ReportLanguage l) => IsDe(l) ? "Kennzahlen" : "Figures";
    public static string AddFilter(ReportLanguage l) => "+ Filter";
    public static string AddColumn(ReportLanguage l) => IsDe(l) ? "+ Spalte" : "+ Column";
    public static string BackToFileOrder(ReportLanguage l) => IsDe(l) ? "Zurück zur Dateireihenfolge" : "Back to file order";
    public static string Preparing(ReportLanguage l) => IsDe(l) ? "Wird vorbereitet…" : "Preparing…";
    public static string FileOrderBanner(ReportLanguage l, long count) => IsDe(l)
        ? string.Create(Numbers(l), $"{count:N0} Datensätze, in Dateireihenfolge")
        : string.Create(Numbers(l), $"{count:N0} records, in file order");
    public static string ShowingBanner(ReportLanguage l, long held, long whole) => IsDe(l)
        ? string.Create(Numbers(l), $"Zeige {held:N0} von {whole:N0} Datensätzen")
        : string.Create(Numbers(l), $"Showing {held:N0} of {whole:N0} records");
    public static string NoRecordMatches(ReportLanguage l) => IsDe(l) ? "Kein Datensatz entspricht den Filtern" : "No record matches";
    public static string StillBeingRead(ReportLanguage l) => IsDe(l) ? "Wird noch eingelesen" : "Still being read";
    public static string PreviousRecord(ReportLanguage l) => IsDe(l) ? "Zurück" : "Previous";
    public static string NextRecord(ReportLanguage l) => IsDe(l) ? "Weiter" : "Next";
    public static string PreviousReferring(ReportLanguage l) => IsDe(l) ? "Vorheriger verweisender Datensatz" : "Previous referring record";
    public static string NextReferring(ReportLanguage l) => IsDe(l) ? "Nächster verweisender Datensatz" : "Next referring record";
    public static string ReturnToFileOrder(ReportLanguage l) => IsDe(l) ? "Zurück zur Dateireihenfolge" : "Return to file order";
    public static string RemoveChip(ReportLanguage l, string name) => IsDe(l) ? $"{name} entfernen" : $"Remove {name}";

    // Reference Tooltips & Menus
    public static string FollowsTo(ReportLanguage l, string leads) => IsDe(l) ? $"Verweist auf '{leads}'." : $"Follows to '{leads}'.";
    public static string RefersToNothing(ReportLanguage l, string leads) => IsDe(l)
        ? $"Verweist auf '{leads}', das keinen solchen Datensatz enthält."
        : $"Refers to '{leads}', which holds no such record.";
    public static string FollowKey(ReportLanguage l, string columns, string table, bool refersToNothing) => IsDe(l)
        ? $"{columns} folgen → '{table}'" + (refersToNothing ? " (verweist auf nichts)" : string.Empty)
        : $"Follow {columns} → '{table}'" + (refersToNothing ? " (refers to nothing)" : string.Empty);
    public static string ReferrersMenu(ReportLanguage l, string table) => IsDe(l)
        ? $"Datensätze in '{table}', die hierauf verweisen"
        : $"Records in '{table}' referring to this";

    // Navigation status
    public static string RecordOfReferring(ReportLanguage l, long index, long matches, string value) => IsDe(l)
        ? string.Create(CultureInfo.InvariantCulture, $"Datensatz {index} von {matches}, die auf '{value}' verweisen.")
        : string.Create(CultureInfo.InvariantCulture, $"Record {index} of {matches} referring to '{value}'.");
    public static string GoToRecordHeading(ReportLanguage l) => IsDe(l) ? "Gehe zu Datensatz" : "Go to record";
    public static string GoTo(ReportLanguage l) => IsDe(l) ? "Gehe zu" : "Go";
    public static string RecordNumberField(ReportLanguage l) => IsDe(l) ? "Datensatznummer" : "Record number";
    public static string RecordRange(ReportLanguage l, long count) => IsDe(l)
        ? string.Create(Numbers(l), $"Eine Datensatznummer von 1 bis {count:N0}, wie die Datei zählt.")
        : string.Create(Numbers(l), $"A record number from 1 to {count:N0}, as the file counts records.");
    public static string NoSuchRecord(ReportLanguage l, string typed, long count) => IsDe(l)
        ? string.Create(Numbers(l), $"'{typed}' ist keine Datensatznummer dieser Tabelle. Sie hat die Datensätze 1 bis {count:N0}.")
        : string.Create(Numbers(l), $"'{typed}' is not a record number of this table. It holds records 1 to {count:N0}.");
    public static string RecordReached(ReportLanguage l, long ordinal) => IsDe(l)
        ? string.Create(Numbers(l), $"Datensatz {ordinal:N0}.")
        : string.Create(Numbers(l), $"Record {ordinal:N0}.");
    public static string RecordFor(ReportLanguage l, long ordinal, string value) => IsDe(l)
        ? string.Create(CultureInfo.InvariantCulture, $"Datensatz {ordinal} für '{value}'.")
        : string.Create(CultureInfo.InvariantCulture, $"Record {ordinal} for '{value}'.");
    public static string ReferenceUnresolved(ReportLanguage l, string value, string table) => IsDe(l)
        ? $"'{value}' entspricht keinem Datensatz in '{table}'. Der Verweis lässt sich nicht auflösen."
        : $"'{value}' matches no record of '{table}'. The reference does not resolve.";
    public static string ReferenceUnreadable(ReportLanguage l, string table) => IsDe(l)
        ? $"'{table}' entspricht nicht seiner Deklaration und hat daher keine Daten. Dieser Verweis führt nirgendwohin."
        : $"'{table}' does not conform to its declaration, so it has no data to show. There is nowhere to follow this reference to.";
    public static string ReferenceNotReady(ReportLanguage l, string table) => IsDe(l)
        ? $"'{table}' wird noch eingelesen. Die Daten werden angezeigt, sobald das geschehen ist."
        : $"'{table}' is still being read. It will show its data as soon as it has been.";
    public static string NoReference(ReportLanguage l) => IsDe(l) ? "Dieser Datensatz verweist hier auf nichts." : "This record refers to nothing here.";
    public static string FilterRemoved(ReportLanguage l, long ordinal) => IsDe(l)
        ? string.Create(CultureInfo.InvariantCulture, $"Filter entfernt, um Datensatz {ordinal} anzuzeigen.")
        : string.Create(CultureInfo.InvariantCulture, $"Filter removed to show record {ordinal}.");

    public static string FindingsOf(ReportLanguage l, string table) => IsDe(l) ? $"Feststellungen zu '{table}'" : $"Findings about '{table}'";

    // Record names
    public static string RecordName(ReportLanguage l, long ordinal, string values) => IsDe(l)
        ? string.Create(Numbers(l), $"Datensatz {ordinal:N0}: {values}")
        : string.Create(Numbers(l), $"Record {ordinal:N0}: {values}");
    public static string RefersToNothingMark(ReportLanguage l) => IsDe(l) ? "verweist auf nichts" : "refers to nothing";

    // Editor fields
    public static string ColumnField(ReportLanguage l) => IsDe(l) ? "Spalte" : "Column";
    public static string ComparisonField(ReportLanguage l) => IsDe(l) ? "Vergleich" : "Comparison";
    public static string ValueField(ReportLanguage l) => IsDe(l) ? "Wert" : "Value";
    public static string SecondValueField(ReportLanguage l) => IsDe(l) ? "Zweiter Wert, bis zu dem verglichen wird" : "Second value, up to which to compare";
    public static string DirectionField(ReportLanguage l) => IsDe(l) ? "Richtung" : "Direction";
    public static string FigureField(ReportLanguage l) => IsDe(l) ? "Kennzahl" : "Figure";

    // Editors & Comparers
    public static string FilterHeading(ReportLanguage l) => "Filter";
    public static string SortHeading(ReportLanguage l) => IsDe(l) ? "Sortieren nach" : "Sort by";
    public static string FigureHeading(ReportLanguage l) => IsDe(l) ? "Kennzahl" : "Figure";
    public static string Ascending(ReportLanguage l) => IsDe(l) ? "aufsteigend" : "ascending";
    public static string Descending(ReportLanguage l) => IsDe(l) ? "absteigend" : "descending";
    public static string IgnoreCase(ReportLanguage l) => IsDe(l) ? "Groß-/Kleinschreibung ignorieren" : "ignore case";
    public static string Apply(ReportLanguage l) => IsDe(l) ? "Anwenden" : "Apply";
    public static string ValuePlaceholder(ReportLanguage l) => IsDe(l) ? "Wert" : "value";
    public static string AndPlaceholder(ReportLanguage l) => IsDe(l) ? "und" : "and";
    public static string AnyCase(ReportLanguage l) => IsDe(l) ? "(beliebige Schreibweise)" : "(any case)";
    public static string MatchesChip(ReportLanguage l) => IsDe(l) ? "entspricht" : "matches";
    public static string OneOfChip(ReportLanguage l) => IsDe(l) ? "eines von" : "one of";
    public static string ColumnNotQueryable(ReportLanguage l) => IsDe(l)
        ? "Dieser Leser kann diese Spalte nicht filtern, sortieren oder berechnen. Ihre Werte werden trotzdem angezeigt."
        : "This reader cannot filter, sort or total this column. Its values are still shown.";
    public static string Expects(ReportLanguage l, string expected) => IsDe(l) ? $"Erwartet: {expected}." : $"Expects {expected}.";
    public static string IsNot(ReportLanguage l, string typed, string expected) => IsDe(l)
        ? $"'{typed}' entspricht nicht der erwarteten Form: {expected}."
        : $"'{typed}' is not {expected}.";
    public static string ExpectsValues(ReportLanguage l) => IsDe(l)
        ? "Erwartet einen oder mehrere Werte, durch Kommas getrennt."
        : "Expects one or more values, separated by commas.";
    public static string ExpectsValue(ReportLanguage l) => IsDe(l) ? "Erwartet einen Vergleichswert." : "Expects a value to compare against.";
    public static string SortsFull(ReportLanguage l, int maximum) => IsDe(l)
        ? string.Create(CultureInfo.InvariantCulture, $"Bereits nach {maximum} Spalten sortiert. Entfernen Sie zuerst eine.")
        : string.Create(CultureInfo.InvariantCulture, $"Already sorted by {maximum} columns. Remove one first.");

    // Comparisons
    public static string EqualsOp(ReportLanguage l) => IsDe(l) ? "gleich" : "equals";
    public static string NotEqualsOp(ReportLanguage l) => IsDe(l) ? "ungleich" : "does not equal";
    public static string LessThanOp(ReportLanguage l) => IsDe(l) ? "kleiner als" : "less than";
    public static string AtMostOp(ReportLanguage l) => IsDe(l) ? "höchstens" : "at most";
    public static string GreaterThanOp(ReportLanguage l) => IsDe(l) ? "größer als" : "greater than";
    public static string AtLeastOp(ReportLanguage l) => IsDe(l) ? "mindestens" : "at least";
    public static string BetweenOp(ReportLanguage l) => IsDe(l) ? "zwischen" : "between";
    public static string ContainsOp(ReportLanguage l) => IsDe(l) ? "enthält" : "contains";
    public static string StartsWithOp(ReportLanguage l) => IsDe(l) ? "beginnt mit" : "starts with";
    public static string EndsWithOp(ReportLanguage l) => IsDe(l) ? "endet mit" : "ends with";
    public static string MatchesOp(ReportLanguage l) => IsDe(l) ? "entspricht (* und ?)" : "matches (* and ?)";
    public static string OneOfOp(ReportLanguage l) => IsDe(l) ? "eines von (kommagetrennt)" : "one of (comma separated)";
    public static string IsEmptyOp(ReportLanguage l) => IsDe(l) ? "ist leer" : "is empty";
    public static string IsNotEmptyOp(ReportLanguage l) => IsDe(l) ? "ist nicht leer" : "is not empty";

    // Figures
    public static string FigCount(ReportLanguage l) => IsDe(l) ? "Anzahl" : "count";
    public static string FigDistinct(ReportLanguage l) => IsDe(l) ? "eindeutige" : "distinct";
    public static string FigSum(ReportLanguage l) => IsDe(l) ? "Summe" : "sum";
    public static string FigAvg(ReportLanguage l) => IsDe(l) ? "Mittelwert" : "average";
    public static string FigMin(ReportLanguage l) => IsDe(l) ? "Min" : "min";
    public static string FigMax(ReportLanguage l) => IsDe(l) ? "Max" : "max";
    public static string FigureInexact(ReportLanguage l) => IsDe(l) ? "kann nicht exakt berechnet werden" : "cannot be computed exactly";
    public static string Rounded(ReportLanguage l) => IsDe(l) ? "(gerundet)" : "(rounded)";
    public static string WithoutValue(ReportLanguage l, long count) => IsDe(l)
        ? string.Create(Numbers(l), $"{count:N0} ohne Wert")
        : string.Create(Numbers(l), $"{count:N0} without a value");

    // Menus
    public static string FileMenu(ReportLanguage l) => IsDe(l) ? "Datei" : "File";
    public static string ViewMenu(ReportLanguage l) => IsDe(l) ? "Ansicht" : "View";
    public static string GoMenu(ReportLanguage l) => IsDe(l) ? "Gehe zu" : "Go";
    public static string TableMenu(ReportLanguage l) => IsDe(l) ? "Tabelle" : "Table";
    public static string CloseTabMenu(ReportLanguage l) => IsDe(l) ? "Tab schließen" : "Close Tab";
    public static string ZoomIn(ReportLanguage l) => IsDe(l) ? "Vergrößern" : "Zoom In";
    public static string ZoomOut(ReportLanguage l) => IsDe(l) ? "Verkleinern" : "Zoom Out";
    public static string ActualSize(ReportLanguage l) => IsDe(l) ? "Originalgröße" : "Actual Size";
    public static string NavigatorName(ReportLanguage l) => IsDe(l) ? "Navigator: Medien und Tabellen des Exports" : "Navigator: the export's media and tables";
    public static string NavigatorWidth(ReportLanguage l) => IsDe(l) ? "Breite des Navigators" : "Navigator width";
    public static string ShowNavigator(ReportLanguage l) => IsDe(l) ? "Navigator anzeigen" : "Show Navigator";
    public static string NextTab(ReportLanguage l) => IsDe(l) ? "Nächster Tab" : "Next Tab";
    public static string PreviousTab(ReportLanguage l) => IsDe(l) ? "Vorheriger Tab" : "Previous Tab";
    public static string Back(ReportLanguage l) => IsDe(l) ? "Zurück" : "Back";
    public static string Forward(ReportLanguage l) => IsDe(l) ? "Vorwärts" : "Forward";
    public static string GoToRecordMenu(ReportLanguage l) => IsDe(l) ? "Gehe zu Datensatz…" : "Go to Record…";
    public static string NextReferringMenu(ReportLanguage l) => IsDe(l) ? "Nächster verweisender Datensatz" : "Next Referring Record";
    public static string PreviousReferringMenu(ReportLanguage l) => IsDe(l) ? "Vorheriger verweisender Datensatz" : "Previous Referring Record";
    public static string AddFilterMenu(ReportLanguage l) => IsDe(l) ? "Filter hinzufügen…" : "Add Filter…";
    public static string AddSortMenu(ReportLanguage l) => IsDe(l) ? "Sortierspalte hinzufügen…" : "Add Sort Column…";
    public static string AddFigureMenu(ReportLanguage l) => IsDe(l) ? "Kennzahl hinzufügen…" : "Add Figure…";
    public static string FileOrderMenu(ReportLanguage l) => IsDe(l) ? "Zurück zur Dateireihenfolge" : "Back to File Order";
    public static string KeyboardShortcuts(ReportLanguage l) => IsDe(l) ? "Tastenkombinationen" : "Keyboard Shortcuts";
    public static string NextArea(ReportLanguage l) => IsDe(l) ? "Zum nächsten Bereich" : "Move to the next area";
    public static string PreviousArea(ReportLanguage l) => IsDe(l) ? "Zum vorherigen Bereich" : "Move to the previous area";
    public static string TabAt(ReportLanguage l, int place) => IsDe(l)
        ? string.Create(CultureInfo.InvariantCulture, $"Tab {place} nach vorn holen (1 ist die Übersicht)")
        : string.Create(CultureInfo.InvariantCulture, $"Bring tab {place} to the front (1 is the summary)");
    public static string RecordActions(ReportLanguage l) => IsDe(l) ? "Aktionen für den Datensatz" : "Actions for the record";
    public static string CopyRecord(ReportLanguage l) => IsDe(l) ? "Datensatz kopieren" : "Copy Record";
    public static string ScrollLeft(ReportLanguage l) => IsDe(l) ? "Nach links blättern" : "Scroll left";
    public static string ScrollRight(ReportLanguage l) => IsDe(l) ? "Nach rechts blättern" : "Scroll right";
    public static string OpenTable(ReportLanguage l) => IsDe(l) ? "Tabelle öffnen" : "Open the table";
    public static string TabsByPlace(ReportLanguage l) => IsDe(l)
        ? "Den Tab an dieser Stelle nach vorn holen (1 ist die Übersicht)"
        : "Bring the tab in that place to the front (1 is the summary)";
    public static string ShortcutsEverywhere(ReportLanguage l) => IsDe(l) ? "Überall" : "Everywhere";
    public static string ShortcutsInRecords(ReportLanguage l) => IsDe(l) ? "In den Datensätzen einer Tabelle" : "In a table's records";
    public static string ShortcutsInNavigator(ReportLanguage l) => IsDe(l) ? "Im Navigator" : "In the navigator";
    public static string ShortcutsDialogs(ReportLanguage l) => IsDe(l)
        ? "Escape schließt einen Editor oder ein Fenster. Ein Menü öffnet sich unter Windows und Linux mit Alt und dem unterstrichenen Buchstaben."
        : "Escape closes an editor or a window. On Windows and Linux, Alt with the underlined letter opens a menu.";
    public static string OpenArchive(ReportLanguage l) => IsDe(l) ? "Archiv öffnen…" : "Open Archive…";
    public static string OpenFolder(ReportLanguage l) => IsDe(l) ? "Ordner öffnen…" : "Open Folder…";
    public static string SettingsMenu(ReportLanguage l) => IsDe(l) ? "Einstellungen…" : "Settings…";
    public static string HelpMenu(ReportLanguage l) => IsDe(l) ? "Hilfe" : "Help";
    public static string Licence(ReportLanguage l) => IsDe(l) ? "Lizenz" : "Licence";
    public static string Notice(ReportLanguage l) => IsDe(l) ? "Hinweis" : "Notice";
    public static string ThirdPartyNotices(ReportLanguage l) => IsDe(l) ? "Drittanbieter-Hinweise" : "Third-party notices";
    public static string About(ReportLanguage l) => IsDe(l) ? "Über GoBD Reader" : "About GoBD Reader";
    public static string AboutDescription(ReportLanguage l) => IsDe(l)
        ? "Liest GoBD/GDPdU-Datenträgerexporte nach dem Beschreibungsstandard 1.6."
        : "Reads GoBD/GDPdU data carrier exports, as described by Beschreibungsstandard 1.6.";
    public static string MitLicence(ReportLanguage l) => IsDe(l) ? "MIT-Lizenz" : "MIT licence";
    public static string ProjectPage(ReportLanguage l) => IsDe(l) ? "Projektseite" : "Project page";

    // Settings Window
    public static string AppearanceSection(ReportLanguage l) => IsDe(l) ? "Erscheinungsbild" : "Appearance";
    public static string ThemeLabel(ReportLanguage l) => IsDe(l) ? "Design:" : "Theme:";
    public static string ThemeSystem(ReportLanguage l) => IsDe(l) ? "Systemstandard" : "System default";
    public static string ThemeLight(ReportLanguage l) => IsDe(l) ? "Hell" : "Light";
    public static string ThemeDark(ReportLanguage l) => IsDe(l) ? "Dunkel" : "Dark";
    public static string ThemeHighContrastDark(ReportLanguage l) => IsDe(l) ? "Hoher Kontrast (dunkel)" : "High contrast (dark)";
    public static string ThemeHighContrastLight(ReportLanguage l) => IsDe(l) ? "Hoher Kontrast (hell)" : "High contrast (light)";
    public static string LanguageSection(ReportLanguage l) => IsDe(l) ? "Sprache" : "Language";
    public static string DisplayLanguageLabel(ReportLanguage l) => IsDe(l) ? "Anzeigesprache:" : "Display Language:";
    public static string LangEnglish(ReportLanguage l) => IsDe(l) ? "Englisch" : "English";
    public static string LangGerman(ReportLanguage l) => "Deutsch";
    public static string ZoomLabel(ReportLanguage l) => IsDe(l) ? "Vergrößerung:" : "Zoom:";
    public static string ZoomSelection(ReportLanguage l) => IsDe(l) ? "Auswahl der Vergrößerung" : "Zoom selection";
    public static string Percent(ReportLanguage l, int percent) => IsDe(l)
        ? string.Create(CultureInfo.InvariantCulture, $"{percent} %")
        : string.Create(CultureInfo.InvariantCulture, $"{percent}%");
    public static string ThemeSelection(ReportLanguage l) => IsDe(l) ? "Designauswahl" : "Theme selection";
    public static string LanguageSelection(ReportLanguage l) => IsDe(l) ? "Sprachauswahl" : "Language selection";
    public static string CloseSettings(ReportLanguage l) => IsDe(l) ? "Einstellungen schließen" : "Close settings";
}
