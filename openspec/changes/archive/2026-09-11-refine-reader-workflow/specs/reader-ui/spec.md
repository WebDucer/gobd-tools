## ADDED Requirements

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

## MODIFIED Requirements

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
  reported, because the report is bounded and the table is not

#### Scenario: Following a reference backwards
- **WHEN** a person asks which records refer to the record in front of them
- **THEN** the system SHALL present the referring table positioned at the first such record, and
  SHALL allow moving between the referring records while stating how many there are

#### Scenario: Referring records are reached without hiding others
- **WHEN** referring records are navigated
- **THEN** the records between them SHALL remain present, because record order is what makes a
  record citable

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
