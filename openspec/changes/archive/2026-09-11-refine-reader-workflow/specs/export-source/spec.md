## MODIFIED Requirements

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
