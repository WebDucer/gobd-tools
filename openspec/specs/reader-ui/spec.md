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
concerns, presented in a structured dashboard layout.

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

#### Scenario: The summary is structured into dashboard sections
- **WHEN** an export is being read or has finished reading
- **THEN** the summary SHALL present verdict, reading progress, table status, and findings
  in visually separated dashboard cards rather than a single uninterrupted list of plain text

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

### Requirement: The reader adapts to system theme variant
A reader must stay legible wherever it is used, and some people cannot read it at ordinary
contrast at all. The system SHALL offer System, Light, Dark, High contrast (dark) and High contrast
(light) appearances. By default it SHALL follow the platform's light or dark preference and its high
contrast setting. Every text, link, status marking and focus indicator SHALL meet WCAG AA contrast in
every variant.

#### Scenario: Automatic theme matching
- **WHEN** the reader runs with the System appearance setting on an operating system configured for
  dark mode or light mode, without high contrast
- **THEN** the reader SHALL present its window, navigator, tabs, grids and dialogs using that theme
  variant

#### Scenario: System follows the platform's high contrast setting
- **WHEN** the reader runs with the System appearance setting and the operating system reports that
  high contrast is on
- **THEN** the reader SHALL present itself in high contrast, dark or light as the operating system
  is, and SHALL change again when that setting changes, without being restarted

#### Scenario: A platform that does not report high contrast
- **WHEN** the reader runs on a platform that does not report a high contrast setting
- **THEN** System SHALL follow the light or dark preference alone, and both high contrast
  appearances SHALL remain available to choose explicitly

#### Scenario: Explicit theme override
- **WHEN** a person selects Light, Dark, High contrast (dark) or High contrast (light) in Settings
- **THEN** the reader SHALL immediately apply that appearance regardless of the operating system's
  setting

#### Scenario: Theme legibility for findings and links
- **WHEN** the reader displays conformant status, non-conformant findings, followable foreign keys,
  values that refer to nothing, or secondary text, in any appearance
- **THEN** that text SHALL contrast with its background by at least 4.5:1, or 3:1 where it is large
  text

#### Scenario: Values in the selected record stay legible
- **WHEN** a record is selected and its row is drawn in the selection colour
- **THEN** its followable values and its values that refer to nothing SHALL still meet the same
  contrast against the selection colour, in every appearance

#### Scenario: Status is not told by colour alone
- **WHEN** the reader marks a value that can be followed, a value that refers to nothing, or the
  export's verdict
- **THEN** the marking SHALL also be told apart without colour, by underline, strikethrough or
  wording, so that it survives high contrast and colour blindness

### Requirement: User can configure and persist preferences
Preferences belong to the person running the application and must not be lost when the application
closes. The system SHALL provide a Settings window accessible from the application menus and keyboard
shortcuts, SHALL allow selecting the appearance theme, display language and zoom level, and SHALL
persist these choices together with the navigator's width and whether it is collapsed, in the user's
platform configuration directory, without writing to the export.

#### Scenario: Opening settings from the menu or shortcut
- **WHEN** a person chooses Settings from the menu or presses the standard shortcut (Cmd+, on macOS, Ctrl+, on other platforms)
- **THEN** the system SHALL present the Settings window, or bring it forward if already open

#### Scenario: Preferences persist across launches
- **WHEN** a person changes the theme, language or zoom level, resizes the navigator or collapses
  it, and subsequently restarts the reader
- **THEN** the reader SHALL start with the previously chosen theme, language, zoom level, navigator
  width and navigator state

#### Scenario: Absent or unreadable configuration falls back safely
- **WHEN** no preferences file exists or the file is unreadable
- **THEN** the reader SHALL start with the System theme, the operating system's detected language,
  actual size and an expanded navigator of its default width, without reporting an error

#### Scenario: A setting this reader does not know
- **WHEN** the preferences file holds a value this version of the reader does not know for one
  setting, such as an appearance added by a later version
- **THEN** that setting SHALL take its default and every other setting in the file SHALL be kept

