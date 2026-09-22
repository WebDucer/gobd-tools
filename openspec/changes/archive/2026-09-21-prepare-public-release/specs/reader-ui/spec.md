## ADDED Requirements

### Requirement: The reader says what it is
The reader reaches a person as one file or one application, with nothing beside it. Which
version it is, under which licence it may be used, what that licence does not cover and whose code
it contains can therefore only be learned from the reader itself. The system SHALL show each of
these from its menu, on every platform, without any file beside the build, and SHALL keep them
apart, so that the licence is the licence and nothing else.

#### Scenario: The menus offer them
- **WHEN** a person opens the reader's menus
- **THEN** the licence, what that licence does not cover and the third-party notices SHALL be
  offered under Help, and the entry that shows the reader's version SHALL be offered where the
  platform puts it: under the application's own menu on macOS, and under Help elsewhere

#### Scenario: The version is shown
- **WHEN** a person chooses the entry for the reader's version
- **THEN** the reader SHALL show its name and the version of the build, as defined for releases

#### Scenario: The licence is shown
- **WHEN** a person chooses the licence
- **THEN** the reader SHALL show the licence it is distributed under, and nothing else

#### Scenario: What the licence does not cover is shown
- **WHEN** a person chooses what the licence does not cover
- **THEN** the reader SHALL show it, including that the grammar the reader carries is Audicon's
  work

#### Scenario: The third-party notices are shown
- **WHEN** a person chooses the third-party notices
- **THEN** the reader SHALL show the notices of every third-party component it contains, each
  with its licence, readable in full

#### Scenario: Available whatever the reader is doing
- **WHEN** a person opens one of these entries with no export open, or while an export is being
  read
- **THEN** the reader SHALL show it, and the reading SHALL carry on unaffected

### Requirement: The reader's store is private to the person running it
The store holds the export's data, extracted: accounting records, customer names and amounts. On
a machine several people use, a location every account can read hands that data to all of them.
A location owned by whoever ran the reader first locks everyone else out. The system SHALL keep
the store where only the person running the reader can read it. It SHALL refuse to open an
export rather than place the export's data where anyone else can read it.

#### Scenario: The store can be read by its owner alone
- **WHEN** an export is opened on Linux or macOS
- **THEN** the directory holding the store SHALL be readable, writable and enterable by the
  account running the reader and by no other account

#### Scenario: Two accounts read exports on one machine
- **WHEN** two accounts on the same machine each open an export with the reader
- **THEN** each SHALL open its export, and neither SHALL be able to read the other's store

#### Scenario: A location others can read is refused
- **WHEN** the directory the reader would keep its store in already exists and other accounts
  can read it, or it is a symbolic link
- **THEN** the reader SHALL NOT open the export, SHALL write nothing there, and SHALL say which
  directory it refused, why, and how to have the reader use another location

#### Scenario: A location another account owns is refused
- **WHEN** the directory the reader would keep its store in already exists and belongs to
  another account
- **THEN** the reader SHALL NOT open the export, and SHALL say which directory it could not use
  and how to have the reader use another location, rather than fail without explanation

#### Scenario: The temporary location the environment names is respected
- **WHEN** the person's environment names a temporary location of its own, such as through
  `TMPDIR`
- **THEN** the reader SHALL keep its private directory inside that location

#### Scenario: Windows
- **WHEN** the reader runs on Windows
- **THEN** the store SHALL stay in the person's own temporary directory, which Windows already
  keeps from other accounts
