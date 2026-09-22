## Purpose

Presents a GoBD data-carrier export — whether delivered as a ZIP archive or as an unpacked
folder — as one uniform, read-only set of named entries, and resolves the standard's relative
`URL` values to those entries without ever escaping the export root.

## ADDED Requirements

### Requirement: Accept both ZIP and folder exports
The system SHALL accept a GoBD export supplied either as a ZIP archive or as an unpacked
folder, and SHALL expose both through a single uniform entry-access contract so that all
downstream validation behaves identically regardless of packaging.

#### Scenario: Export supplied as a ZIP archive
- **WHEN** the validator is given a path to a `.zip` file containing `index.xml`
- **THEN** it SHALL enumerate the archive's entries and locate `index.xml` without extracting
  the archive to disk

#### Scenario: Export supplied as an unpacked folder
- **WHEN** the validator is given a path to a directory containing `index.xml`
- **THEN** it SHALL enumerate the directory tree and produce the same entry set it would
  produce for the equivalent ZIP archive

#### Scenario: Source kind is detected from the path
- **WHEN** the validator is given a path without an explicit source-kind option
- **THEN** it SHALL treat a directory as a folder export and a regular file as a ZIP archive

### Requirement: Locate index.xml at the export root
The system SHALL require exactly one `index.xml` at the root of the export and SHALL report an
error when it is absent.

#### Scenario: index.xml is missing
- **WHEN** the export contains no `index.xml` at its root
- **THEN** validation SHALL stop and report an error identifying the export path

#### Scenario: index.xml exists only in a subdirectory
- **WHEN** `index.xml` is present only beneath a subdirectory of the export root
- **THEN** validation SHALL report an error stating that `index.xml` must be at the root

### Requirement: Resolve relative URLs within the export root
The system SHALL resolve each `Table/URL` value relative to the location of `index.xml`, SHALL
support the relative forms the standard permits (including `../` segments), and SHALL reject
any value that resolves outside the export root.

#### Scenario: Simple and nested relative URLs resolve
- **WHEN** a `Table/URL` is `Accounts.dat` or `data/january/Accounts.dat`
- **THEN** it SHALL resolve to the corresponding entry relative to `index.xml`

#### Scenario: Absolute URLs are rejected
- **WHEN** a `Table/URL` uses an absolute form such as `http://`, `ftp://`, `file://localhost/`
  or `file:///`
- **THEN** the system SHALL report an error stating that only relative URLs are permitted

#### Scenario: Traversal outside the export root is rejected
- **WHEN** a `Table/URL` resolves to a location outside the export root
- **THEN** the system SHALL report an error and SHALL NOT read the target

### Requirement: Report entry presence without reading entry content
The system SHALL determine whether each referenced entry exists using only the export's
directory or archive listing, and SHALL NOT read the content of any data file.

#### Scenario: Declared table file is absent
- **WHEN** a `Table/URL` resolves to an entry that is not present in the export
- **THEN** the system SHALL report an error naming the table and the unresolved URL

#### Scenario: Multi-gigabyte data files are present
- **WHEN** the export contains data files of several gigabytes
- **THEN** validation SHALL complete without reading their contents and without extracting
  them to disk

### Requirement: Entry name matching is case-sensitive and separator-normalised
The system SHALL compare entry names using ordinal, case-sensitive comparison after
normalising path separators, and SHALL report a distinct diagnostic when an entry differs from
the declared URL only by letter case.

#### Scenario: Declared URL differs only in case
- **WHEN** `Table/URL` is `Accounts.csv` and the export contains `accounts.csv`
- **THEN** the system SHALL report the file as not found and SHALL note that a case-insensitive
  match exists, because the export may be consumed on a case-sensitive filesystem
