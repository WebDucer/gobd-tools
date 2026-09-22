## ADDED Requirements

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
