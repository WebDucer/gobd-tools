## Purpose

The command-line entry point a producer runs by hand or wires into an export pipeline: it takes
an export path, chooses a report format, and communicates the verdict through an exit code that
a build step can act on.

## ADDED Requirements

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
Because a clean v1 result does not mean the data files are well formed, the system SHALL make
the boundary of the check discoverable from the command line itself.

#### Scenario: Help output
- **WHEN** the user requests help
- **THEN** the output SHALL state that validation covers `index.xml`, the DTD and the export's
  file listing, and that data file contents are not examined

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
