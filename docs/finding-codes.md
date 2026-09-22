# Finding codes

Every code `gobd-validate` and `gobd-reader` can report, what it means, and what to change.

Codes are the tools' stable interface: they are what a pipeline suppresses by and what you search
for when a build fails. A code is never reused for a different check.

| Range                                                          | Concerns                                            | Codes |
| -------------------------------------------------------------- | --------------------------------------------------- | ----- |
| [`GOBD1xxx`](#gobd1xxx--the-export-as-a-package)               | The export as a package: files, paths, presence     | 6     |
| [`GOBD2xxx`](#gobd2xxx--the-dtd-and-the-grammar)               | `index.xml` against the grammar, and the DTD itself | 8     |
| [`GOBD3xxx`](#gobd3xxx--what-indexxml-describes)               | Whether the description is internally coherent      | 31    |
| [`GOBD4xxx`](#gobd4xxx--what-the-data-files-hold)              | What a data file holds, against its declared layout | 11    |
| [`GOBD5xxx`](#gobd5xxx--keys-and-the-references-built-on-them) | Declared keys, and whether the references resolve   | 4     |
| [`GOBD9xxx`](#gobd9xxx--the-validator-itself)                  | Failures of the validator, not of your export       | 6     |

**Severities.** `error` means the standard is violated. `warning` means the export is permitted
but suspect. `info` is a neutral observation implying no defect. Severity reflects conformance,
never how confident the check is — a check that is unsure emits nothing.

**Exit codes.** `0` conformant · `1` warnings only · `2` errors present, or warnings under
`--strict` · `3` the validator could not run. A `GOBD9xxx` error always produces `3`.

**Scope.** `GOBD1xxx` to `GOBD3xxx` read `index.xml`, the DTD and the export's file listing, and
open no data file. `GOBD4xxx` and `GOBD5xxx` read the data files themselves. The validator reports them only
with `--contents`, and without it a clean result says nothing about what your CSV or
fixed-length files contain. The reader always reads the data files.

---

## GOBD1xxx — the export as a package

### GOBD1001 — index.xml missing from the export root

**Severity:** error **Category:** source

The export contains no `index.xml` at its root. Nothing can be validated: `index.xml` is the
description that everything else is checked against.

**Triggers when** neither a ZIP archive nor a folder has `index.xml` directly at the top level.

**Why an error:** the standard requires the data carrier to carry the description file. Without
it the medium is not a GoBD export at all.

**What to change:** confirm the export completed. If your tooling writes into a subdirectory and
zips the parent, see `GOBD1002`, which is reported instead when a nested `index.xml` is found.

### GOBD1002 — index.xml is not at the export root

**Severity:** error **Category:** source

An `index.xml` exists, but beneath a subdirectory. The finding names where it was found.

**Triggers when** no `index.xml` sits at the root and at least one is present deeper in the tree.

**Why an error:** the standard requires it at the root, and an importer looking there finds
nothing. This is reported separately from `GOBD1001` because it is a different mistake with a
different fix.

**What to change:** zip the _contents_ of the export directory, not the directory itself. This is
the usual cause: `zip -r export.zip export/` produces `export/index.xml`, one level too deep.

### GOBD1003 — a declared table file is absent

**Severity:** error **Category:** source

A `Table/URL` names a file that is not in the export.

**Triggers when** the URL resolves cleanly but no entry with that name exists.

**Why an error:** the standard states the description must match the delivered data, and an
import will stop on the missing file.

**What to change:** either ship the file or remove the `Table` element. A common cause is an
export that skipped an empty table while still describing it.

### GOBD1004 — a URL is absolute

**Severity:** error **Category:** source

A `Table/URL` uses an absolute form. Only relative URLs are permitted.

**Triggers when** the URL begins with a scheme (`http:`, `ftp:`, `file:`), a slash, or a drive
letter. The standard lists `http://…`, `ftp://…`, `file://localhost/…` and `file:///…` as
explicitly invalid.

**Why an error:** an absolute path is meaningless on the auditor's machine. The medium must be
self-contained.

**What to change:** make the URL relative to `index.xml` — `Accounts.dat`, `data/Accounts.dat`.

### GOBD1005 — a URL resolves outside the export root

**Severity:** error **Category:** source

A relative URL climbs out of the export.

**Triggers when** `../` segments ascend past the root. `data/../kunden.csv` is fine; `../kunden.csv`
is not, because `index.xml` sits at the root.

**Why an error:** the file is not on the medium, so it will not reach the auditor.

**What to change:** move the file inside the export and adjust the URL.

### GOBD1006 — an entry matches only when case is ignored

**Severity:** error **Category:** source

The declared URL does not match any entry, but one differs only in letter case. The finding names
the near miss.

**Triggers when** `Table/URL` says `Accounts.csv` and the export contains `accounts.csv`.

**Why an error:** the export works only on a case-insensitive filesystem. The auditor's
environment is not yours, and on Linux the import simply fails to find the file.

**What to change:** make the declared URL match the file name exactly, or rename the file.

### GOBD1007 — two entries resolve to the same name

**Severity:** error   **Category:** source

Two files in the export end up with the same name, so it is undefined which one describes a
declared table. The finding names the collision and how many entries share it.

**Triggers when** two entries normalise to one name. Two ways this happens in practice: a folder
holding `data/x.csv` beside a file whose literal name is `data\x.csv` — a backslash is a legal
filename character on Linux and macOS — or a ZIP archive carrying the same member name twice,
which the format permits.

**Why an error:** the medium cannot be interpreted unambiguously. Different importers resolve the
duplicate differently, so two auditors can reach two conclusions from the same data carrier.
Duplicate ZIP members are also a known way to make one tool read different bytes than another.

**What to change:** remove the duplicate. If a filename genuinely contains a backslash, rename it
— the standard's URLs use `/` as the separator, so such a name cannot be referenced anyway.

---

## GOBD2xxx — the DTD and the grammar

### GOBD2001 — index.xml is not well formed

**Severity:** error **Category:** grammar

The document is not valid XML. Validation stops here: there is no tree to reason about, so no
other finding is reported.

**Triggers when** a tag is unclosed, an attribute unquoted, or the encoding declaration does not
match the bytes.

**Why an error:** nothing can be read from it.

**What to change:** the finding carries the parser's message with a line and column. Note that
`"`, `&`, `<` and `>` inside names or descriptions must be escaped as `&quot;`, `&amp;`, `&lt;`
and `&gt;` — unescaped ampersands in a company name are a frequent cause.

### GOBD2002 — index.xml violates the 1.6 grammar

**Severity:** error **Category:** grammar

The document is well formed but does not match the Beschreibungsstandard 1.6 DTD. All recoverable
violations are reported, not just the first.

**Triggers when** an element is missing, out of order, or in the wrong place — for example a
`Table` declaring neither `VariableLength` nor `FixedLength`, or `Name` before `URL`.

**Why an error:** the description does not conform to the standard, and element order is part of
the grammar, not a formatting preference.

**What to change:** the message names the element and lists what was expected there. Validation
always uses the canonical grammar embedded in the tool, so this verdict does not depend on which
DTD your export ships.

### GOBD2003 — the export ships no DTD file

**Severity:** error **Category:** grammar

No `.dtd` file is present in the export.

**Triggers when** the export contains no DTD alongside `index.xml`.

**Why an error:** the standard's FAQ is explicit that the medium must contain the DTD file, the
`index.xml` and all reference data.

**What to change:** include `gdpdu-01-03-2019.dtd`, byte for byte as published. Validation itself
is unaffected — the tool uses its own embedded copy — but the delivered medium is incomplete.

### GOBD2004 — the shipped DTD differs only in encoding

**Severity:** warning **Category:** grammar

The DTD in the export is the right grammar, re-encoded. The finding names which artefact differs
— line endings, byte-order mark or trailing whitespace — and gives both fingerprints.

**Triggers when** the shipped DTD matches the canonical one only after normalising those three
things. Converting CRLF to LF is by far the most common cause: a Linux zip/unzip cycle, a
`.gitattributes` rule, or any tool that normalises text.

**Why a warning and not an error:** the grammar is semantically identical and any XML parser
reads it. But the standard requires the shipped copy to be byte-identical to the original, so
this is a real defect in the medium rather than a cosmetic one. It counts toward exit code `1`.

**What to change:** copy the DTD from the official archive in binary mode and ensure nothing in
your packaging rewrites it. The expected SHA-256 is
`691051c9828ec2bbef71527c4aa77554c09ecd49e862fc6f2bc4b14507c72a0e`.

### GOBD2005 — the shipped DTD has been modified

**Severity:** error **Category:** grammar

The DTD in the export differs in substance, not merely encoding. Both fingerprints are given.

**Triggers when** normalising line endings, BOM and trailing whitespace still leaves a difference
— an edited declaration, or a different version of the standard under the 1.6 name.

**Why an error:** the standard states plainly that you must not modify the DTD.

**What to change:** replace it with the official file. If you edited it to make your export
validate, the export is what needs changing.

### GOBD2006 — the DOCTYPE names the 2002 grammar

**Severity:** info **Category:** grammar

`index.xml` declares `SYSTEM "gdpdu-01-08-2002.dtd"` rather than the 2019 name.

**Triggers when** the document type declaration names the older file.

**Why only info:** the 1.6 document's own Examples 1 and 3 declare exactly this, while its FAQ
and Example 4 name the 2019 file. The standard is inconsistent with itself, so this cannot
reasonably be held against an export. Validation used the canonical 1.6 grammar either way, and
1.6 is a strict superset of 1.1, so nothing is lost.

**What to change:** nothing is required. For consistency with the current standard you may name
`gdpdu-01-03-2019.dtd`.

### GOBD2007 — an external DOCTYPE reference was refused

**Severity:** error **Category:** grammar

`index.xml` pointed its document type declaration outside the export, and the tool refused to
follow it.

**Triggers when** the DOCTYPE system identifier is absolute — `file:///…`, `http://…`, `ftp://…`.

**Why an error:** resolving it would let a description file reach the filesystem or the network of
whoever validates it. The reference is refused and the run stops.

**What to change:** declare a plain relative file name, as every example in the standard does.

### GOBD2008 — entity expansion exceeded the permitted budget

**Severity:** error **Category:** grammar

The document declared entities whose expansion is disproportionate to its size, and parsing was
abandoned.

**Triggers when** an internal subset defines nested entities that multiply out — the "billion
laughs" shape, whether hostile or accidental.

**Why an error:** expanding them would exhaust memory. The canonical grammar declares no entities
at all, so anything expanding here was declared by the document itself.

**What to change:** remove the internal subset. A GoBD description file has no need of entity
definitions.

---

## GOBD3xxx — what index.xml describes

### GOBD3001 — a foreign key names an undeclared column

**Severity:** error **Category:** structure

A `ForeignKey/Name` is not a column of the table that declares it.

**Triggers when** the named column appears in no `VariableColumn`, `VariablePrimaryKey`,
`FixedColumn` or `FixedPrimaryKey` of that table.

**Why an error:** the standard is explicit that a `ForeignKey` does not define a column — the
column must already have been declared. There is nothing to join from.

**What to change:** declare the column, or correct the spelling. Names are case-sensitive.

### GOBD3002 — a reference names no table in the data set

**Severity:** error **Category:** structure

A `ForeignKey/References` points at a table that is not described anywhere in the `DataSet`.

**Triggers when** the referenced identity matches no table's `Name`, nor any table's `URL` where
`Name` is absent.

**Why an error:** the standard's FAQ says the description of the links must match the delivered
data, and the import will report the inconsistency to the auditor.

**What to change:** `References` names a _table_, not a file or a column. Check you used the
table's `Name` — or its `URL` if it declares no `Name`. References may cross `Media` boundaries;
they are scoped to the whole `DataSet`.

### GOBD3003 — a reference matches more than one table

**Severity:** error **Category:** structure

Two or more tables share the identity a `References` names. The finding lists the candidates.

**Triggers when** several `Table` elements declare the same `Name`.

**Why an error:** the join target is undecidable.

**What to change:** give the tables distinct names. See also `GOBD3024`.

### GOBD3004 — the referenced table declares no primary key

**Severity:** error **Category:** structure

The referenced table exists but has no primary key, so there is nothing for the foreign key to
join to.

**Triggers when** the target declares only `VariableColumn` or `FixedColumn` elements. The DTD
permits this, which is why it needs a semantic check.

**Why an error:** a foreign key joins to the referenced table's primary key. Without one the
relationship cannot be resolved.

**What to change:** declare the identifying column of the master table as a primary key.

### GOBD3005 — foreign key arity differs from the referenced primary key

**Severity:** error **Category:** structure

The foreign key names a different number of columns than the target's primary key has. Both
counts are given.

**Triggers when** a one-column key references a table with a composite primary key, or the reverse.

**Why an error:** the columns are matched positionally, so an arity mismatch leaves columns
unpaired.

**What to change:** name every column of the composite key, in the order the target declares them
— or use `Alias` to map them explicitly.

### GOBD3006 — a foreign key column's datatype differs from its target

**Severity:** error **Category:** structure

A foreign key column and the primary key column it maps onto are declared as different datatypes.

**Triggers when** the two declared types differ. `Accuracy` and `MaxLength` are not compared; only
`Numeric` versus `AlphaNumeric` versus `Date`.

**Why an error:** the standard warns that joining columns of differing datatypes gives undefined
results and will most likely error during import.

**What to change:** make the declarations agree. Usually one side was declared `Numeric` because
the values look numeric, while the master data declares `AlphaNumeric`.

### GOBD3007 — an alias maps from a column outside its foreign key

**Severity:** error **Category:** structure

An `Alias/From` names a column that is not one of this `ForeignKey`'s `Name` elements.

**Triggers when** the alias source is a different column of the table, or a typo.

**Why an error:** the alias has nothing to remap.

**What to change:** `Alias/From` must be one of the names listed in the same `ForeignKey`.

### GOBD3008 — an alias maps onto a non-key column

**Severity:** error **Category:** structure

An `Alias/To` names a column of the referenced table that is not part of its primary key.

**Triggers when** the target column exists but is a plain column, or does not exist at all.

**Why an error:** the join target is the referenced table's primary key. Mapping onto anything
else does not describe a resolvable relationship.

**What to change:** point the alias at a primary key column of the referenced table.

### GOBD3009 — two aliases map onto the same target column

**Severity:** error **Category:** structure

Two `Alias` elements in one `ForeignKey` both name the same `To`.

**Triggers when** a composite key's aliases were copied and one target was not updated.

**Why an error:** two source columns cannot both join to one key column; another key column is
left unmapped.

**What to change:** give each alias its own target.

### GOBD3010 — a composite key is only partially aliased

**Severity:** warning **Category:** structure

A `ForeignKey` with several columns supplies aliases for some of them but not all. The counts are
given.

**Triggers when** at least one but not every column of a multi-column key has an alias.

**Why a warning and not an error:** the standard does not say what the unaliased columns fall back
to. They are matched positionally here, which is order-dependent and very likely not what was
intended — but since the standard is silent, this is reported rather than decided.

**What to change:** alias every column of the key, or none of them. Aliasing all of them is
clearer even where the names already match.

### GOBD3011 — a time map's From and To masks differ

**Severity:** warning **Category:** structure

A `Map` whose `From` and `To` are both valid time masks but are not equal.

**Triggers when** for example `HHMMSS` maps to `HH:MM:SS`.

**Why a warning:** standard 1.6 lets a `Map` declare an alphanumeric column as carrying _time_
values, but only when `From` and `To` are identical. Unequal masks are a value substitution
instead — almost certainly not what was meant, since it would replace the literal text `HHMMSS`
with the literal text `HH:MM:SS`.

**What to change:** to declare a time column, make both sides the same mask. Recognised forms are
`HHMM`, `HH:MM`, `HHMMSS`, `HHMMTT`, `HH:MMTT`, `HH:MM:SS`, `HHMMSSTT`, `HH:MM:SSTT`, `HHMM TT`,
`HH:MM TT`, `HHMMSS TT` and `HH:MM:SS TT`.

### GOBD3012 — a map appears on a non-alphanumeric column

**Severity:** error **Category:** structure

A `Map` is declared on a `Numeric` or `Date` column.

**Triggers when** the column carrying the `Map` is not `AlphaNumeric`.

**Why an error:** the standard states that `Map` may only be used for alphanumeric fields, and
lists the conversions it does not support.

**What to change:** remove the `Map`, or declare the column `AlphaNumeric` if it really holds
coded text.

### GOBD3013 — a medium describes no data

**Severity:** warning **Category:** structure

A `Media` element contains no `Table` and does not declare `AcceptNoTables`.

**Triggers when** a medium is empty and says nothing about that being deliberate.

**Why a warning:** since 1.4 an empty medium is legal, but only as a deliberate choice signalled
by `AcceptNoTables`. Without it, an empty medium usually means the export failed partway.

**What to change:** remove the empty `Media`, or add `<AcceptNoTables>true</AcceptNoTables>` if it
is intended — see `GOBD3014`.

### GOBD3014 — a medium deliberately describes no data

**Severity:** info **Category:** structure

A `Media` element declares no `Table` and says so with `AcceptNoTables`.

**Triggers when** the marker is present on an empty medium.

**Why only info:** this is exactly what the standard added `AcceptNoTables` for. It is recorded so
the reader knows the emptiness was intended.

**What to change:** nothing.

### GOBD3015 — a fixed range describes an invalid span

**Severity:** error **Category:** structure

A `FixedRange` cannot be read as a position span.

**Triggers when** `From` is not a positive integer, `To` is smaller than `From`, `Length` is zero
or negative, or neither `To` nor `Length` is present.

**Why an error:** the column's position in the record is undefined, so the field cannot be read.

**What to change:** positions are 1-based and inclusive. `From 1, To 10` and `From 1, Length 10`
describe the same ten characters.

### GOBD3016 — two fixed-length columns overlap

**Severity:** warning **Category:** structure

Two columns of a `FixedLength` table claim overlapping positions. Both are named.

**Triggers when** one column's span starts at or before another's end.

**Why a warning and not an error:** overlapping spans are usually a mistake, but exposing a
composite field both whole and in parts is a legitimate pattern the standard does not forbid.

**What to change:** if unintended, correct the positions — an off-by-one in `From` is the usual
cause.

### GOBD3017 — a fixed-length record leaves unclaimed positions

**Severity:** info **Category:** structure

Positions between the first and last declared column belong to no column. The gap is named.

**Triggers when** one column ends at 10 and the next begins at 21.

**Why only info:** filler and reserved regions are common and entirely legal. It is reported
because an unintended gap usually means a column was omitted.

**What to change:** nothing, unless a column is genuinely missing.

### GOBD3018 — a column extends beyond the declared record length

**Severity:** error **Category:** structure

A column's span ends past the `Length` declared for the `FixedLength` table.

**Triggers when** the table declares a record length and any column reaches beyond it.

**Why an error:** the field cannot be read; the record ends first.

**What to change:** correct the record length or the column's span. They disagree, and only you
know which is right.

### GOBD3019 — a declared scalar is not a usable non-negative integer

**Severity:** error **Category:** structure

A numeric setting cannot be interpreted. The element and its declared value are named.

**Triggers when** `Accuracy`, `ImpliedAccuracy`, `MaxLength`, `SkipNumBytes`, `Epoch` or a `Range`
bound is not a non-negative integer — or when `MaxLength` is zero, which would describe a column
that can hold nothing.

**Why an error:** an importer cannot act on it. A decimal separator inside the number, such as an
`Accuracy` of `2,5`, is a frequent cause.

**What to change:** use a plain non-negative integer.

### GOBD3020 — decimal and grouping symbols are identical

**Severity:** error **Category:** structure

A table declares the same character for `DecimalSymbol` and `DigitGroupingSymbol`.

**Triggers when** both are, say, `,`.

**Why an error:** `1,234` becomes unparseable — there is no way to tell a thousands separator from
a decimal point.

**What to change:** the standard's defaults are `,` for decimals and `.` for grouping. Both must
be declared together, and they must differ.

### GOBD3021 — declared delimiters collide

**Severity:** error **Category:** structure

A `VariableLength` table declares the same character for two different delimiters. Both roles are
named.

**Triggers when** `ColumnDelimiter` equals `RecordDelimiter`, or equals a non-empty
`TextEncapsulator`.

**Why an error:** records or fields cannot be separated unambiguously.

**What to change:** use distinct characters. An empty `<TextEncapsulator/>` means "none" and never
collides — that form is legal and common.

### GOBD3022 — a date mask is unusable

**Severity:** error **Category:** structure

A `Date/Format` or `Validity/Format` mask has no usable year, month and day placeholders. The mask
is quoted.

**Triggers when** a placeholder is missing or appears twice — `DD.MM` has no year, `DD.MM.YYYY.YY`
has two.

**Why an error:** dates cannot be interpreted without knowing where each part sits.

**What to change:** use `DD`, `MM` and `YY` or `YYYY`, each exactly once, with any separators you
like: `DD.MM.YYYY`, `YYYY-MM-DD`, `DDMMYY`.

### GOBD3023 — a record range starts below one

**Severity:** error **Category:** structure

A `Table/Range/From` is zero or negative.

**Triggers when** the range's start is not at least 1.

**Why an error:** records are counted from one.

**What to change:** `Range/From` counts _records_, not bytes — `<Range><From>2</From></Range>` is
the documented way to skip a header row. To skip bytes, use `SkipNumBytes`.

### GOBD3024 — two tables share a name

**Severity:** warning **Category:** structure

Two `Table` elements declare the same `Name`. The URLs of both are given.

**Triggers when** the same name is used twice, whether or not anything references it.

**Why a warning — and when it becomes an error:** duplicate names are only harmful once something
resolves through them. If any `ForeignKey` references the duplicated name, this is reported as an
**error** instead, because the join target is then undecidable. You may therefore see this code
with either severity.

**What to change:** give each table a distinct name.

### GOBD3025 — two tables share a URL

**Severity:** warning **Category:** structure

The same file is described by more than one `Table`. The count is given.

**Triggers when** two `Table` elements declare the same `URL`.

**Why a warning:** describing one file twice is legal — two views of the same data with different
column subsets — but it is usually a copy-paste mistake.

**What to change:** if unintended, remove the duplicate. If intended, give each `Table` a distinct
`Name` so references stay unambiguous.

### GOBD3026 — two columns of one table share a name

**Severity:** error **Category:** structure

A table declares the same column name twice.

**Triggers when** any two columns or primary keys within one table have the same `Name`.

**Why an error:** foreign keys and aliases resolve columns by name, so a duplicate makes those
references ambiguous. Unlike duplicate table names, there is no benign reading.

**What to change:** rename one of them.

### GOBD3027 — a name contains a character the standard forbids

**Severity:** warning **Category:** structure

A table or column name contains one of `"`, `&`, `<` or `>`. The offending characters are listed.

**Triggers when** any of those four appear after XML entity decoding — so `Firma &amp; Co` triggers
it, because the decoded name contains `&`.

**Why a warning:** the standard asks you not to use these characters in names and descriptive
fields, but a correctly escaped document still parses, and importers generally cope.

**What to change:** rename the column, or accept the warning knowingly. The restriction exists
because these characters need escaping in XML and are easy to mishandle downstream.

### GOBD3028 — a description exceeds 255 characters

**Severity:** info **Category:** structure

A `Description` is longer than the standard advises. The actual length is given.

**Triggers when** the text exceeds 255 characters.

**Why only info:** the standard says description text _should_ not exceed 255 characters — advice,
not a rule. Descriptions are shown to the auditor as commentary and a very long one may be
truncated by the importer.

**What to change:** shorten it if the detail matters, or leave it.

### GOBD3029 — a Command is declared and was not executed

**Severity:** warning **Category:** structure

The export declares a `Command`. Its text is quoted.

**Triggers when** any `Command` appears under `DataSet` or `Media`.

**Why a warning:** the standard defines `Command` as an operating-system command run around the
import, typically to decompress data. **This validator never executes it, and never opens or even
resolves the file it names** — that is a security boundary, not a missing feature. It is reported
as a warning so a human reviews what the medium is asking the auditor's machine to run.

**What to change:** usually nothing. Confirm the command is what you intend to ship, and that any
script it names is present on the medium.

### GOBD3030 — an Extension is declared

**Severity:** info **Category:** structure

The export declares an `Extension`, naming an application-specific supplementary file.

**Triggers when** any `Extension` appears in the `DataSet`.

**Why only info:** extensions were added in 1.3 for application-specific additions. Their meaning
is outside this standard, so their presence is recorded and nothing more.

**What to change:** nothing.

### GOBD3031 — an Extension URL resolves to no entry

**Severity:** error **Category:** structure

An `Extension` names a supplementary file that is not in the export.

**Triggers when** the URL cannot be resolved, or resolves to a name no entry has.

**Why an error:** as with table files, the description must match what is delivered.

**What to change:** ship the file or remove the `Extension`.

---

## GOBD4xxx — what the data files hold

These read the data files and compare each record against the layout `index.xml` declares for its
table. They are reported **only when content checking is requested**; without it no data file is
opened at all.

A finding here names the table, the record number as the file counts it, the column, and the
single offending value — never the rest of the record. A content report travels: it is attached
to build logs, pasted into tickets and sent to whoever produced the export, and a record of a
GoBD export is a person's data.

### GOBD4001 — a value does not match its declared type or format

**Severity:** error **Category:** content

A value cannot be read as the datatype its column declares: a `Date` that does not match the
column's mask, or a `Numeric` that is not a number under the table's declared decimal and digit
grouping symbols.

A number is one optional sign, `-` or `+`, standing directly before its digits or directly after
them; then at least one digit; and, after the decimal symbol, at least one more. Padding around the
value is ignored, and a sign is never counted as a decimal place.

- **Minus sign:** `-1782,90` and `1782,90-` are the same number, because the standard lets a
  negative figure carry its minus sign on either side.
- **Plus sign:** the standard does not mention one. A trailing `+` is accepted by this tool's own
  decision, for symmetry with the leading `+` it has always accepted.
- **Not a number:** a sign separated from the digits by a space, a sign at both ends, or two signs.
- **A declared `+` or `-` symbol:** a table that declares `+` or `-` as its decimal or grouping
  symbol reads no sign behind the digits, because the sign could not be told from the symbol.

**Triggers when** the value is non-empty and fails to parse under the declaration. An empty value
is an absent value and is never reported: GoBD exports write an empty field for "no value".

**Why an error:** the declaration is what an importer will use. A value that does not match it
either fails the import or, worse, is silently read as something else.

**What to change:** correct the value, or correct the declaration if the mask or symbols are what
is wrong. The finding quotes both the value and what was expected, so which of the two is at
fault is usually obvious. A common cause is a `Date` column exported in ISO form while the
declaration still says `DD.MM.YYYY`.

### GOBD4002 — a value carries more decimal places than declared

**Severity:** error **Category:** content

A `Numeric` value has more digits after the decimal symbol than the column's `Accuracy` permits.

**Triggers when** the count of digits following the declared decimal symbol exceeds `Accuracy`.
Fewer is not reported: `Accuracy` is a maximum, not an exact count.

**Why an error:** the standard states outright that results are undefined when importing numeric
data with greater accuracy than `index.xml` declares. Where the extra digits go — truncated,
rounded, or carried — differs between importers, so the same export produces different sums.

**What to change:** raise `Accuracy` to what the data actually carries, or round the values
before export. Rounding at import time is what you are otherwise leaving to chance.

### GOBD4003 — a value exceeds its declared maximum length

**Severity:** error **Category:** content

An `AlphaNumeric` value is longer than the `MaxLength` its column declares.

**Triggers when** the stored value's length exceeds `MaxLength`. The length is measured in
characters after decoding, not in bytes.

**Why an error:** an importer sizes its column from the declaration. A longer value is truncated
on import, and a truncated value is wrong without looking wrong.

**What to change:** raise `MaxLength` to the true maximum, or shorten the values. If the column
has no meaningful bound, omitting `MaxLength` is legitimate and better than declaring one that
the data exceeds.

### GOBD4004 — a record yields a different number of columns than declared

**Severity:** error **Category:** content

Splitting the record at the declared column delimiter produced more or fewer values than the
table declares columns.

**Triggers when** the count differs, in either direction. The finding names both counts.

**Why an error:** with a missing or extra column, every value after the discrepancy belongs to a
different column than the declaration says. Nothing else detects that, and the values are all
individually plausible.

**What to change:** the usual causes are an unescaped column delimiter inside an unencapsulated
value, a value containing a line break that was not encapsulated, and a trailing delimiter at the
end of each record. Encapsulating the affected values fixes all three.

### GOBD4005 — bytes could not be decoded in the declared codepage

**Severity:** error **Category:** content

The file holds a byte sequence that is not valid in the codepage the table declares.

**Triggers when** decoding fails at some position in the record. The finding names the record and
the column the position falls in. In practice this means the declaration and the file disagree
about the encoding — most often a file written as UTF-8 while `index.xml` declares ANSI, or the
reverse.

**Why an error:** every value in the file is read through the declared codepage. If it is the
wrong one, values that decode "successfully" are silently wrong too, and only the sequences that
happen to be invalid announce it.

**What to change:** declare the codepage the file was actually written in, or re-encode the file
to the one declared. When no codepage is declared, ANSI applies — which is a frequent cause on
its own.

### GOBD4006 — a text encapsulator was never closed

**Severity:** error **Category:** content

A value opened with the declared text encapsulator, and the file ended before it was closed.

**Triggers when** the closing encapsulator is absent. Since an encapsulated value may contain the
record delimiter, an unclosed one swallows the rest of the file.

**Why an error:** everything after the unclosed encapsulator is read as one value. The record
count and every subsequent record are wrong.

**What to change:** close it. When the value itself must contain the encapsulator, double it —
that is the standard's escape and the reader honours it.

### GOBD4007 — a fixed-length record has the wrong length

**Severity:** error **Category:** content

A record of a `FixedLength` table is not as long as the declaration says a record is.

**Triggers when** the record's length in characters differs from the declared `Length`, or from
the position the last declared column reaches when no `Length` is declared. A final record cut
short by the end of the file triggers it too.

**Why an error:** in a fixed-length file, position is identity. A record of the wrong length
shifts every column that follows, and where records are not delimited, it shifts every record
after it as well.

**What to change:** pad the record to the declared length, or correct `Length`. Check the record
delimiter first: a file written with CRLF and declared with LF leaves a stray character in every
record.

### GOBD4008 — analysis of a table stopped at the finding bound

**Severity:** info **Category:** content

Enough defects were found in one table that analysis of it stopped. The findings reported are the
first ones by record order; the table may hold further defects that were never looked for.

**Triggers when** a table produces as many findings as the configured bound, 50 by default.

**Why info:** it is not itself a defect. It is the report telling you that its list of defects for
this table is not exhaustive, which matters when you are about to conclude that you have fixed
everything.

**What to change:** nothing, directly. Fix the reported defects and run again; they are usually
one mistake repeated. Raise the bound if you want the full count for a file you already know is
broken.

### GOBD4009 — a header row's column order differs from the declaration

**Severity:** error **Category:** content

The record the declaration excludes carries exactly the declared column names, but not in the
declared order. The finding names both orders.

**Triggers when** the excluded first record holds the same set of names as the declaration and a
different sequence. A record that bears no resemblance to the column names is not reported: a
declaration may legitimately exclude a preamble rather than a header.

**Why an error:** this is the defect that hides best. Every value lands in a different column
than the declaration says, every value is individually plausible, and nothing else in the export
disagrees with itself. Amounts end up in a date column or, worse, in another amount column where
they still add up.

**What to change:** put the declared columns in the order the file writes them, or write the file
in the order it declares. The order in `index.xml` is the order the file must deliver, because a
file without a header row has no other way to say which column is which.

### GOBD4010 — a header row is present but no record is excluded

**Severity:** error **Category:** content

Record 1 carries the declared column names, and the declaration excludes no record — so that row
will be read as data.

**Triggers when** the first record holds the declared column names, in any order, and the table
declares no `Range` that skips it.

**Why an error:** the header is imported as a record. Every column is then read as text that
happens to be a column name, which fails the type checks for dates and numbers, and the row
count is one too high everywhere it is used.

**What to change:** declare `<Range><From>2</From></Range>` on the table, or export the file
without its header row. Note that `SkipNumBytes` is not the tool for this: it counts bytes, not
records, and a header of the wrong assumed length leaves half a row behind.

### GOBD4011 — a table's declaration could not be turned into a reading

**Severity:** error **Category:** content

The data file is present, but the declaration does not say how to read it, so its contents were
not checked. The finding names what was missing.

**Triggers when** the table declares an empty column or record delimiter, a fixed-length column
whose span cannot be read, or a codepage this build cannot supply.

**Why an error:** the contents of this table were not checked at all, and the export must not be
reported as clean on the strength of a check that did not run. Most of the causes are also
defects in their own right: a declaration that cannot be read by this tool cannot be read by an
importer either.

**What to change:** fix the part of the declaration the finding names. `FixedRange` problems are
usually also reported as `GOBD3015`.

---

## GOBD5xxx — keys and the references built on them

These check the keys the declaration promises: that a primary key identifies one record, and that
every foreign key value points at a record that exists. They read the data files and are reported
only when content checking is asked for.

A dangling reference is the defect that most often makes an export unusable to the auditor
importing it, and it is invisible in every table taken alone.

### GOBD5001 — a primary key value occurs more than once

**Severity:** error **Category:** integrity

Two or more records of a table carry the same declared primary key. The finding names the value
and the records carrying it.

**Triggers when** the same key value appears in more than one record. Composite keys are compared
as whole tuples. Candidates are confirmed against the actual key values before anything is
reported, so a hash collision cannot produce a false accusation.

**Why an error:** `References` resolves to a table's primary key. A duplicate key makes every
reference to that table ambiguous — the importer picks one record, and which one is undefined.

**What to change:** if the key really is not unique, declare a key that is; a composite of the
columns that together identify a record is usually already in the data. Duplicated records are
the other common cause, most often from an export that ran twice into the same file.

### GOBD5002 — a primary key is empty or only partly present

**Severity:** error **Category:** integrity

A record's declared primary key has no value, or a composite key has a value for some of its
columns and not others.

**Triggers when** any column of the declared key is empty in a data record.

**Why an error:** the record cannot be referred to, and it cannot be told apart from any other
record with an empty key. References to the table are checked against the keys that exist, so
such a record is invisible to every foreign key that ought to find it.

**What to change:** give the record a key. If the column is genuinely optional, it is not a
primary key and should not be declared as one.

### GOBD5003 — a foreign key value matches no record of the referenced table

**Severity:** error **Category:** integrity

A record's foreign key points at a primary key that is not in the referenced table.

**Triggers when** the value, mapped onto the referenced table's key columns as the declaration
establishes, is found in no record of that table. A foreign key whose columns are all empty is
an absent reference and is not reported.

**Why an error:** this is the defect that makes an export unusable rather than merely untidy. The
auditor's import either fails outright or drops the referring records, and neither outcome is
one the producer learns about.

**What to change:** the usual causes are a partial export — the child table covering a period the
parent does not — and a key that was transformed on one side only, such as leading zeros
stripped from a customer number. Check the reported value against the referenced table before
assuming the record is missing.

### GOBD5004 — a reference could not be checked

**Severity:** warning **Category:** integrity

The referenced table could not be read, so the values pointing at it were not checked. They are
not reported as unresolved, because nothing was compared.

**Triggers when** the referenced table's file is absent or its declaration cannot be turned into
a reading.

**Why a warning:** the real defect is the referenced table, and it carries its own error —
`GOBD1003` for an absent file, `GOBD4011` for an unusable declaration. This finding exists so
that the reference is not silently treated as sound.

**What to change:** fix the referenced table, then run again to find out whether the references
actually resolve.

---

## GOBD9xxx — the validator itself

These report that **the validator could not do its job**. They say nothing about the quality of
your export. Any error in this range produces exit code `3`, deliberately distinct from `2`, so a
pipeline can tell "your export is bad" from "this result cannot be trusted".

### GOBD9001 — the export path does not exist

**Severity:** error **Category:** tool

The path given on the command line is neither a file nor a directory.

**Triggers when** the argument does not exist.

**Why a tool failure:** nothing was examined, so no verdict about the export is possible.

**What to change:** check the path. A directory is read as an unpacked export, a file as a ZIP.

### GOBD9002 — the export could not be read

**Severity:** error **Category:** tool

The export exists but could not be opened. The underlying reason is quoted.

**Triggers when** the ZIP is corrupt or truncated, or the file cannot be read.

**Why a tool failure:** the export may be perfectly valid; it could not be inspected.

**What to change:** verify the archive opens elsewhere, and re-create it if not. A truncated
upload or an interrupted copy is the usual cause.

### GOBD9003 — the requested report language is unavailable

**Severity:** info **Category:** tool

A `--language` value was given that the tool does not provide. English was used instead.

**Triggers when** the requested language is anything other than English or German.

**Why only info, and why it does not affect the exit code:** an unavailable language is no reason
to withhold a report. The run proceeds and reports normally.

**What to change:** use `en` or `de`, or omit the option to take the operating system's language.

### GOBD9004 — a check failed unexpectedly

**Severity:** error **Category:** tool

One of the checks threw. The check's name and the error are given. Every other check still ran,
so the rest of the report is intact.

**Triggers when** a check encounters input its author did not anticipate.

**Why a tool failure:** it is a defect in this tool, not in your export. The report is incomplete
because one check produced nothing, so the verdict cannot be relied on.

**What to change:** nothing on your side — please report it, ideally with the `index.xml` that
provoked it.

### GOBD9005 — the embedded canonical grammar failed its self-check

**Severity:** error **Category:** tool

The Beschreibungsstandard grammar built into this binary does not match its pinned fingerprint.
Validation was not performed. Both the expected and the observed fingerprint are given.

**Triggers when** the embedded grammar has been altered — a corrupted build, a substituted
resource, or a checkout that rewrote the file's line endings.

**Why a tool failure:** a validator judging exports against the wrong grammar would produce
confidently wrong verdicts. It refuses to run instead.

**What to change:** re-download the binary or rebuild from a clean checkout. If you build from
source, confirm nothing normalises `*.dtd` — the repository marks it binary for this reason.
### GOBD9006 — a table holds more keys than this build can check

**Severity:** error **Category:** tool

The table carries more records than the key checker will hold in memory at once, so its primary
keys and every reference pointing at it were left unchecked.

**Triggers when** a single table exceeds the key capacity — 64 million records by default, at
eight bytes a key. The limit applies per table, not to the export as a whole.

**Why an error, and why in this range:** nothing is wrong with your export. The validator could
not answer the question, and a result that silently omits a check is worse than one that says so.
Like every `GOBD9xxx` error it produces exit code `3`, so a pipeline can tell "unchecked" from
"bad".

**What to change:** nothing in the export. Run the desktop reader instead, whose store spills to
disk and has no such ceiling, or raise the limit if the machine has the memory for it.
