## MODIFIED Requirements

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
