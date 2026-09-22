## ADDED Requirements

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

## MODIFIED Requirements

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