### Requirement: The reader presents its interface and findings in the selected language
Reading and auditing records should happen in the language the person understands. The system SHALL
support English and German display languages, and SHALL present its menus, tabs, status messages,
actions, and validation finding descriptions in the chosen language.

#### Scenario: Selecting German
- **WHEN** a person chooses German as the display language
- **THEN** the system SHALL present its menus, tab headers, filter and sort labels, dashboard sections,
  and finding descriptions in German

#### Scenario: Selecting English
- **WHEN** a person chooses English as the display language
- **THEN** the system SHALL present its menus, tab headers, filter and sort labels, dashboard sections,
  and finding descriptions in English

#### Scenario: Switching language updates the view immediately
- **WHEN** the display language is changed in Settings
- **THEN** the open workspace, tabs, summary, and menus SHALL update their displayed text immediately
  without requiring an application restart

### Requirement: Interactive controls provide baseline accessibility
Someone using assistive technology knows a control only by the name it exposes, and someone using
the keyboard knows where they are only by the focus they can see. The system SHALL give every
interactive control and every input an accessible name in the display language, and SHALL show a
clearly visible focus indicator on whichever control has keyboard focus.

#### Scenario: Accessible names for controls
- **WHEN** assistive technology inspects interactive elements such as table tabs, close buttons,
  filter and sort chips, action buttons, the inputs and pickers of the filter, sort and figure
  editors, the navigator's splitter and its show control, or the reading progress
- **THEN** each element SHALL expose an accessible name describing its action or content

#### Scenario: Names follow the display language
- **WHEN** the display language is changed
- **THEN** every accessible name SHALL be given in the new language

#### Scenario: Visible keyboard focus
- **WHEN** a person navigates interactive controls using keyboard input
- **THEN** the currently focused control SHALL display a clear, distinct visual focus indicator,
  including a button that shows only a symbol, such as a tab's or a chip's close button

#### Scenario: Focus indicators are visible in every appearance
- **WHEN** a focus indicator is drawn in any appearance
- **THEN** it SHALL contrast with its surroundings by at least 3:1

### Requirement: The interface can be enlarged
A person who needs larger text needs all of it larger, including the navigator, and must not lose
anything by enlarging it. The system SHALL let a person zoom the whole interface, including its
dialogs, its editors and the record actions, from actual size to twice that size, and SHALL keep
every control and every value reachable at any zoom level.

#### Scenario: Zooming from the keyboard
- **WHEN** a person presses the command key with plus, with minus, or with zero (the command key
  being Cmd on macOS and Ctrl elsewhere)
- **THEN** the reader SHALL zoom in by one step, zoom out by one step, or return to actual size,
  within the range from 100 % to 200 %

#### Scenario: Zooming from Settings and the menu
- **WHEN** a person chooses a zoom level in Settings, or Zoom In, Zoom Out or Actual Size in the
  View menu
- **THEN** the reader SHALL apply it at once

#### Scenario: Everything is enlarged together
- **WHEN** a zoom level other than actual size is in force
- **THEN** the navigator, the tabs, the table toolbar, the records, the in-window menu bar, the
  Settings, About and text windows, the filter, sort and figure editors, and the record actions
  SHALL all be presented at that zoom level

#### Scenario: Nothing is lost at the largest zoom
- **WHEN** the interface is zoomed to 200 %
- **THEN** every control and every value SHALL remain reachable, by scrolling where it does not fit

### Requirement: The navigator can be resized and collapsed
Long table names and large text need a wider navigator, while a wide table at a large zoom level
needs all the room it can get. The system SHALL let a person change the navigator's width and
collapse it to a narrow strip, with the mouse and with the keyboard, and SHALL NOT do either on its
own.

#### Scenario: Resizing the navigator
- **WHEN** a person drags the boundary between the navigator and the tables, or focuses that
  boundary and presses the left or right arrow key
- **THEN** the navigator SHALL become narrower or wider, within a minimum width and a maximum of half
  the window

#### Scenario: Collapsing and restoring the navigator
- **WHEN** a person chooses Show Navigator in the View menu, presses Ctrl+B (Ctrl+Cmd+S on macOS), or
  activates the control on the collapsed strip
