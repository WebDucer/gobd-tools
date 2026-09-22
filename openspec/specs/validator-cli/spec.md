# validator-cli

## Purpose

The command-line entry point a producer runs by hand or wires into an export pipeline: it takes
an export path, chooses a report format, and communicates the verdict through an exit code that
a build step can act on.

## Requirements

### Requirement: Validate an export given its path
The system SHALL provide a command that takes the path of a GoBD export — a ZIP archive or a
folder — and validates it.

#### Scenario: Validating a ZIP archive
- **WHEN** the user runs the validate command against a path to a `.zip` file
- **THEN** the system SHALL validate the archive and emit a report

#### Scenario: Validating a folder
- **WHEN** the user runs the validate command against a path to a directory
- **THEN** the system SHALL validate the folder and emit a report

#### Scenario: Path does not exist
- **WHEN** the given path does not exist
- **THEN** the system SHALL emit a report carrying that failure and SHALL exit with the code
  reserved for tool-level failure

### Requirement: Report format is selectable
The system SHALL let the caller choose between the text and JSON report formats, defaulting to
text for interactive use.

#### Scenario: No format requested
- **WHEN** no format option is given
- **THEN** the text report SHALL be produced

#### Scenario: JSON requested
- **WHEN** the JSON format is requested
- **THEN** the JSON report SHALL be written to standard output as the only content on that
  stream

#### Scenario: Report written to a file
- **WHEN** an output file is specified
- **THEN** the report SHALL be written to that file in the selected format, and standard output
  SHALL be left free for a short human-readable summary

### Requirement: Exit codes communicate the verdict
The system SHALL distinguish, by exit code, a conformant export, an export with warnings only,
a non-conformant export, and a failure of the tool itself, so that a pipeline can act on each.

#### Scenario: Conformant export
- **WHEN** validation produces no findings above `info`
- **THEN** the process SHALL exit with code `0`

#### Scenario: Warnings only
- **WHEN** validation produces warnings but no errors
- **THEN** the process SHALL exit with code `1`

#### Scenario: Errors present
- **WHEN** validation produces at least one error
- **THEN** the process SHALL exit with code `2`

#### Scenario: Tool failure
- **WHEN** the tool cannot complete validation, for instance because the path is unreadable or
  the archive is corrupt
- **THEN** the process SHALL exit with a code distinct from the verdict codes above

#### Scenario: Strict mode promotes warnings
- **WHEN** strict mode is requested and validation produces warnings but no errors
- **THEN** the process SHALL exit with code `2`

### Requirement: Ship as a self-contained native executable
The system SHALL be distributable as a single self-contained native executable per supported
platform, requiring no .NET runtime on the machine that runs it.

#### Scenario: Running on a machine without .NET installed
- **WHEN** the executable is run on a supported platform with no .NET runtime present
- **THEN** it SHALL validate an export successfully

#### Scenario: Canonical DTD requires no external file
- **WHEN** the executable is run in a directory containing no DTD file
- **THEN** validation SHALL still use the canonical DTD carried inside the executable

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

### Requirement: Report language is selectable and defaults to the operating system's
Because GoBD is a German regime, the system SHALL render human-readable output in German when
the operating system is configured for German or when German is requested, and in English
otherwise. The system SHALL resolve the language from, in order of precedence, an explicit
command-line option, an environment variable, the operating system's language, and finally
English.

#### Scenario: Explicit option overrides everything
- **WHEN** the caller requests a language on the command line
- **THEN** output SHALL be rendered in that language regardless of the environment or the
  operating system's configuration

#### Scenario: German operating system
- **WHEN** no language is requested and the operating system reports German
- **THEN** human-readable output SHALL be rendered in German

#### Scenario: Non-German operating system
- **WHEN** no language is requested and the operating system reports a language other than
  German
- **THEN** human-readable output SHALL be rendered in English

#### Scenario: Unsupported language requested
- **WHEN** the caller requests a language the system does not provide
- **THEN** output SHALL be rendered in English and the system SHALL report an informational
  finding naming the unsupported language, rather than failing

#### Scenario: Operating system language cannot be determined
- **WHEN** the operating system's language cannot be determined
- **THEN** output SHALL be rendered in English rather than the run failing

### Requirement: Numeric and date rendering must not vary with language
The verdict, the counts and every value quoted from `index.xml` SHALL be rendered identically
regardless of the selected language or the operating system's configuration, so that only
translated prose differs between runs.

#### Scenario: Same findings rendered under different operating system languages
- **WHEN** the same export is validated on a machine configured for German and on one
  configured for English, with the language option held fixed
- **THEN** the rendered reports SHALL be identical, including every number and quoted value

#### Scenario: Values quoted from index.xml are reproduced verbatim
- **WHEN** a finding quotes a value taken from `index.xml`, such as a declared `Accuracy`, a
  `DecimalSymbol` or a date mask
- **THEN** the value SHALL be reproduced exactly as it appears in the document, never
  reformatted according to any culture

#### Scenario: Declared symbols govern interpretation
- **WHEN** the system interprets a numeric or date value declared in `index.xml`
- **THEN** it SHALL apply the `DecimalSymbol`, `DigitGroupingSymbol`, `Format` and `Epoch`
  declared for that table or column, and SHALL NOT consult the operating system's culture

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

### Requirement: The executable says what it is
The executable is the whole delivery. It is downloaded, copied into a pipeline and run with
nothing beside it, so everything a person needs to know about what they are running has to come
from the executable itself: which version it is, under which licence it may be used, what that
licence does not cover, and whose code it contains. The system SHALL report each of these from the
command line, without any file beside the executable, and SHALL keep them apart, so that the
licence is the licence and nothing else.

#### Scenario: Version requested
- **WHEN** the user runs the command with the version option
- **THEN** it SHALL print the version of the build, as defined for releases, and exit with code
  `0`

#### Scenario: Licence requested
- **WHEN** the user runs the command with the licence option
- **THEN** it SHALL print the licence the tool is distributed under, and nothing else, and exit
  with code `0`

#### Scenario: What the licence does not cover requested
- **WHEN** the user runs the command with the option for what the licence does not cover
- **THEN** it SHALL print that, including that the grammar the executable carries is Audicon's
  work, and exit with code `0`

#### Scenario: Third-party notices requested
- **WHEN** the user runs the command with the notices option
- **THEN** it SHALL print the notices of every third-party component the executable contains,
  each with its licence, and exit with code `0`

#### Scenario: Nothing beside the executable
- **WHEN** the executable is run from a directory that contains no other file, with any of these
  options
- **THEN** it SHALL print what was asked for in full

#### Scenario: Asking what it is validates nothing
- **WHEN** any of these options is given together with an export path
- **THEN** the command SHALL print what was asked for, SHALL NOT read the export, and SHALL exit
  with code `0`

#### Scenario: Help lists the options
- **WHEN** the user requests help
- **THEN** the output SHALL list the options for the licence, for what it does not cover and for
  the third-party notices, beside the version option
