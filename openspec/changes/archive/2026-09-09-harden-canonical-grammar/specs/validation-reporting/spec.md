## ADDED Requirements

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
