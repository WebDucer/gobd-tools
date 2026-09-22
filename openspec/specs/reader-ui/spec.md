# reader-ui

## Purpose

Defines how a person navigates and reads a GoBD export: the declared structure they move
through, the conditions under which a table's data is shown at all, and how a foreign key
carries them from one table to another.

## Requirements

### Requirement: The export is navigated by its declared structure
The relationships between tables form a directed graph — a table may reference several others,
several may reference it, references may cross media, and a table may reference itself — so no
single tree can present them without either duplicating tables or omitting them. The system
SHALL therefore navigate by the structure `index.xml` actually declares, which is a tree, and
SHALL show each table's relationships as information about that table rather than as further
levels of the tree.

#### Scenario: The navigator presents the declared hierarchy
- **WHEN** an export is opened
- **THEN** the system SHALL present its media and, beneath each, the tables that medium carries

#### Scenario: References are presented per table, not as hierarchy
- **WHEN** the navigator lists a table
- **THEN** it SHALL list beneath that table the tables it references and the tables that
  reference it, each with the columns forming the key, and a listed relationship SHALL NOT
  expand into relationships of its own

#### Scenario: Relationships are information, not navigation
- **WHEN** a person chooses a relationship listed beneath a table
- **THEN** the system SHALL NOT open or reposition any table, because following a reference is an
  action on a record's value rather than on a table

#### Scenario: A table that references itself
- **WHEN** a table references itself
- **THEN** it SHALL be listed both among its own references and among its own referrers

#### Scenario: The navigator indicates the table in view
- **WHEN** the table in view changes, whether it was chosen in the navigator or reached by
  following a reference
- **THEN** the navigator SHALL indicate that table, so that what is selected and what is shown
  never disagree

### Requirement: A table's data is shown only when the table is consistent
Rendering a value the system could not interpret would present a guess as a fact. The system
SHALL show a table's data only when that table conforms to its declaration, and SHALL otherwise
present what is wrong with it.

#### Scenario: A consistent table
- **WHEN** a table is opened and its records conform to its declaration
- **THEN** the system SHALL present its data

#### Scenario: A table that does not conform
- **WHEN** a table is opened and any record does not conform to its declaration
- **THEN** the system SHALL present that table's findings instead of its data

#### Scenario: One defective table does not withhold the others
- **WHEN** one table of an export does not conform
- **THEN** every other table SHALL remain openable and readable, because the gate is per table

#### Scenario: A reference that does not resolve does not withhold the table
- **WHEN** a table's records conform to their declaration but some of its foreign key values
  match no record of the referenced table
- **THEN** the system SHALL present the table's data, and SHALL report those references as
  findings rather than withhold the table, because a record whose reference is broken is still a
  record someone needs to read

#### Scenario: A table with no records
- **WHEN** a table conforms to its declaration and holds no records
- **THEN** the system SHALL present it as a table of no records under its declared columns,
  rather than as a defect or as nothing at all, because that a table is empty is itself
  something a reader needs to be able to see

#### Scenario: A table that could not be read at all
- **WHEN** presenting a table fails for a reason the system did not anticipate
- **THEN** it SHALL present that failure as what is wrong with that table, and every other table
  SHALL remain openable, because a reader that ends mid-session takes the whole export with it

#### Scenario: The findings shown are the bounded set
- **WHEN** a defective table is presented
- **THEN** the findings shown SHALL be those the analysis reports, and where analysis stopped at
  its bound the presentation SHALL say so rather than implying the list is exhaustive

### Requirement: Data is presented as declared, and never altered
The value of reading an export here rather than in a spreadsheet is that the declaration is
applied. The system SHALL render each column under the name and type the export declares, SHALL
present every value as the file stores it, and SHALL present records in the order the file
delivers them unless a person asks for another view of them. No view SHALL alter a value, and
every record SHALL remain citable by the number the file gives it, whatever order it is shown in.

#### Scenario: Columns are named and formatted from the declaration
- **WHEN** a table's data is presented
- **THEN** each column SHALL carry its declared name, and each value SHALL be rendered as its
  declared type and format prescribe

#### Scenario: Records appear in file order by default
- **WHEN** a table's data is presented and no filter or sort has been asked for
- **THEN** records SHALL appear in the order the file delivers them

#### Scenario: Each record carries the number the file gives it
- **WHEN** a table's data is presented, in file order or in any other view
- **THEN** each record SHALL show its record number as the file counts records, told apart from
  its position in the view, and that number SHALL be the one findings cite, so that a record can
  be cited whatever order it is shown in

