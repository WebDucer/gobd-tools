using System.Collections.Frozen;
using System.Globalization;
using GoBd.Validation.Findings;

namespace GoBd.Validation.Localisation;

/// <summary>An English and a German rendering of one finding code.</summary>
/// <param name="English">English template.</param>
/// <param name="German">German template.</param>
internal sealed record MessageTemplates(string English, string German)
{
    /// <summary>The template for the given language.</summary>
    internal string For(ReportLanguage language) =>
        language == ReportLanguage.German ? German : English;
}

/// <summary>
/// Renders a finding's message from its code and arguments.
/// </summary>
/// <remarks>
/// The catalogue is compiled-in data rather than satellite assemblies: satellites are separate
/// files on disk, which would defeat the single-binary goal and does not fit NativeAOT cleanly.
/// Substitution is explicitly invariant, so only the prose differs between languages — every
/// value quoted from index.xml is reproduced exactly as declared. See design.md D8.
/// </remarks>
public static class MessageCatalogue
{
    private static readonly FrozenDictionary<string, MessageTemplates> Templates = new Dictionary<string, MessageTemplates>(StringComparer.Ordinal)
    {
        [FindingCodes.IndexMissing] = new(
            "The export at '{0}' contains no index.xml at its root.",
            "Der Export unter '{0}' enthält keine index.xml im Stammverzeichnis."),
        [FindingCodes.IndexNotAtRoot] = new(
            "index.xml must be at the root of the export at '{0}', but was found at '{1}'.",
            "index.xml muss im Stammverzeichnis des Exports unter '{0}' liegen, wurde aber unter '{1}' gefunden."),
        [FindingCodes.TableFileMissing] = new(
            "Table '{0}' declares '{1}', which is not present in the export.",
            "Tabelle '{0}' verweist auf '{1}', das im Export nicht vorhanden ist."),
        [FindingCodes.UrlNotRelative] = new(
            "Table '{0}' declares the absolute URL '{1}'; the standard permits only relative URLs.",
            "Tabelle '{0}' verwendet die absolute URL '{1}'; der Standard erlaubt nur relative URLs."),
        [FindingCodes.UrlEscapesRoot] = new(
            "Table '{0}' declares '{1}', which resolves outside the export root.",
            "Tabelle '{0}' verweist auf '{1}', was außerhalb des Export-Stammverzeichnisses liegt."),
        [FindingCodes.EntryCaseMismatch] = new(
            "Table '{0}' declares '{1}', but the export contains '{2}'; the names differ only in case and will not resolve on a case-sensitive filesystem.",
            "Tabelle '{0}' verweist auf '{1}', der Export enthält jedoch '{2}'; die Namen unterscheiden sich nur in der Groß-/Kleinschreibung und lassen sich auf einem Dateisystem mit Groß-/Kleinschreibung nicht auflösen."),
        [FindingCodes.EntryNameCollision] = new(
            "The export contains {1} entries that resolve to the name '{0}'. It is undefined which one an importer reads, so the medium cannot be interpreted unambiguously.",
            "Der Export enthält {1} Einträge, die sich zum Namen '{0}' auflösen. Es ist undefiniert, welchen ein Importprogramm liest, sodass der Datenträger nicht eindeutig interpretierbar ist."),
        [FindingCodes.XmlNotWellFormed] = new(
            "index.xml is not well formed: {0}",
            "index.xml ist nicht wohlgeformt: {0}"),
        [FindingCodes.GrammarViolation] = new(
            "index.xml violates the Beschreibungsstandard 1.6 grammar: {0}",
            "index.xml verletzt die Grammatik des Beschreibungsstandards 1.6: {0}"),
        [FindingCodes.DtdMissing] = new(
            "The export ships no DTD file; the data carrier must contain '{0}'.",
            "Der Export enthält keine DTD-Datei; der Datenträger muss '{0}' enthalten."),
        [FindingCodes.DtdNormalisationDifference] = new(
            "The DTD '{0}' in this export differs from the canonical grammar in {1}. Expected SHA-256 {2}, found {3}. The standard requires the shipped DTD to be byte-identical to the original.",
            "Die DTD '{0}' in diesem Export unterscheidet sich von der kanonischen Grammatik in {1}. Erwartet wurde SHA-256 {2}, gefunden {3}. Der Standard verlangt, dass die mitgelieferte DTD mit dem Original bytegleich ist."),
        [FindingCodes.DtdAltered] = new(
            "The DTD '{0}' in this export has been modified: expected SHA-256 {1}, found {2}. The standard forbids changing the grammar.",
            "Die DTD '{0}' in diesem Export wurde verändert: erwartet wurde SHA-256 {1}, gefunden {2}. Der Standard verbietet Änderungen an der Grammatik."),
        [FindingCodes.DtdLegacySystemIdentifier] = new(
            "The document type declaration names '{0}'; validation used the canonical 1.6 grammar.",
            "Die Dokumenttyp-Deklaration nennt '{0}'; validiert wurde gegen die kanonische Grammatik 1.6."),
        [FindingCodes.ExternalReferenceBlocked] = new(
            "Refused to resolve the external reference '{0}'.",
            "Die externe Referenz '{0}' wurde nicht aufgelöst."),
        [FindingCodes.EntityExpansionExceeded] = new(
            "Entity expansion exceeded the permitted budget: {0}",
            "Die Entity-Expansion hat das zulässige Limit überschritten: {0}"),
        [FindingCodes.ForeignKeyColumnUndeclared] = new(
            "Table '{0}': foreign key column '{1}' referencing '{2}' is not declared in this table.",
            "Tabelle '{0}': Die Fremdschlüsselspalte '{1}' mit Verweis auf '{2}' ist in dieser Tabelle nicht deklariert."),
        [FindingCodes.ReferenceDangling] = new(
            "Table '{0}' references '{1}', which is not a table in this data set.",
            "Tabelle '{0}' verweist auf '{1}', das in diesem Datensatz keine Tabelle ist."),
        [FindingCodes.ReferenceAmbiguous] = new(
            "Table '{0}' references '{1}', which matches more than one table: {2}.",
            "Tabelle '{0}' verweist auf '{1}', was auf mehrere Tabellen zutrifft: {2}."),
        [FindingCodes.ReferencedTableHasNoPrimaryKey] = new(
            "Table '{0}' references '{1}', which declares no primary key to join to.",
            "Tabelle '{0}' verweist auf '{1}', das keinen Primärschlüssel für die Verknüpfung deklariert."),
        [FindingCodes.ForeignKeyArityMismatch] = new(
            "Table '{0}' references '{1}' with {2} column(s), but that table's primary key has {3}.",
            "Tabelle '{0}' verweist mit {2} Spalte(n) auf '{1}', dessen Primärschlüssel jedoch {3} umfasst."),
        [FindingCodes.ForeignKeyDatatypeMismatch] = new(
            "Table '{0}': column '{1}' is {2}, but joins to '{3}'.'{4}' which is {5}.",
            "Tabelle '{0}': Spalte '{1}' ist {2}, verknüpft aber mit '{3}'.'{4}' vom Typ {5}."),
        [FindingCodes.AliasFromUnknown] = new(
            "Table '{0}': alias source '{1}' is not a column of the foreign key referencing '{2}'.",
            "Tabelle '{0}': Die Alias-Quelle '{1}' ist keine Spalte des Fremdschlüssels mit Verweis auf '{2}'."),
        [FindingCodes.AliasToNotPrimaryKey] = new(
            "Table '{0}': alias target '{1}' is not a primary key column of '{2}'.",
            "Tabelle '{0}': Das Alias-Ziel '{1}' ist keine Primärschlüsselspalte von '{2}'."),
        [FindingCodes.AliasTargetDuplicated] = new(
            "Table '{0}': two aliases both map onto '{1}' of '{2}'.",
            "Tabelle '{0}': Zwei Aliase verweisen beide auf '{1}' von '{2}'."),
        [FindingCodes.AliasPartial] = new(
            "Table '{0}': the foreign key referencing '{1}' aliases {2} of {3} columns; the rest fall back to positional matching.",
            "Tabelle '{0}': Der Fremdschlüssel mit Verweis auf '{1}' aliasiert {2} von {3} Spalten; die übrigen werden positionsbasiert zugeordnet."),
        [FindingCodes.TimeMapMasksDiffer] = new(
            "Table '{0}': column '{1}' maps '{2}' to '{3}'. Both are time masks but differ, so this is a value substitution rather than a Time declaration.",
            "Tabelle '{0}': Spalte '{1}' bildet '{2}' auf '{3}' ab. Beides sind Zeitmasken, die sich jedoch unterscheiden, sodass eine Wertersetzung statt einer Zeit-Deklaration vorliegt."),
        [FindingCodes.MapOnNonAlphanumeric] = new(
            "Table '{0}': column '{1}' is {2}, but Map is permitted only on alphanumeric columns.",
            "Tabelle '{0}': Spalte '{1}' ist {2}; Map ist nur für alphanumerische Spalten zulässig."),
        [FindingCodes.MediaWithoutTables] = new(
            "Medium '{0}' declares no table and no AcceptNoTables, so it describes no data.",
            "Medium '{0}' deklariert weder eine Tabelle noch AcceptNoTables und beschreibt somit keine Daten."),
        [FindingCodes.MediaWithoutTablesAccepted] = new(
            "Medium '{0}' deliberately declares no table.",
            "Medium '{0}' deklariert bewusst keine Tabelle."),
        [FindingCodes.FixedRangeInvalid] = new(
            "Table '{0}': column '{1}' declares the unusable span value '{2}'.",
            "Tabelle '{0}': Spalte '{1}' deklariert den unbrauchbaren Bereichswert '{2}'."),
        [FindingCodes.FixedRangeOverlap] = new(
            "Table '{0}': columns '{1}' and '{2}' claim overlapping positions.",
            "Tabelle '{0}': Die Spalten '{1}' und '{2}' beanspruchen überlappende Positionen."),
        [FindingCodes.FixedRangeGap] = new(
            "Table '{0}': positions {1} to {2} are not claimed by any column.",
            "Tabelle '{0}': Die Positionen {1} bis {2} werden von keiner Spalte beansprucht."),
        [FindingCodes.FixedRangeExceedsRecordLength] = new(
            "Table '{0}': column '{1}' ends at position {2}, beyond the declared record length of {3}.",
            "Tabelle '{0}': Spalte '{1}' endet an Position {2} und damit jenseits der deklarierten Satzlänge von {3}."),
        [FindingCodes.NumericSettingInvalid] = new(
            "Table '{0}': {1} declares '{2}', which is not a usable non-negative integer.",
            "Tabelle '{0}': {1} deklariert '{2}', was keine brauchbare nicht-negative ganze Zahl ist."),
        [FindingCodes.SymbolCollision] = new(
            "Table '{0}' declares '{1}' as both the decimal and the digit grouping symbol.",
            "Tabelle '{0}' deklariert '{1}' sowohl als Dezimal- als auch als Tausendertrennzeichen."),
        [FindingCodes.DelimiterCollision] = new(
            "Table '{0}' declares the same character for {1} and {2}.",
            "Tabelle '{0}' deklariert dasselbe Zeichen für {1} und {2}."),
        [FindingCodes.DateMaskUnusable] = new(
            "Table '{0}', {1}: the date mask '{2}' has no usable year, month and day placeholders.",
            "Tabelle '{0}', {1}: Die Datumsmaske '{2}' enthält keine brauchbaren Platzhalter für Jahr, Monat und Tag."),
        [FindingCodes.RecordRangeInvalid] = new(
            "Table '{0}' declares a record range starting at '{1}'; records are counted from one.",
            "Tabelle '{0}' deklariert einen Satzbereich ab '{1}'; Sätze werden ab eins gezählt."),
        [FindingCodes.TableNameDuplicated] = new(
            "The name '{0}' is declared by more than one table: {1}.",
            "Der Name '{0}' wird von mehreren Tabellen deklariert: {1}."),
        [FindingCodes.TableUrlDuplicated] = new(
            "The URL '{0}' is described by {1} tables.",
            "Die URL '{0}' wird von {1} Tabellen beschrieben."),
        [FindingCodes.ColumnNameDuplicated] = new(
            "Table '{0}' declares the column name '{1}' more than once.",
            "Tabelle '{0}' deklariert den Spaltennamen '{1}' mehrfach."),
        [FindingCodes.ReservedCharacterInName] = new(
            "Table '{0}': the name '{1}' contains the reserved character(s) {2}.",
            "Tabelle '{0}': Der Name '{1}' enthält die reservierten Zeichen {2}."),
        [FindingCodes.DescriptionTooLong] = new(
            "Table '{0}' declares a description of {1} characters; the standard advises at most 255.",
            "Tabelle '{0}' deklariert eine Beschreibung mit {1} Zeichen; der Standard empfiehlt höchstens 255."),
        [FindingCodes.CommandDeclared] = new(
            "{0} declares the command '{1}'. It was reported only and never executed.",
            "{0} deklariert den Befehl '{1}'. Er wurde nur gemeldet und niemals ausgeführt."),
        [FindingCodes.ExtensionDeclared] = new(
            "Extension '{0}' is declared, supplied by '{1}'.",
            "Die Erweiterung '{0}' ist deklariert und wird von '{1}' bereitgestellt."),
        [FindingCodes.ExtensionFileMissing] = new(
            "Extension '{0}' names '{1}', which is not present in the export.",
            "Die Erweiterung '{0}' verweist auf '{1}', das im Export nicht vorhanden ist."),
        [FindingCodes.RecordValueTypeMismatch] = new(
            "Table '{0}', record {1}, column '{2}': '{3}' does not match the declared {4}.",
            "Tabelle '{0}', Datensatz {1}, Spalte '{2}': '{3}' entspricht nicht der Deklaration {4}."),
        [FindingCodes.RecordValueAccuracyExceeded] = new(
            "Table '{0}', record {1}, column '{2}': '{3}' carries more decimal places than the declared accuracy of {4}.",
            "Tabelle '{0}', Datensatz {1}, Spalte '{2}': '{3}' hat mehr Nachkommastellen als die deklarierte Genauigkeit {4}."),
        [FindingCodes.RecordValueTooLong] = new(
            "Table '{0}', record {1}, column '{2}': '{3}' is longer than the declared maximum length of {4}.",
            "Tabelle '{0}', Datensatz {1}, Spalte '{2}': '{3}' ist länger als die deklarierte Maximallänge {4}."),
        [FindingCodes.RecordColumnCountMismatch] = new(
            "Table '{0}', record {1}: {2} columns were read, but {3} are declared.",
            "Tabelle '{0}', Datensatz {1}: {2} Spalten wurden gelesen, deklariert sind {3}."),
        [FindingCodes.RecordBytesUndecodable] = new(
            "Table '{0}', record {1}, column '{2}': bytes could not be decoded as {3}.",
            "Tabelle '{0}', Datensatz {1}, Spalte '{2}': Bytes konnten nicht als {3} dekodiert werden."),
        [FindingCodes.RecordEncapsulatorUnterminated] = new(
            "Table '{0}', record {1}, column '{2}': the text encapsulator '{3}' was opened and never closed.",
            "Tabelle '{0}', Datensatz {1}, Spalte '{2}': Der Textbegrenzer '{3}' wurde geöffnet und nie geschlossen."),
        [FindingCodes.RecordLengthMismatch] = new(
            "Table '{0}', record {1}: the record is {2} characters long, but {3} are declared.",
            "Tabelle '{0}', Datensatz {1}: Der Datensatz ist {2} Zeichen lang, deklariert sind {3}."),
        [FindingCodes.TableAnalysisStopped] = new(
            "Table '{0}': analysis stopped after {1} findings. The table may hold further defects that were not looked for.",
            "Tabelle '{0}': Die Analyse wurde nach {1} Befunden abgebrochen. Die Tabelle kann weitere Mängel enthalten, nach denen nicht gesucht wurde."),
        [FindingCodes.HeaderOrderDiffers] = new(
            "Table '{0}': the excluded first record carries the declared column names in a different order. Declared: {1}. Found: {2}. Every value is therefore read into the wrong column.",
            "Tabelle '{0}': Der ausgeschlossene erste Datensatz enthält die deklarierten Spaltennamen in abweichender Reihenfolge. Deklariert: {1}. Gefunden: {2}. Jeder Wert wird dadurch der falschen Spalte zugeordnet."),
        [FindingCodes.HeaderUndeclared] = new(
            "Table '{0}': record 1 carries the declared column names ({1}), but the declaration excludes no record, so that row will be read as data.",
            "Tabelle '{0}': Datensatz 1 enthält die deklarierten Spaltennamen ({1}), die Deklaration schließt jedoch keinen Datensatz aus, sodass diese Zeile als Daten gelesen wird."),
        [FindingCodes.TableNotReadable] = new(
            "Table '{0}': the declaration could not be turned into a reading of '{1}' ({2}), so its contents were not checked.",
            "Tabelle '{0}': Aus der Deklaration ließ sich keine Lesart von '{1}' ableiten ({2}); der Inhalt wurde daher nicht geprüft."),
        [FindingCodes.PrimaryKeyDuplicated] = new(
            "Table '{0}': the primary key '{1}' occurs in records {2}. Every reference to it is ambiguous.",
            "Tabelle '{0}': Der Primärschlüssel '{1}' kommt in den Datensätzen {2} vor. Jede Referenz darauf ist mehrdeutig."),
        [FindingCodes.PrimaryKeyIncomplete] = new(
            "Table '{0}', record {1}: the declared primary key is empty or only partly present ('{2}').",
            "Tabelle '{0}', Datensatz {1}: Der deklarierte Primärschlüssel ist leer oder nur teilweise vorhanden ('{2}')."),
        [FindingCodes.ForeignKeyValueUnresolved] = new(
            "Table '{0}', record {1}: '{2}' matches no primary key of the referenced table '{3}'.",
            "Tabelle '{0}', Datensatz {1}: '{2}' entspricht keinem Primärschlüssel der referenzierten Tabelle '{3}'."),
        [FindingCodes.ReferenceNotChecked] = new(
            "Table '{0}': the reference to '{1}' could not be checked, because that table could not be read.",
            "Tabelle '{0}': Die Referenz auf '{1}' konnte nicht geprüft werden, da diese Tabelle nicht gelesen werden konnte."),
        [FindingCodes.ExportPathNotFound] = new(
            "The export path '{0}' does not exist.",
            "Der Exportpfad '{0}' existiert nicht."),
        [FindingCodes.ExportUnreadable] = new(
            "The export at '{0}' could not be read: {1}",
            "Der Export unter '{0}' konnte nicht gelesen werden: {1}"),
        [FindingCodes.LanguageUnsupported] = new(
            "The report language '{0}' is not available; English was used.",
            "Die Berichtssprache '{0}' ist nicht verfügbar; es wurde Englisch verwendet."),
        [FindingCodes.KeyCheckCapacityExceeded] = new(
            "Table '{0}' holds more than {1} records, which is more keys than this build can hold in memory. Its keys and the references to it were not checked.",
            "Tabelle '{0}' enthält mehr als {1} Datensätze und damit mehr Schlüssel, als dieser Build im Speicher halten kann. Ihre Schlüssel und die Referenzen darauf wurden nicht geprüft."),
        [FindingCodes.EmbeddedGrammarNotCanonical] = new(
            "This validator's embedded grammar is not the canonical one: expected SHA-256 {0}, found {1}. No verdict can be trusted, so validation was not performed.",
            "Die in diesem Validator eingebettete Grammatik ist nicht die kanonische: erwartet wurde SHA-256 {0}, gefunden {1}. Da kein Ergebnis verlässlich wäre, wurde nicht validiert."),
        [FindingCodes.CheckFailed] = new(
            "The check '{0}' failed unexpectedly: {1}",
            "Die Prüfung '{0}' ist unerwartet fehlgeschlagen: {1}"),
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>
    /// Marks an argument as prose the catalogue owns, rather than a value copied from the
    /// document. Several tokens may travel in one argument, separated by <c>.</c>.
    /// </summary>
    private const char TokenMarker = '@';

    /// <summary>
    /// Wording chosen by a check rather than copied from <c>index.xml</c>.
    /// </summary>
    /// <remarks>
    /// A check must not know the report language, so it emits a stable token and the wording
    /// lives here beside the rest of the wording. See design.md D6c.
    /// </remarks>
    private static readonly FrozenDictionary<string, MessageTemplates> Phrases = new Dictionary<string, MessageTemplates>(StringComparer.Ordinal)
    {
        ["lineEndings"] = new("line endings", "Zeilenenden"),
        ["byteOrderMark"] = new("byte-order mark", "Byte-Order-Mark"),
        ["trailingWhitespace"] = new("trailing whitespace", "abschließende Leerzeichen"),
        ["encoding"] = new("encoding", "Kodierung"),
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>Renders the finding's message in the given language.</summary>
    public static string Render(Finding finding, ReportLanguage language)
    {
        ArgumentNullException.ThrowIfNull(finding);
        var template = Templates[finding.Code].For(language);
        if (finding.Arguments.Count == 0)
        {
            return template;
        }

        var arguments = finding.Arguments.Select(argument => Localise(argument, language)).ToArray();
        return string.Format(CultureInfo.InvariantCulture, template, arguments);
    }

    /// <summary>Builds a token argument from one or more phrase names.</summary>
    public static string Token(params string[] names) => TokenMarker + string.Join('.', names);

    private static string Localise(string argument, ReportLanguage language)
    {
        if (argument.Length < 2 || argument[0] != TokenMarker)
        {
            return argument;
        }

        // The marker alone is not enough to claim an argument is ours. Document values really do
        // begin with '@' -- a Command of "@echo off", a Map placeholder, a column named "@id" --
        // and rewriting one would break the guarantee that values quoted from index.xml are
        // reproduced exactly as declared. So every part must be a phrase this catalogue knows;
        // otherwise the argument is someone else's and is passed through untouched.
        var names = argument[1..].Split('.');
        if (!names.All(Phrases.ContainsKey))
        {
            return argument;
        }

        return string.Join(", ", names.Select(name => Phrases[name].For(language)));
    }

    /// <summary>True when the catalogue carries a rendering for the code.</summary>
    public static bool Contains(string code) => Templates.ContainsKey(code);

    /// <summary>Every code the catalogue can render.</summary>
    public static IReadOnlyCollection<string> Codes => Templates.Keys;
}
