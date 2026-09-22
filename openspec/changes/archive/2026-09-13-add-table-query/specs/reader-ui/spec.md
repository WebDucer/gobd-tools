## ADDED Requirements

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

## MODIFIED Requirements

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

## REMOVED Requirements

### Requirement: Data is presented as declared, and only presented
**Reason**: Its "No query surface" scenario kept the first version simple. What it protected, a
record that stays citable, now rests on the record number every record carries in any view
rather than on file order being the only order.
**Migration**: Replaced by "Data is presented as declared, and never altered" in this spec, and by
the `table-query` capability. Column naming, file order as the default and the export never being
written carry over unchanged. "Record order is preserved" becomes "Records appear in file order
by default" together with "Each record carries the number the file gives it".