#### Scenario: A view never alters a value
- **WHEN** a table's records are filtered, sorted or totalled
- **THEN** every value shown SHALL be the value as the file stores it, with only a declared value
  redefinition applied, because a value interpreted in order to filter or compute with it is not
  the evidence

#### Scenario: The export is never modified
- **WHEN** any part of the export is read
- **THEN** the system SHALL NOT write to the export, because it is a data carrier

### Requirement: A foreign key carries the reader to the records it refers to
Following a reference by hand — reading a key, opening another table, searching for it — is the
work the declaration already describes. The system SHALL let a person follow a declared foreign
key from the record in front of them to the records it refers to, SHALL make the values that can
be followed recognisable, and SHALL keep what it says about a navigation with the table that
navigation concerns.

#### Scenario: Following a reference to the record it names
- **WHEN** a person acts on a value participating in a foreign key
- **THEN** the system SHALL present the referenced table positioned at the record that key
  identifies, with that record indicated

#### Scenario: A key spanning several columns is followed as one key
- **WHEN** a foreign key names more than one column
- **THEN** acting on any of its values SHALL follow the whole key, and the columns forming it
  SHALL be indicated together

#### Scenario: A value that can be followed is recognisable
- **WHEN** a table's data is presented
- **THEN** every value taking part in a foreign key SHALL be presented so that it can be told
  apart from values that cannot be followed, and the table it leads to SHALL be discoverable
  without acting on it

#### Scenario: A value whose reference does not resolve is marked
- **WHEN** a presented foreign key value matches no record of the referenced table
- **THEN** it SHALL be marked as such where it appears, whether or not it was among the findings
  reported, and in whatever order the view presents it, because the report is bounded and the
  table is not

#### Scenario: Following a reference backwards
- **WHEN** a person asks which records refer to the record in front of them
- **THEN** the system SHALL present the referring table positioned at the first such record, and
  SHALL allow moving between the referring records while stating how many there are

#### Scenario: Referring records are reached without hiding others
- **WHEN** referring records are navigated
- **THEN** the records between them SHALL remain present, because stepping through referring
  records positions the view rather than filtering it

#### Scenario: Following a reference into a view that hides its record
- **WHEN** a navigation, whether following a reference or stepping between referring records,
  leads to a record that the filter of the target table's view hides
- **THEN** the system SHALL remove that filter, keep the view's sort, present the table positioned
  at the record, and say with that table for a few seconds that the filter was removed, because
  a navigation that lands anywhere but the record it names is wrong

#### Scenario: A view that shows the record is kept
- **WHEN** a navigation leads to a record the target table's view shows
- **THEN** the view's filters and sort SHALL be kept, and the table SHALL be positioned at the
  record within that view

#### Scenario: Moving between referring records belongs to that walk
- **WHEN** a person is not stepping through the records that refer to one record
- **THEN** no controls for stepping between referring records SHALL be presented, and stepping
  SHALL end when the person leaves the table being stepped through

#### Scenario: What is said about a navigation stays with its table
- **WHEN** navigation reports an outcome, such as the record reached or a value that refers to
  nothing
- **THEN** that report SHALL be presented with the table it concerns, and SHALL NOT remain in view
  once another table is shown

#### Scenario: A value that refers to nothing
- **WHEN** a foreign key value matches no record in the referenced table
- **THEN** the system SHALL say so, and SHALL NOT present an empty result as though the
  reference resolved

#### Scenario: A reference into a table that could not be read
- **WHEN** the referenced table does not conform to its declaration and so has no data to show
- **THEN** the system SHALL state that the referenced table could not be read, rather than
  offering navigation that cannot complete

### Requirement: A table occupies one place in the workspace
A person following references repeatedly must not accumulate duplicate views of the same table,
and must be able to return to a table they have just left. The system SHALL present each opened
table in a view of its own, at most one per table, SHALL reuse it when that table is reached
again, and SHALL keep the other open views as they were.

#### Scenario: Opening a table gives it a view of its own
- **WHEN** a table is chosen, or reached by following a reference
- **THEN** it SHALL be presented in a view of its own alongside the views already open, and that
  view SHALL come to the front

#### Scenario: Reaching a table already open
- **WHEN** navigation leads to a table that is already open
- **THEN** its existing view SHALL be reused and repositioned, rather than a second view of the
  same table being created

#### Scenario: Returning to a table left behind
- **WHEN** a person returns to a view they left by following a reference
- **THEN** it SHALL be where they left it, positioned at the same record

#### Scenario: Closing a view
- **WHEN** a person closes a table's view
- **THEN** the other views SHALL remain as they were, and the table SHALL still be openable again

