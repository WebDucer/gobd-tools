# dtd-validation

## Purpose

Establishes that an export's `index.xml` conforms to the grammar of Beschreibungsstandard 1.6,
using a canonical DTD the application carries itself, and separately reports on the DTD copy
that the standard requires the export to ship.

## Requirements

### Requirement: Validate index.xml against the canonical embedded 1.6 DTD
The system SHALL validate `index.xml` against the Beschreibungsstandard 1.6 DTD
(`gdpdu-01-03-2019.dtd`) that is embedded in the application, and SHALL NOT use any DTD file
found inside the export to drive that validation.

#### Scenario: Conformant index.xml
- **WHEN** an `index.xml` conforms to the 1.6 grammar
- **THEN** the system SHALL report no DTD-conformance findings

#### Scenario: Non-conformant index.xml
- **WHEN** an `index.xml` violates the 1.6 grammar, such as a `Table` that contains neither
  `VariableLength` nor `FixedLength`
- **THEN** the system SHALL report an error carrying the parser's message and the line and
  column within `index.xml`

#### Scenario: Export ships a modified DTD
- **WHEN** the export contains a DTD file whose content differs from the canonical DTD
- **THEN** grammar validation SHALL still be performed against the canonical embedded DTD, so
  that the verdict cannot be influenced by the export's own DTD copy

#### Scenario: Multiple grammar violations
- **WHEN** `index.xml` contains more than one grammar violation
- **THEN** the system SHALL report all violations it can recover from rather than stopping at
  the first

### Requirement: Accept documents written against earlier standard versions
Because the 1.6 grammar is a strict superset of 1.1, the system SHALL validate every export
against the 1.6 grammar alone and SHALL NOT branch on a declared standard version.

#### Scenario: Document valid under 1.1 conventions
- **WHEN** an `index.xml` uses no `Extension`, `Alias` or `AcceptNoTables` elements and would
  have been valid under standard 1.1
- **THEN** it SHALL validate successfully against the 1.6 grammar

#### Scenario: DOCTYPE names the 2002 DTD
- **WHEN** `index.xml` declares `SYSTEM "gdpdu-01-08-2002.dtd"`, as the 1.6 document's own
  examples do
- **THEN** the system SHALL still validate against the canonical 1.6 grammar and SHALL record
  the declared system identifier as an informational finding, not an error

### Requirement: Report on the DTD file shipped inside the export
The standard requires the data carrier to carry the DTD file alongside `index.xml`, and
forbids modifying it. The system SHALL report on that copy with a graded outcome that
distinguishes a missing DTD, a byte-identical DTD, a DTD differing only in encoding artefacts,
and a semantically altered DTD. Every such finding SHALL identify which side differs, so that a
reader can tell a re-encoded export from a defective validator without inspecting either.

#### Scenario: No DTD file in the export
- **WHEN** the export contains no DTD file
- **THEN** the system SHALL report an error stating that the data carrier must include the DTD

#### Scenario: DTD is byte-identical to the canonical file
- **WHEN** the export's DTD file matches the canonical DTD byte for byte
- **THEN** the system SHALL report no finding for the DTD copy

#### Scenario: DTD differs only in line endings or byte-order mark
- **WHEN** the export's DTD file is identical to the canonical DTD after normalising line
  endings, a leading byte-order mark, and trailing whitespace
- **THEN** the system SHALL report a warning describing the difference as a packaging artefact
  rather than an error, because re-packaging an export commonly rewrites line endings

#### Scenario: DTD content has been altered
- **WHEN** the export's DTD file still differs from the canonical DTD after that normalisation
- **THEN** the system SHALL report an error stating that the DTD must not be modified

#### Scenario: A reported difference names both fingerprints
- **WHEN** the system reports that the export's DTD differs from the canonical grammar, whether
  as an encoding artefact or as an alteration
- **THEN** the finding SHALL carry both the expected fingerprint and the fingerprint observed in
  the export

#### Scenario: An encoding difference names what actually differs
- **WHEN** the export's DTD differs only in encoding artefacts
- **THEN** the finding SHALL name which of line endings, byte-order mark or trailing whitespace
  accounts for the difference, rather than listing all three as possibilities

### Requirement: Contain DTD processing within the export
The system SHALL resolve the document type declaration to its own embedded canonical DTD and
SHALL refuse to fetch any external resource named by `index.xml`, whether by network, by
filesystem path, or by any other scheme.

#### Scenario: index.xml points its DOCTYPE at a local file
- **WHEN** `index.xml` declares `SYSTEM "file:///etc/passwd"`
- **THEN** the system SHALL refuse to resolve it, SHALL report an error, and SHALL NOT read
  the named file

#### Scenario: index.xml points its DOCTYPE at a network location
- **WHEN** `index.xml` declares a DOCTYPE with an `http://` or `ftp://` system identifier
- **THEN** the system SHALL refuse to resolve it and SHALL NOT perform any network request

#### Scenario: Entity expansion is bounded
- **WHEN** `index.xml` declares an internal subset whose entity expansion is disproportionate
  to the document size
- **THEN** the system SHALL abort parsing with an error rather than exhausting memory

### Requirement: Verify the embedded canonical grammar before using it
The grammar the application carries is defined by a fingerprint pinned in the application, not
by whatever bytes happen to be embedded. The system SHALL verify the embedded grammar against
that fingerprint before validating any export, and SHALL refuse to report a conformance verdict
when the check fails.

#### Scenario: Embedded grammar matches the pinned fingerprint
- **WHEN** a run begins and the embedded grammar's SHA-256 equals the pinned fingerprint
- **THEN** validation SHALL proceed and no finding SHALL be raised for the embedded grammar

#### Scenario: Embedded grammar does not match the pinned fingerprint
- **WHEN** the embedded grammar's SHA-256 differs from the pinned fingerprint
- **THEN** the system SHALL report an error identifying it as a failure of the validator itself,
  quoting the expected and the observed fingerprint

#### Scenario: A failed self-check is not reported as a bad export
- **WHEN** the embedded grammar fails its self-check
- **THEN** the finding SHALL belong to the tool-failure category, so that the run exits with the
  code reserved for "the validator could not run" rather than the code for a non-conformant
  export

#### Scenario: The pinned fingerprint is stated independently of the embedded file
- **WHEN** the embedded grammar file is replaced
- **THEN** the pinned fingerprint SHALL NOT be derived from the replacement, so that a
  substituted grammar fails verification rather than redefining what is canonical
