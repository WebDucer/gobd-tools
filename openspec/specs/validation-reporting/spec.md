# validation-reporting

## Purpose

Defines what a validation result looks like — the finding, its stable code, its severity and
its location — and the two shapes that result is published in: readable text for a person and
stable JSON for CI pipelines and agents.

## Requirements

### Requirement: Every finding carries a code, severity, message and location
The system SHALL represent each validation outcome as a finding carrying a stable machine
identifier, a severity, a human-readable message, the location in `index.xml` that caused it,
and what the finding is about. What a finding is about SHALL be stated by the check that
produced it, not reconstructed by a reader from the finding's other content.

#### Scenario: Finding produced by a check
- **WHEN** any check reports a problem
- **THEN** the finding SHALL carry a stable code, one of the severities `error`, `warning` or
  `info`, a message, and the line and column in `index.xml` where the offending element begins

#### Scenario: Finding not attributable to a location
- **WHEN** a finding concerns the export as a whole rather than an element, such as a missing
  `index.xml`
- **THEN** the location SHALL be omitted rather than reported as a placeholder position

#### Scenario: A finding about something names what it is about
- **WHEN** a finding concerns a table, a medium, an extension or the DTD the export ships
- **THEN** it SHALL carry that thing's identity together with what kind of thing it is

#### Scenario: A finding about the export itself names nothing
- **WHEN** a finding concerns the export as a whole, such as a grammar violation or a failure of
  the validator
- **THEN** it SHALL carry no subject, so that grouping a report can tell the two cases apart

#### Scenario: What a finding is about does not depend on how its message is worded
- **WHEN** the arguments a check passes for its message are reordered
- **THEN** what the finding is reported to be about SHALL be unchanged, so that a published
  field cannot shift because prose was improved

#### Scenario: Two things sharing a name remain distinguishable
- **WHEN** a medium and a table are both named `Kunden`
- **THEN** the kind carried alongside the name SHALL distinguish them

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

### Requirement: A message stays readable in the JSON report
A finding's message is written for a person, and the JSON report is where a person most often
reads it — in a CI log, a pull-request comment, or an agent's output. The system SHALL write
message text so that characters carrying meaning to the reader survive serialisation as
themselves, escaping only what the JSON grammar requires.

#### Scenario: Quoted names survive as written
- **WHEN** a message quotes a table, column or file name using apostrophes
- **THEN** the JSON report SHALL contain those apostrophes as themselves, not as numeric escapes

#### Scenario: German text survives as written
- **WHEN** a report is rendered in German, whose messages contain characters such as `ä`, `ü`
  and `Ä`
- **THEN** the JSON report SHALL contain those characters as themselves, so that a German
  report is as readable as an English one

#### Scenario: The characters a finding is about survive as themselves
- **WHEN** a finding reports which characters the standard forbids in a name, namely `"`, `&`,
  `<` and `>`
- **THEN** the JSON report SHALL render those characters so the reader can see which ones were
  found, since a finding whose subject is a character is useless when that character is escaped

#### Scenario: The document remains valid JSON
- **WHEN** a message contains a character the JSON grammar requires to be escaped, such as a
  quotation mark, a backslash or a control character
- **THEN** the system SHALL escape it, and the report SHALL remain parseable as a single JSON
  document

#### Scenario: Prose supplied by a check is rendered in the report's language
- **WHEN** a finding's message includes wording chosen by the check rather than copied from
  `index.xml`, such as naming which encoding artefact differs
- **THEN** that wording SHALL appear in the language the report is rendered in, so that a German
  report contains no English phrases

#### Scenario: Readability does not change what a parser sees
- **WHEN** the same findings are serialised before and after this change
- **THEN** a JSON parser SHALL yield identical values for every field, because only the written
  form differs, and the report's schema version SHALL therefore be unchanged

### Requirement: Every finding code is documented
Finding codes are the interface downstream systems suppress and track by, and the identifier a
person searches for when a build fails. The project SHALL carry a reference documenting every
code the system can emit, and that reference SHALL be verified against the code catalogue rather
than maintained by good intentions.

#### Scenario: Every code the system can emit is documented
- **WHEN** the code catalogue is compared against the reference
- **THEN** every code in the catalogue SHALL have an entry in the reference

#### Scenario: The reference documents no code that does not exist
- **WHEN** the reference is compared against the code catalogue
- **THEN** every code documented SHALL exist in the catalogue, so that a removed or renamed code
  cannot leave a stale entry behind

#### Scenario: Adding a code without documenting it fails the build
- **WHEN** a new finding code is added to the catalogue and no entry is written for it
- **THEN** the verification SHALL fail, because a reference that silently falls behind is worse
  than none once people rely on it

#### Scenario: The reference agrees with the catalogue about severity and category
- **WHEN** an entry states a code's severity or its category
- **THEN** that statement SHALL match the catalogue, so the reference cannot disagree about
  whether a code counts against the verdict

#### Scenario: An entry explains rather than restates
- **WHEN** a reader consults an entry for a code
- **THEN** it SHALL say what the finding means, what triggers it, why it carries the severity it
  does, and what a producer should change — not merely repeat the message text, which the reader
  already has