- **THEN** the navigator SHALL collapse to a narrow strip that still offers a control to restore it,
  or be restored at the width it had

#### Scenario: Collapsing the navigator while it has focus
- **WHEN** the navigator is collapsed while keyboard focus is inside it
- **THEN** focus SHALL move to the tab in front, rather than be lost

#### Scenario: A collapsed navigator stays current
- **WHEN** the table in view changes while the navigator is collapsed, and the navigator is then
  restored
- **THEN** it SHALL indicate the table in view

#### Scenario: Zooming does not collapse the navigator
- **WHEN** a person changes the zoom level
- **THEN** the navigator SHALL keep its state, so that only the person decides whether it is shown

### Requirement: Moving through the navigator does not open tables
Moving through a list from the keyboard is how a person reads it, and every move cannot also be
taken as a choice. The system SHALL let the arrow keys move through the navigator without opening
anything, and SHALL open a table only when the person asks for it.

#### Scenario: Arrow keys only move
- **WHEN** a person moves through the navigator's entries with the arrow keys
- **THEN** no table SHALL be opened and the tab in front SHALL NOT change

#### Scenario: Opening a table from the keyboard
- **WHEN** a person presses Enter on a table's entry in the navigator
- **THEN** the system SHALL open that table, bring its tab to the front and move focus to its
  records

#### Scenario: Opening a table with the mouse
- **WHEN** a person clicks a table's entry in the navigator
- **THEN** the system SHALL open that table and bring its tab to the front

### Requirement: Every command can be reached from the keyboard
A reader that needs a mouse is closed to people who cannot use one, and slow for people who would
rather not. The system SHALL offer every command in a menu and from the keyboard. Commands used
often SHALL have shortcuts that follow each platform's conventions. A shortcut SHALL mean one
thing in any one place.

#### Scenario: Shortcuts for the workspace
- **WHEN** a person presses one of these shortcuts, where the command key is Cmd on macOS and Ctrl
  elsewhere: command+O, command+Shift+O, command+comma, command+W (also Ctrl+F4 on Windows and
  Linux), Ctrl+Tab, Ctrl+Shift+Tab (also Cmd+Shift+] and Cmd+Shift+[ on macOS), command+1 to
  command+9, F6, Shift+F6, or F1
- **THEN** the reader SHALL in that order open an archive, open a folder, show Settings, close the
  table tab in front, bring the next or previous tab to the front, bring the tab in that place to the
  front with the summary as the first, move focus to the next or previous area, or show the shortcut
  overview

#### Scenario: Shortcuts for the table in front
- **WHEN** a table is in front and a person presses command+F, F3 or Shift+F3 (Cmd+G or Cmd+Shift+G
  on macOS), Alt+Left or Alt+Right (Cmd+[ or Cmd+] on macOS), or Ctrl+G (Cmd+L on macOS)
- **THEN** the reader SHALL in that order open the filter editor, move to the next or previous
  referring record while referring records are being stepped through, go back or forward, or ask for
  a record number to go to

#### Scenario: Closing a tab from the keyboard
- **WHEN** a person presses the shortcut for closing a tab
- **THEN** the table tab in front SHALL close and focus SHALL move into the tab that comes to the
  front; with the summary in front nothing SHALL close, and the window SHALL stay open

#### Scenario: Next and previous tab wrap around
- **WHEN** a person moves to the next tab from the last one, or to the previous tab from the summary
- **THEN** the summary, or the last tab, SHALL come to the front

#### Scenario: Moving between areas
- **WHEN** a person presses F6 or Shift+F6
- **THEN** focus SHALL move to the next or previous of the navigator, the table's toolbar and the
  table's records (or the summary), skipping the navigator while it is collapsed

#### Scenario: Shortcuts are shown where commands are found
- **WHEN** a person opens a menu, or the shortcut overview
- **THEN** each command SHALL show its shortcut for the platform the reader runs on, and the overview
  SHALL list every shortcut in the display language

#### Scenario: Menus can be opened from the keyboard
- **WHEN** the reader runs on Windows or Linux and a person presses Alt with the letter a menu's
  title marks
