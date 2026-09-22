## Purpose

Defines what a validation result looks like — the finding, its stable code, its severity and
its location — and the two shapes that result is published in: readable text for a person and
stable JSON for CI pipelines and agents.

## ADDED Requirements

### Requirement: Every finding carries a code, severity, message and location
The system SHALL represent each validation outcome as a finding carrying a stable machine
identifier, a severity, a human-readable message, and the location in `index.xml` that caused
it.

#### Scenario: Finding produced by a check
- **WHEN** any check reports a problem
- **THEN** the finding SHALL carry a stable code, one of the severities `error`, `warning` or
  `info`, a message, and the line and column in `index.xml` where the offending element begins

#### Scenario: Finding not attributable to a location
- **WHEN** a finding concerns the export as a whole rather than an element, such as a missing
  `index.xml`
- **THEN** the location SHALL be omitted rather than reported as a placeholder position

### Requirement: Finding codes are stable and namespaced
Downstream systems suppress and track individual checks by code. The system SHALL assign each
distinct check a unique, namespaced, stable code and SHALL NOT reuse a retired code for a
different check.

#### Scenario: Same defect across runs
- **WHEN** the same defect is validated by two different releases of the tool
- **THEN** it SHALL be reported under the same code

### Requirement: Severity reflects conformance, not confidence
The system SHALL assign `error` only where the standard is violated, `warning` where an export
is permitted but suspect, and `info` where a fact is worth surfacing but implies no defect.

#### Scenario: Violation of the standard
- **WHEN** a check finds a dangling `References`, which the standard requires to be consistent
- **THEN** the finding SHALL be an `error`

#### Scenario: Permitted but suspect construct
- **WHEN** a check finds a `Media` with no tables and no `AcceptNoTables`
- **THEN** the finding SHALL be a `warning`

#### Scenario: Neutral observation
- **WHEN** a check records that the export declares the 2002 DTD system identifier
- **THEN** the finding SHALL be `info`

### Requirement: A run yields an overall verdict
The system SHALL derive a single verdict for each run from the findings it produced.

#### Scenario: No errors
- **WHEN** a run produces no `error` findings
- **THEN** the export SHALL be reported as conformant, whether or not warnings were produced

#### Scenario: At least one error
- **WHEN** a run produces one or more `error` findings
- **THEN** the export SHALL be reported as non-conformant

#### Scenario: Strict mode
- **WHEN** strict mode is requested
- **THEN** `warning` findings SHALL be treated as errors for the purposes of the verdict, while
  retaining their original severity in the report

### Requirement: Text report is for human reading
The system SHALL provide a text report that leads with the verdict, groups findings by the
table or element they concern, and orders them by severity.

#### Scenario: Report with findings
- **WHEN** a text report is produced for a run with findings
- **THEN** it SHALL state the verdict, the counts per severity, and each finding with its code,
  severity, location and message

#### Scenario: Clean report states the scope of the check
- **WHEN** a text report is produced for a run with no findings
- **THEN** it SHALL state that the export is conformant and SHALL state explicitly that data
  file contents were not examined, so that a clean result is not mistaken for a full check

### Requirement: JSON report is stable and machine-consumable
The system SHALL provide a JSON report intended for CI pipelines and automated agents, carrying
a schema version so consumers can detect format changes.

#### Scenario: JSON report structure
- **WHEN** a JSON report is produced
- **THEN** it SHALL be a single JSON document carrying a schema version, the export path, the
  verdict, per-severity counts, and the full array of findings with their codes, severities,
  messages and locations

#### Scenario: JSON report on a clean run
- **WHEN** a JSON report is produced for a run with no findings
- **THEN** it SHALL contain an empty findings array rather than omitting the field

#### Scenario: JSON report on a failed run
- **WHEN** validation cannot proceed, for example because the export path does not exist
- **THEN** a JSON report SHALL still be produced, carrying the failure as a finding

#### Scenario: JSON is the only content on standard output
- **WHEN** the JSON reporter writes to standard output
- **THEN** no progress text, banner or warning SHALL be interleaved with it, so that the stream
  can be parsed directly

### Requirement: Localisation must not change a report's machine identity
Finding codes are the interface downstream systems depend on, and message text is not. The
system SHALL localise only human-readable message text, and SHALL keep finding codes,
severities and the JSON report's structure and field names identical across languages.

#### Scenario: Same defect reported in two languages
- **WHEN** the same defect is validated once in English and once in German
- **THEN** both runs SHALL produce the same finding code, the same severity and the same
  verdict, differing only in message text

#### Scenario: JSON structure is language-neutral
- **WHEN** a JSON report is produced in German
- **THEN** its field names, severity values and schema version SHALL be identical to those of
  an English run, so that a consumer needs no knowledge of the language used

#### Scenario: JSON report records the language used
- **WHEN** a JSON report is produced
- **THEN** it SHALL record which language the message text was rendered in
