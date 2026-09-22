<!-- Made by .github/scripts/make-reader-icons.sh from src/GoBd.Reader.Ui/Assets/gobd-reader.svg. -->
<p align="center"><img src="docs/gobd-reader-icon.svg" alt="GoBD Reader" width="128" height="128"></p>

# GoBD validator and reader

Two tools for GoBD/GDPdU data carrier exports, as described by the
[Beschreibungsstandard 1.6](https://www.caseware.com/de/beschreibungsstandard):

- **`gobd-validate`**, a command-line validator for companies that **produce** exports. Run it in
  the export pipeline and find the defects before the auditor does.
- **`gobd-reader`**, a desktop reader for anyone who has to **read** an export. It shows each table
  under the column names `index.xml` declares, which a spreadsheet cannot, and follows foreign keys
  with a click.

Every finding code either tool reports is explained in [`docs/finding-codes.md`](docs/finding-codes.md).

## gobd-validate

By default it reads only what **describes** the export and opens no data file, so a
multi-gigabyte export validates in milliseconds. `--contents` reads the data files as well.

| Default                                                    | Added by `--contents`                     |
| ---------------------------------------------------------- | ----------------------------------------- |
| `index.xml` against the canonical 1.6 DTD                  | Every record against its declared layout  |
| The DTD the export ships                                   | Types, formats, accuracy, maximum lengths |
| Foreign keys, aliases, primary keys, table identity        | Header rows, including a reordered one    |
| Fixed-length spans, declared metadata, date and time masks | Primary key uniqueness                    |
| That every declared file is present                        | That every foreign key value resolves     |

Without `--contents`, a clean result means the description is valid, not the data. Every report
states which of the two it checked.

### Usage

```
gobd-validate <export> [options]

  <export>  A ZIP archive or an unpacked folder containing index.xml.

Options:
  --format <text|json>   Report format. Default: text.
  -o, --output <file>    Write the report to a file instead of standard output.
  -l, --language <en|de> Report language. Default: the operating system's, else English.
                         May also be set with the GOBD_LANG environment variable.
      --strict           Count warnings against the verdict.
      --contents         Also read the data files and check what they contain:
                         record layout, header rows, primary key uniqueness and
                         foreign key existence. Off by default.
      --max-findings <n> Findings per table after which analysis of that table
                         stops. Default: 50. Only meaningful with --contents.
  -h, --help             Show this help.
      --version          Show the version.
      --license          Show the licence: MIT.
      --notice           Show what that licence does not cover.
      --third-party-notices
                         Show the notices of the components included.
```

Run against the defective fixture in `tests/GoBd.Validation.Tests/Fixtures`:

```console
$ gobd-validate broken-export --contents
GoBD export validation: NOT CONFORMANT
Export: broken-export
5 error(s), 0 warning(s), 0 note(s)

Bestellungen
  error    GOBD1003  24:8     Table 'Bestellungen' declares 'bestellungen.csv', which is not present in the export.
  error    GOBD3001  33:12    Table 'Bestellungen': foreign key column 'KundenCode' referencing 'Kunden' is not declared in this table.
  error    GOBD3002  39:12    Table 'Bestellungen' references 'Nirgends', which is not a table in this data set.

Kunden
  error    GOBD4004  12:6     Table 'Kunden', record 1: 2 columns were read, but 1 are declared.

gdpdu-01-03-2019.dtd
  error    GOBD2005  -        The DTD 'gdpdu-01-03-2019.dtd' in this export has been modified: expected SHA-256 691051c9828ec2bbef71527c4aa77554c09ecd49e862fc6f2bc4b14507c72a0e, found da40349b9f67d6e4636fb2a34fb03f675d7436074e6d93880bd78df3c08c164e. The standard forbids changing the grammar.

Scope: index.xml, the DTD, the export's file listing and the contents of the data files were checked.
```

### Exit codes

| Code | Meaning                                      |
| ---- | -------------------------------------------- |
| `0`  | Conformant                                   |
| `1`  | Warnings only                                |
| `2`  | Errors present, or warnings under `--strict` |
| `3`  | The validator could not run                  |

`3` is deliberately distinct: a pipeline must be able to tell "your export is bad" from
"the validator failed".

### JSON output

`--format json` emits one document with a `schemaVersion`. Codes, severities and field names are
language-neutral; only `message` is translated. `contentsExamined` tells a consumer whether the
data files were read.

### Limits

The key checks hold one table's keys in memory at eight bytes each. A table beyond 64 million
records is reported as unchecked (`GOBD9006`, exit `3`), never as clean. The reader has no such
limit.

## gobd-reader

### Get it

Download the reader for your platform and its `.sha256` from the
[latest release](https://github.com/WebDucer/gobd-tools/releases/latest). The CI run of every commit also carries a build, kept for
48 hours.

| Platform      | Download                      | Size  |
| ------------- | ----------------------------- | ----- |
| `win-x64`     | `gobd-reader-win-x64.exe`     | 35 MB |
| `win-arm64`   | `gobd-reader-win-arm64.exe`   | 35 MB |
| `linux-x64`   | `gobd-reader-linux-x64`       | 45 MB |
| `linux-arm64` | `gobd-reader-linux-arm64`     | 42 MB |
| `osx-arm64`   | `gobd-reader-osx-arm64.dmg`   | 25 MB |

Nothing needs to be installed, because the .NET runtime and the DuckDB database engine come with it.
That is why it is far larger than the validator. The builds are not code-signed, so the first start
asks for confirmation:

- **Windows:** start `gobd-reader-win-x64.exe`, or `gobd-reader-win-arm64.exe` on an ARM machine.
  When SmartScreen warns, choose **More info**, then **Run anyway**.
- **Linux:** make the file executable with `chmod +x gobd-reader-linux-x64`, or
  `gobd-reader-linux-arm64` on an ARM machine, then start it.
- **macOS:** open the disk image and drag **GoBD Reader** to **Applications**. macOS refuses the
  first start: allow it under **System Settings → Privacy & Security → Open Anyway**, or clear the
  download quarantine with `xattr -dr com.apple.quarantine "/Applications/GoBD Reader.app"`.

On Windows and Linux the single file unpacks its native libraries when it first starts, about 55 MB
beneath `%TEMP%\.net` on Windows and 80 MB beneath `~/.net` on Linux, and reuses them afterwards. Set
`DOTNET_BUNDLE_EXTRACT_BASE_DIR` to unpack them somewhere else, for instance where `HOME` is not set.
Starting another version removes the copies earlier versions left, unless a reader still running
uses them.

### Check it

The builds are not code-signed, but every file a release carries, the validator's included, is
attested: the release workflow signs a record of the build that produced it. With the
[GitHub CLI](https://cli.github.com), check that a download is exactly what that workflow built:

```console
gh attestation verify gobd-reader-win-x64.exe -R WebDucer/gobd-tools
```

Every release also carries a software bill of materials for each tool, in CycloneDX:
`gobd-validate.cdx.json` and `gobd-reader.cdx.json` list every component the tool contains, with its
version, the .NET runtime included. Each tool's downloads are bound to their bill of materials, so
a download leads to the one that describes it:

```console
gh attestation verify gobd-reader-win-x64.exe -R WebDucer/gobd-tools \
  --predicate-type https://cyclonedx.org/bom
```

### Use it

- **Open** an export with **File → Open Archive…** for a ZIP, or **Open Folder…** for an unpacked
  export. Alternatively, pass its path: `gobd-reader-win-x64.exe export.zip` on Windows,
  `./gobd-reader-linux-x64 export.zip` on Linux, and
  `"/Applications/GoBD Reader.app/Contents/MacOS/gobd-reader" export.zip` on macOS.
- **Wait, and watch.** Opening reads and checks every table, in the background, with progress
  measured in bytes read. The window stays usable throughout, and each table becomes readable as
  soon as its own import finishes rather than when the last one does. Closing the reader, or
  opening another export, stops the reading and removes everything it wrote. A multi-gigabyte
  export takes minutes before its summary is complete; that is the price of knowing.
- **Read the summary** on the first tab. It shows the verdict, each table with its record count
  or its findings, and the findings grouped by table — under the same codes and messages
  `gobd-validate --contents` reports for the same export. It cannot be closed.
- **Navigate** by medium and table. Beneath each table the navigator lists the tables it
  references and the tables that reference it, with the columns forming each key. Those entries
  are information: choosing one opens nothing.
- **Read** a consistent table as a grid in its own tab: its declared columns, declared value
  redefinitions, and two numbers on every row — `#`, where the record sits in what you are
  looking at, and **Record**, the number the file gives it, which is the number a finding cites.
  A table with no records opens as an empty grid. Each table has one tab, reused when the table
  is reached again and keeping its position when it is left; closing a tab leaves the others as
  they were.
- **Filter, sort and total** from the controls above the grid. Each column is offered what its
  declared type supports: a range for a number, a date or a time, and exact, contains, starts
  with, ends with, a `*`/`?` pattern or one-of for text, with an "ignore case" option. What you
  type is read under the table's own decimal and grouping symbols, its date mask and its
  two-digit-year window — never the machine's locale — and anything unreadable is refused with
  the form expected. Sort by up to three columns, in the order you add them; equal values keep
  file order and empty values sort last. Choose a figure per column — count, distinct, sum,
  minimum, maximum, average — and it is computed over the records the view holds, exactly, or it
  says it cannot. Averages are rounded to the column's own decimals and marked as rounded.
- **Know what you are looking at.** A filtered or sorted table says how many records it shows of
  how many, lists what is in force, and returns to file order in one click. Following a reference
  into a table whose filter hides the record lifts that filter, keeps the sort, and says so.
  Columns holding numbers longer than the reader can compute with exactly offer no filter, sort
  or figure; the summary lists them as limits of the reader, not as defects in the export.
- **See what is wrong** with a table whose records do not match their declaration. Its findings
  are shown instead of its data, and only that table is withheld. A reference that resolves to
  nothing is a finding, not a reason to withhold: such a table still shows its records.
- **Follow** a foreign key by clicking its cell. Values that take part in one are coloured and
  underlined, with the table they lead to in their tooltip; a value matching no record there is
  struck through wherever it appears, however many of them there are. Right-click a record to
  step through the records that refer to it — Previous and Next appear only for that walk, in
  that table's tab, and go when it ends.

Nothing is ever written to the export. A filter or a sort makes a view inside the reader's own
store, and every value shown is the value the file holds — the reader interprets values in order
to compare them, never in order to display them. The export is imported into that store under the
system temporary directory (`TMPDIR`, or `TEMP` on Windows), which is deleted when the reader
closes. A filtered or sorted table costs room there for as long as its tab holds it, because a
view carries the records it selects.

The store holds the export's data, so only the person running the reader can read it. On Linux and
macOS it lives in `gobd-reader-<user name>` under the temporary directory, readable by that account
alone; the reader refuses to open an export there if other accounts can read that directory, if
it is a symbolic link, or if it belongs to another account, and says which directory it refused.
Set `TMPDIR` to a directory of your own to put it elsewhere. On Windows it lives in `gobd-reader`
under `%TEMP%`, which Windows already keeps from other accounts.

## Building

Requires the .NET 10 SDK. Supported runtime identifiers are `linux-x64`, `linux-arm64`, `win-x64`,
`win-arm64` and `osx-arm64`.

```console
dotnet build
dotnet test
dotnet publish src/GoBd.Validation.Cli -c Release -r <rid> -p:PublishAot=true
dotnet publish src/GoBd.Reader.Ui -c Release -r <rid>
```

The validator publishes as a single native file, and only on the platform it is for. A build guard
fails the build if it ever comes to depend on the reader's store.

The reader publishes for every platform from any of them: as one trimmed file for `win-x64` and
`linux-x64`, and as a self-contained folder for `osx-arm64`.
`.github/scripts/assemble-reader-app.sh` turns that folder into `GoBD Reader.app` on any platform,
and `.github/scripts/make-reader-dmg.sh` finishes it into a disk image on a Mac.

A release takes its version from its tag, passed as `-p:ReleaseTag=v1.2.3`: that builds version
`1.2.3.0`, and a tag may leave out the patch or the minor version, as `v1.2` or `v1`. A tag of any
other form fails the build. A build without a tag reports `0.0.0.0`, so it cannot pass for a
release. `.github/scripts/make-sbom.sh` makes the release's bills of materials.

The reader's icon is drawn once, as `src/GoBd.Reader.Ui/Assets/gobd-reader.svg`.
`.github/scripts/make-reader-icons.sh` makes every other icon file from it, sized and padded for
each platform and losslessly compressed:
- the Windows `.ico`
- the macOS `.icns`
- the PNG the window shows on Linux
- the SVG at the top of this README.

Those files are committed, so building needs none of the script's tools. Run it after changing the
drawing. It needs Inkscape, oxipng, Python 3 and Node, and prints the path of a page that shows
every image on a light and a dark background.

## The standard

Published by Audicon GmbH; this validator implements **version 1.6 of 1 March 2019**.

- Documentation and downloads: <https://www.caseware.com/de/beschreibungsstandard>
- DTD archive:
  <https://cdn.prod.website-files.com/693916696fbefc2d8e49a0e7/6996a47cff41a53a66326a5d_gdpdu-01-03-2019.zip>

`src/GoBd.Validation/Resources/gdpdu-01-03-2019.dtd` is checked in and embedded in the binary,
because the standard requires every data carrier to ship it. The specification document is
published by Audicon GmbH and is **not** redistributed here; download it from the link above.

The `.dtd` file is authoritative where it and the document disagree, and they do: the document
prints both `Table*` and `Table+` for `Media`, and both a mandatory and an optional content
model for `Table`. Regression tests pin the DTD's reading.

## Licence

[MIT](LICENSE), covering the code, tests, build configuration and specifications. What it does not
cover, Audicon's grammar that every data carrier carries, is set out in [NOTICE](NOTICE), with how
the reader's icon was made. The components of other projects that the builds contain are named,
with their licences, in [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt).

Both tools carry these three texts inside themselves, and keep them apart as the files are:
`gobd-validate --license` prints the licence, `--notice` what it does not cover, and
`--third-party-notices` the notices of the components included. The reader shows all three under
**Help**, beside what it says about itself under **About GoBD Reader**.