- **THEN** that menu SHALL open, and the marked letters SHALL follow the display language

#### Scenario: A text field keeps its own keys
- **WHEN** keyboard focus is in a text field and a person presses a key that the field uses itself,
  such as copy, Enter or the arrow keys
- **THEN** the key SHALL act on the field and SHALL NOT run a command of the reader

#### Scenario: Editors and dialogs are operated from the keyboard
- **WHEN** a filter, sort or figure editor, or a dialog, is opened
- **THEN** focus SHALL move into it, Enter SHALL apply an editor, Escape SHALL close it and return
  focus to where it was opened from, and Cmd+W SHALL close a dialog on macOS

### Requirement: A record's references can be followed from the keyboard
Following a reference is the reader's central act, and it must not depend on aiming a pointer at a
cell. The system SHALL offer, for the record that has focus, every reference it can follow and
every table that refers to it, from the keyboard and from the context menu alike.

#### Scenario: Opening the record's actions
- **WHEN** a record has focus and a person presses Enter, Shift+F10 or the Menu key, or right-clicks
  a record
- **THEN** the system SHALL present the record's actions next to that record: each foreign key it
  can follow, each table whose records refer to it, and copying the record

#### Scenario: Where a value leads is said before it is followed
- **WHEN** the record's actions are presented
- **THEN** each foreign key SHALL be named by its columns and by the table it leads to, and one whose
  value refers to nothing SHALL say so, before anything has been followed

#### Scenario: Following from the record's actions
- **WHEN** a person chooses a foreign key or a referring table among the record's actions
- **THEN** the system SHALL navigate exactly as it does when the reference is followed with the
  mouse, or the referring records are asked for

#### Scenario: Clicking a value still follows it
- **WHEN** a person clicks a value that takes part in a foreign key
- **THEN** the system SHALL follow that key directly, as before

### Requirement: Focus follows navigation
Someone who follows a reference from the keyboard has to arrive where the navigation arrived, or
they are left reading something else. The system SHALL move keyboard focus to the record a
navigation reaches.

#### Scenario: Arriving at a record
- **WHEN** a navigation positions a table at a record, whether by following a reference, stepping
  between referring records, going back or forward, or going to a record number
- **THEN** that record SHALL have keyboard focus

#### Scenario: Returning to a tab
- **WHEN** a person brings a table's tab to the front from the keyboard
- **THEN** focus SHALL move to the record that table is positioned at

### Requirement: Back and Forward retrace navigations
Following references leads from table to table, and finding the way back by hunting through tabs
undoes the convenience of following them. The system SHALL remember where each navigation started,
and SHALL let a person go back to it and forward again, as a browser does.

#### Scenario: Going back
- **WHEN** a person has followed a reference, asked for the records referring to one, or gone to a
  record number, and then chooses Back
- **THEN** the system SHALL present the table that navigation started from, positioned at the record
  it started from

#### Scenario: Going forward
- **WHEN** a person has gone back and then chooses Forward
- **THEN** the system SHALL present again the table and record that navigation reached

#### Scenario: A new navigation after going back
- **WHEN** a person goes back and then navigates anew
- **THEN** the navigations that Forward would have retraced SHALL be discarded

#### Scenario: What is not a navigation
- **WHEN** a person chooses a table in the navigator, switches tabs, or steps between referring
  records
- **THEN** no new place SHALL be remembered, and Back from a walk through referring records SHALL
  return to the record the walk started from

#### Scenario: Going back to a table whose tab was closed
- **WHEN** Back or Forward leads to a table whose tab has since been closed
- **THEN** the system SHALL open that table again and position it at the remembered record

#### Scenario: Going back into a view that hides the record
- **WHEN** Back or Forward leads to a record that the filter of the target table's view hides
- **THEN** the system SHALL treat it as any navigation into such a view: remove the filter, keep the
  sort, and say so with the table

#### Scenario: Nothing to go back to
- **WHEN** there is no earlier or later navigation
- **THEN** Back or Forward SHALL be unavailable, and the menu SHALL show it so

