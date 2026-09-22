## MODIFIED Requirements

### Requirement: The command surface states the scope of the check
Because a clean result does not mean more was checked than was asked for, the system SHALL make
the boundary of the check discoverable from the command line itself, and SHALL describe the
boundary of the mode being run rather than a fixed one.

#### Scenario: Help output
- **WHEN** the user requests help
- **THEN** the output SHALL state that validation covers `index.xml`, the DTD and the export's
  file listing, that data file contents are examined only when a content check is requested, and
  how to request one

#### Scenario: Report states what was checked
- **WHEN** a report is produced
- **THEN** it SHALL state whether data file contents were examined, so that a clean result is
  never read as a stronger claim than it is

## ADDED Requirements

### Requirement: Content checking is opt-in from the command line
Reading data files costs time proportional to the export, which a caller validating only the
description should not pay. The system SHALL check data file contents only when asked, and SHALL
make the request explicit on the command line.

#### Scenario: Content check requested
- **WHEN** the user runs the command with the content check requested
- **THEN** the system SHALL read the declared data files, check them, and report their findings
  alongside the structural ones

#### Scenario: Content check not requested
- **WHEN** the user runs the command without requesting a content check
- **THEN** no data file SHALL be read, and the run SHALL cost what it costs today

#### Scenario: The verdict accounts for content findings
- **WHEN** a content check reports an error
- **THEN** the overall verdict and the exit code SHALL reflect it under the same rules that
  govern structural findings

### Requirement: The reporting bound is adjustable from the command line
Analysis of a defective table stops at a bounded number of findings, and how much a caller wants
to see of a badly broken export differs between a pipeline gate and someone diagnosing it. The
system SHALL apply a default bound without being asked and SHALL let the caller change it.

#### Scenario: Default bound
- **WHEN** the user requests a content check without specifying a bound
- **THEN** a default bound SHALL apply per table, so that a wholly defective export costs
  seconds rather than a full read

#### Scenario: Bound raised
- **WHEN** the user specifies a larger bound
- **THEN** the report SHALL carry correspondingly more findings per table, and those SHALL
  include every finding the smaller bound would have reported

#### Scenario: The bound does not affect the verdict
- **WHEN** two runs of the same export use different bounds
- **THEN** both SHALL reach the same verdict, because whether an export is conformant does not
  depend on how much of its defect list was printed

### Requirement: The command remains a single self-contained executable
The reason a pipeline can adopt this tool is that it is one file with nothing to install. The
system SHALL perform content checks without acquiring a dependency that cannot be published into
a single native executable.

#### Scenario: Content checking in a published binary
- **WHEN** the published native executable is run with the content check requested
- **THEN** it SHALL perform the check without requiring any component beyond that executable

### Requirement: An export beyond the command's capacity is reported, not guessed at
Checking keys requires holding every key of a table, so a sufficiently large table exceeds what
a single process can do. The system SHALL say so plainly rather than failing obscurely or
reporting an unchecked table as clean.

#### Scenario: A table too large to check keys
- **WHEN** a table exceeds the number of records for which the command can check keys
- **THEN** the system SHALL report that the check was not performed for that table, and the
  verdict SHALL NOT be conformant on the strength of a check that did not run

#### Scenario: The limit is discoverable
- **WHEN** the user requests help
- **THEN** the output SHALL make it discoverable that key checking has a capacity limit and what
  to do when an export exceeds it