### Requirement: An export is opened from within the reader
A person who has been handed a data carrier does not know where the application expects its
argument, and an auditor's medium arrives as a ZIP archive or as an unpacked folder without them
having chosen which. The system SHALL let a person open an export from within the application,
in both the forms the standard permits.

#### Scenario: Choosing a packaged export
- **WHEN** a person asks to open an export and chooses a ZIP archive
- **THEN** the system SHALL open it and present its declared structure

#### Scenario: Choosing an unpacked export
- **WHEN** a person asks to open an export and chooses a folder
- **THEN** the system SHALL open it and present its declared structure, because an export is a
  folder as legitimately as it is an archive

#### Scenario: Choosing nothing
- **WHEN** a person dismisses the choice without choosing anything
- **THEN** whatever was open SHALL remain open and unchanged

#### Scenario: Choosing something that is not an export
- **WHEN** the chosen file or folder cannot be opened as an export
- **THEN** the system SHALL say why, in the same terms the validator would, rather than
  presenting an empty reader

#### Scenario: Opening a second export
- **WHEN** an export is opened while another is already open
- **THEN** the previous export SHALL be closed and everything it held released, so that reading
  a succession of media does not accumulate them

### Requirement: The export is read and checked in full when it is opened
Deciding what can be shown means reading the data. Reading a table only when it is first chosen
makes a person wait at the moment they want to read, and it hides the export's condition until
every table has been opened. The system SHALL read and check every table of an export when the
export is opened, and SHALL stay responsive while it does.

#### Scenario: Every table is checked on opening
- **WHEN** an export is opened
- **THEN** every declared table SHALL be read and checked, including its keys and the references
  into and out of it, without any table having been chosen first

#### Scenario: The reader stays responsive
- **WHEN** an export is being read
- **THEN** the reader SHALL continue to respond to input, and SHALL show how far reading has
  progressed

#### Scenario: Progress is measured, not guessed
- **WHEN** progress is shown
- **THEN** it SHALL reflect how much of the export's data has been read, for the table being
  read and for the export as a whole

#### Scenario: A table can be opened as soon as it is ready
- **WHEN** one table has been read and checked while others are still being read
- **THEN** that table SHALL be openable without waiting for the rest

#### Scenario: A table that is not ready yet
- **WHEN** a person chooses a table that has not been read yet
- **THEN** the system SHALL say that the table is still being read and present it once it is,
  rather than blocking the reader until then

#### Scenario: Opening can be abandoned
- **WHEN** a person closes the reader, or opens another export, while an export is being read
- **THEN** reading SHALL stop, and everything written for that export SHALL be removed

### Requirement: The outcome of opening is summarised before any table is chosen
Someone handed a medium first needs to know what condition it is in, and only then which table
to read. The system SHALL present the outcome of reading the export before any table is chosen:
whether it conforms, the state of each table, and what was found, grouped by the table it
concerns.

#### Scenario: The export's condition is summarised
- **WHEN** an export has been read
- **THEN** the reader SHALL present its verdict, each table with either its record count or its
  number of findings, and the findings grouped by table

#### Scenario: The summary uses the validator's terms
- **WHEN** a finding is presented in the summary
- **THEN** it SHALL carry the same code and message the validator reports when it checks the same
  export's contents, so that the two cannot be read as disagreeing

#### Scenario: The summary shows tables that are not finished
- **WHEN** some tables are still being read
- **THEN** the summary SHALL show which tables are finished, which is being read and which are
  waiting, and SHALL complete itself as they finish

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

### Requirement: The reader shows its icon wherever an application's icon is shown
Someone with several windows open, or looking for the reader among their applications and
downloads, recognises it by its picture before its name, and a generic icon gives them nothing to
recognise. The system SHALL show the reader's icon wherever the platform shows an application's
icon, and SHALL keep that icon recognisable on a light and on a dark background.

#### Scenario: The Windows file shows the icon before it is started
- **WHEN** a person looks at the downloaded Windows executable in the file manager or on the
  desktop
- **THEN** it SHALL show the reader's icon

#### Scenario: The running window shows the icon
- **WHEN** the reader runs on Windows, or on a Linux desktop that shows the icons of windows
- **THEN** its window SHALL show the reader's icon in the title bar and in the taskbar or window
  switcher

#### Scenario: The macOS application shows the icon
- **WHEN** a person sees the macOS application in the disk image, in Finder or in the Dock
- **THEN** it SHALL show the reader's icon, and macOS SHALL show that icon as delivered rather than
  inside a frame of its own

#### Scenario: The icon is recognisable on a dark background
- **WHEN** the icon is shown on a dark background, such as a dark taskbar or a dark Dock
- **THEN** the whole drawing SHALL remain visible, including its dark parts

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