#### Scenario: A new export starts a new history
- **WHEN** another export is opened
- **THEN** the navigations remembered for the previous one SHALL be forgotten

### Requirement: A record can be reached by its number
Findings, notes and colleagues cite a record by the number the file gives it. The system SHALL let
a person go to a record of the table in front by that number.

#### Scenario: Going to a record
- **WHEN** a person asks to go to a record and enters a record number the table holds
- **THEN** the system SHALL position the table at that record, with that record indicated and
  focused

#### Scenario: A number the table does not hold
- **WHEN** the number entered is not a record number of that table
- **THEN** the system SHALL say so where the number was entered, and SHALL leave the table where it
  was

#### Scenario: A record hidden by the view
- **WHEN** the record is hidden by the filter of the table's view
- **THEN** the system SHALL remove the filter, keep the sort, and say so with the table, as for any
  navigation

#### Scenario: Only where there are records
- **WHEN** the summary is in front, or the table in front has no data to show
- **THEN** going to a record SHALL be unavailable

### Requirement: A record can be copied
Someone who reads an export has to quote what they read, and retyping a value invites the very
error the reader exists to avoid. The system SHALL let a person copy the record that has focus, as
shown, in a form that pastes into a spreadsheet.

#### Scenario: Copying a record
- **WHEN** a record has focus and a person presses the command key with C, or chooses Copy Record
  among the record's actions
- **THEN** the clipboard SHALL hold two lines, separated by tabs: the record number and the declared
  column names, then the record's number and its values exactly as shown

#### Scenario: Values that would break the lines
- **WHEN** a copied value contains a tab, a line break or a quotation mark
- **THEN** that value SHALL be enclosed in quotation marks with its own quotation marks doubled, so
  that it pastes as one cell

### Requirement: Wide tables can be scrolled from the keyboard
A table may declare fifty columns, and what lies beyond the window's edge is as much part of the
record as what is in view. The system SHALL let a person scroll a table's records sideways from the
keyboard.

#### Scenario: Scrolling sideways
- **WHEN** a table's records have focus and a person presses the left or right arrow key
- **THEN** the records SHALL scroll sideways, leaving the record that has focus unchanged

### Requirement: Records are identified to assistive technology
A row that announces nothing, or the name of a type, tells a screen reader user nothing about the
record. The system SHALL give every presented record an accessible name made of its record number
and its values under their column names. It SHALL NOT be required to expose records as a table
with row and column headers.

#### Scenario: A record is announced
- **WHEN** assistive technology inspects a presented record
- **THEN** its name SHALL state its record number as the file counts records, followed by each
  declared column's name and value as shown

#### Scenario: A value that refers to nothing is announced
- **WHEN** a record holds a foreign key value that matches no record of the referenced table
- **THEN** the record's name SHALL say so at that value, as the presentation marks it

### Requirement: Status messages are announced
What the reader says about its progress and about a navigation is shown where it happened, which is
not where a screen reader is listening. The system SHALL announce its status messages to assistive
technology without moving focus.

#### Scenario: Messages that are announced
- **WHEN** the reader reports reading progress, the outcome of a navigation, that a filter was
  removed, the position while stepping between referring records, that a table is not ready yet, or
  an editor's refusal of what was entered
- **THEN** that message SHALL be announced to assistive technology, and focus SHALL stay where it was

#### Scenario: Progress is not announced at every step
- **WHEN** reading progresses continuously
- **THEN** the progress SHALL be announced at intervals and when a table finishes, rather than at
  every update the display receives

### Requirement: The reader lists its keyboard shortcuts
Shortcuts that cannot be found are shortcuts nobody uses. The system SHALL present every shortcut
it offers, for the platform it runs on and in the display language, from its Help menu and from F1.

#### Scenario: Showing the shortcut overview
- **WHEN** a person presses F1 or chooses Keyboard Shortcuts in the Help menu
- **THEN** the reader SHALL show every command that has a shortcut, with that shortcut as the
  platform writes it, grouped by where it applies

#### Scenario: Closing the shortcut overview
- **WHEN** the shortcut overview has focus and a person presses Escape
- **THEN** it SHALL close and focus SHALL return to where it was
