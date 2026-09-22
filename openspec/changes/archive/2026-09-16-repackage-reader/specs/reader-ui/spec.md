## MODIFIED Requirements

### Requirement: The reader is distributed as a runnable build per platform
A reader that has to be built from source is a reader an auditor does not have, and a reader that
arrives as hundreds of files is one they first have to be told how to start. The system SHALL be
distributable as a build that runs on a supported platform without anything being installed
alongside it, and SHALL reach a person as one thing to download and start: one executable file on
Windows and Linux, and one application on macOS, delivered in a disk image.

#### Scenario: Running on a machine without .NET installed
- **WHEN** the published build is run on a supported platform with no .NET runtime present
- **THEN** it SHALL start and open an export

#### Scenario: The store engine travels with the build
- **WHEN** the published build is run on a machine that has never had a database engine installed
- **THEN** importing an export SHALL still work, because the build carries the store it needs

#### Scenario: Every supported platform is built
- **WHEN** the project's pipeline builds a revision
- **THEN** it SHALL produce a downloadable reader build for each platform the CLI is built for

#### Scenario: One executable file on Windows and Linux
- **WHEN** a person downloads the reader for Windows or Linux
- **THEN** the download SHALL be one executable file that starts as it stands, without being
  unpacked or installed first, and nothing else SHALL be needed beside it

#### Scenario: An application on macOS
- **WHEN** a person opens the macOS download
- **THEN** it SHALL present one application that can be copied to the Applications folder and
  started from there, and macOS SHALL show that application under the name GoBD Reader

#### Scenario: The macOS application verifies as a whole
- **WHEN** macOS checks the code signature of the application as delivered
- **THEN** the signature SHALL cover the whole application and verify, so that the platform does
  not report the application as damaged

#### Scenario: No debug symbol files are delivered
- **WHEN** a reader build is published for any platform
- **THEN** it SHALL carry no separate debug symbol files, because they serve the people who build
  the reader and not the people who use it

## ADDED Requirements

### Requirement: A build that unpacks itself leaves no copies of other versions behind
The executable file on Windows and Linux unpacks the native libraries it carries when it first
starts, and reuses them on later starts. Each version unpacks a copy of its own, so every update
would otherwise leave one behind, on a machine the reader is meant to leave as it found it. The
system SHALL remove, when it starts, the unpacked copies that other versions of the reader left,
and SHALL NOT remove a copy that a running reader is using.

#### Scenario: Copies no running reader uses are removed
- **WHEN** the reader starts and unpacked copies of other versions exist that no running reader
  uses
- **THEN** those copies SHALL be removed

#### Scenario: A running reader keeps its copy
- **WHEN** the reader starts while a reader of another version is running
- **THEN** the copy that the running reader uses SHALL be left in place, so that it can still
  open an export afterwards

#### Scenario: The reader keeps its own copy
- **WHEN** the reader starts
- **THEN** its own unpacked copy SHALL remain, so that the next start does not unpack again

#### Scenario: Nothing but the reader's own copies is touched
- **WHEN** unpacked copies are removed
- **THEN** nothing outside the location where the reader's own copies are unpacked SHALL be
  removed, including the unpacked copies of other applications beside it

#### Scenario: A copy that cannot be removed does not stop the reader
- **WHEN** an unpacked copy of another version cannot be removed
- **THEN** the reader SHALL start and work as usual, and SHALL try to remove that copy again on a
  later start

#### Scenario: A build that does not unpack itself removes nothing
- **WHEN** the reader runs from a build that does not unpack itself, such as the macOS application
  or a build made for development
- **THEN** it SHALL NOT remove anything as part of starting
