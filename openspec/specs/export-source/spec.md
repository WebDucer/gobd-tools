# export-source

## Purpose

Presents a GoBD data-carrier export — whether delivered as a ZIP archive or as an unpacked
folder — as one uniform, read-only set of named entries, and resolves the standard's relative
`URL` values to those entries without ever escaping the export root.

## Requirements

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

#### Scenario: A folder named with a trailing separator
- **WHEN** the validator is given a folder path that ends in a path separator, such as `export/`,
  as shell completion writes it
- **THEN** it SHALL open the same export, with the same entries and the same findings, as for the
  path without the separator, rather than refusing every entry as outside the export root

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
directory or archive listing, and SHALL NOT read the content of a data file in order to
establish that it is present. Reading a data file's content SHALL happen only when a content
check is asked for, and SHALL never be required to answer whether a declared file is there.

#### Scenario: Declared table file is absent
- **WHEN** a `Table/URL` resolves to an entry that is not present in the export
- **THEN** the system SHALL report an error naming the table and the unresolved URL

#### Scenario: Multi-gigabyte data files are present
- **WHEN** the export contains data files of several gigabytes
- **THEN** validation SHALL complete without reading their contents and without extracting
  them to disk

#### Scenario: Presence is answered without opening the file
- **WHEN** the presence of every declared file is established
- **THEN** no data file SHALL have been opened, whether or not a content check is also requested

#### Scenario: Content is read only on request
- **WHEN** no content check is requested
- **THEN** the system SHALL behave exactly as it does today: the listing alone answers presence,
  and no data file is read

### Requirement: Entry name matching is case-sensitive and separator-normalised
The system SHALL compare entry names using ordinal, case-sensitive comparison after
normalising path separators, and SHALL report a distinct diagnostic when an entry differs from
the declared URL only by letter case.

#### Scenario: Declared URL differs only in case
- **WHEN** `Table/URL` is `Accounts.csv` and the export contains `accounts.csv`
- **THEN** the system SHALL report the file as not found and SHALL note that a case-insensitive
  match exists, because the export may be consumed on a case-sensitive filesystem

### Requirement: The listing never exposes anything outside the export root
The entry listing is what every later check trusts, so it is the boundary that keeps an
untrusted medium from reaching the rest of the filesystem. The system SHALL exclude from the
listing any entry that does not denote a file inside the export root, and SHALL confine every
read to that root regardless of how the entry was obtained.

#### Scenario: An entry name that escapes the root is not listed
- **WHEN** an entry's normalised name contains a `..` segment that leaves the export root, which
  can arise from a file whose literal name contains a backslash on a filesystem that permits one
- **THEN** the entry SHALL NOT appear in the listing, and SHALL NOT be found by a lookup

#### Scenario: A symbolic link is not listed
- **WHEN** the export contains a symbolic link
- **THEN** it SHALL NOT appear in the listing, because it denotes a file the medium does not
  carry and following it would read whatever it points at

#### Scenario: A symbolically linked directory is not descended into
- **WHEN** a directory inside the export is a symbolic link
- **THEN** no file beneath it SHALL appear in the listing, because such files are not links
  themselves and would otherwise arrive under ordinary in-root names

#### Scenario: A lookup and a read describe the same file
- **WHEN** an entry is found in the listing and then opened
- **THEN** the bytes read SHALL be those of the entry that was found, so that the size a check
  saw and the content it inspected cannot come from two different files

#### Scenario: Reads are confined even when a name reaches the index
- **WHEN** a read is attempted for a name that resolves outside the export root
- **THEN** the system SHALL refuse it rather than open the file, so that confinement does not
  depend on the listing having excluded it first

#### Scenario: A ZIP entry named to escape the root is not listed
- **WHEN** a ZIP archive declares a member whose name escapes the export root
- **THEN** the entry SHALL NOT appear in the listing, even though nothing is extracted and the
  name could not reach the filesystem today

#### Scenario: No hash or path of an out-of-root file can reach a report
- **WHEN** an export is crafted so that a file outside its root would be selected for inspection
- **THEN** no finding SHALL disclose that file's content, fingerprint or absolute path

### Requirement: Colliding entry names are reported
Two entries can resolve to one name, and which of them an importer reads is undefined. The
system SHALL report such a collision rather than resolving it silently, and SHALL NOT fail the
run because of one.

#### Scenario: Two files in a folder resolve to one name
- **WHEN** a folder export holds `data/x.csv` alongside a file whose literal name is
  `data\x.csv`, so both normalise to the same entry name
- **THEN** the system SHALL report an error naming the collision and how many entries share it

#### Scenario: A ZIP carries the same member name twice
- **WHEN** an archive declares the same member name more than once, which the format permits
- **THEN** the system SHALL report the collision, because differing tools may read different
  members and reach different conclusions from one medium

#### Scenario: A collision does not stop the run
- **WHEN** a collision is present
- **THEN** validation SHALL continue and report its other findings, rather than failing before
  it can report anything

#### Scenario: An export without collisions reports none
- **WHEN** every entry resolves to a distinct name
- **THEN** no collision SHALL be reported
