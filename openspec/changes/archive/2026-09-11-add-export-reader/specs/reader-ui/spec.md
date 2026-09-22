## Purpose

Defines how a person navigates and reads a GoBD export: the declared structure they move
through, the conditions under which a table's data is shown at all, and how a foreign key
carries them from one table to another.

## ADDED Requirements

### Requirement: The export is navigated by its declared structure
The relationships between tables form a directed graph — a table may reference several others,
several may reference it, references may cross media, and a table may reference itself — so no
single tree can present them without either duplicating tables or omitting them. The system
SHALL therefore navigate by the structure `index.xml` actually declares, which is a tree.

#### Scenario: The navigator presents the declared hierarchy
- **WHEN** an export is opened
- **THEN** the system SHALL present its media and, beneath each, the tables that medium carries

#### Scenario: References are presented per table, not as hierarchy
- **WHEN** a table is selected
- **THEN** the system SHALL present the tables it references and the tables that reference it,
  as relationships of that table rather than as parents or children in the navigator

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

### Requirement: Data is presented as declared, and only presented
The value of reading an export here rather than in a spreadsheet is that the declaration is
applied. The system SHALL render each column under the name and type the export declares, in
the order the file delivers its records, and SHALL NOT offer a query surface over it.

#### Scenario: Columns are named and formatted from the declaration
- **WHEN** a table's data is presented
- **THEN** each column SHALL carry its declared name, and each value SHALL be rendered as its
  declared type and format prescribe

#### Scenario: Record order is preserved
- **WHEN** a table's data is presented
- **THEN** records SHALL appear in the order the file delivers them, and each SHALL be
  identifiable by its position, so that a record can be cited

#### Scenario: No query surface
- **WHEN** a person views a table
- **THEN** the system SHALL NOT offer sorting, filtering, grouping or aggregation of that table

#### Scenario: The export is never modified
- **WHEN** any part of the export is read
- **THEN** the system SHALL NOT write to the export, because it is a data carrier

### Requirement: A foreign key carries the reader to the records it refers to
Following a reference by hand — reading a key, opening another table, searching for it — is the
work the declaration already describes. The system SHALL let a person follow a declared foreign
key from the record in front of them to the records it refers to.

#### Scenario: Following a reference to the record it names
- **WHEN** a person acts on a value participating in a foreign key
- **THEN** the system SHALL present the referenced table positioned at the record that key
  identifies, with that record indicated

#### Scenario: A key spanning several columns is followed as one key
- **WHEN** a foreign key names more than one column
- **THEN** acting on any of its values SHALL follow the whole key, and the columns forming it
  SHALL be indicated together

#### Scenario: Following a reference backwards
- **WHEN** a person asks which records refer to the record in front of them
- **THEN** the system SHALL present the referring table positioned at the first such record, and
  SHALL allow moving between the referring records while stating how many there are

#### Scenario: Referring records are reached without hiding others
- **WHEN** referring records are navigated
- **THEN** the records between them SHALL remain present, because record order is what makes a
  record citable

#### Scenario: A value that refers to nothing
- **WHEN** a foreign key value matches no record in the referenced table
- **THEN** the system SHALL say so, and SHALL NOT present an empty result as though the
  reference resolved

#### Scenario: A reference into a table that could not be read
- **WHEN** the referenced table does not conform to its declaration and so has no data to show
- **THEN** the system SHALL state that the referenced table could not be read, rather than
  offering navigation that cannot complete

### Requirement: A table occupies one place in the workspace
A person following references repeatedly must not accumulate duplicate views of the same table.
The system SHALL present at most one view per table and SHALL reuse it when that table is
reached again.

#### Scenario: Reaching a table already open
- **WHEN** navigation leads to a table that is already open
- **THEN** its existing view SHALL be reused and repositioned, rather than a second view of the
  same table being created

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

### Requirement: The reader is distributed as a runnable build per platform
A reader that has to be built from source is a reader an auditor does not have. The system SHALL
be distributable as a build that runs on a supported platform without anything being installed
alongside it.

#### Scenario: Running on a machine without .NET installed
- **WHEN** the published build is run on a supported platform with no .NET runtime present
- **THEN** it SHALL start and open an export

#### Scenario: The store engine travels with the build
- **WHEN** the published build is run on a machine that has never had a database engine installed
- **THEN** importing an export SHALL still work, because the build carries the store it needs

#### Scenario: Every supported platform is built
- **WHEN** the project's pipeline builds a revision
- **THEN** it SHALL produce a downloadable reader build for each platform the CLI is built for
