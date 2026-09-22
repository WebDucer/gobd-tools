## MODIFIED Requirements

### Requirement: A data file is read as its declaration defines it
A GoBD data file carries no self-describing structure — frequently not even a header row — so
`index.xml` is the only authority on how to read it. The system SHALL interpret a table's file
using the record layout declared for that table, and SHALL NOT infer structure from the file's
own content.

#### Scenario: The declared record layout is applied
- **WHEN** a table is read
- **THEN** the system SHALL split records and columns using the declared delimiters, text
  encapsulator and, for a fixed-length table, the declared column spans; and SHALL decode bytes
  using the declared codepage

#### Scenario: Column identity comes from the declaration
- **WHEN** a table's file carries no header row
- **THEN** each column SHALL still be named and typed, because the declaration names and types
  it; the file's content SHALL NOT be consulted for either

#### Scenario: Values are interpreted as their declared type
- **WHEN** a value is read from a column declared `Numeric`, `Date` or `AlphaNumeric`
- **THEN** it SHALL be interpreted using that column's declared format, and the table's declared
  decimal symbol, digit grouping symbol and two-digit-year window

#### Scenario: A number's sign stands before or after its digits
- **WHEN** a value in a `Numeric` column carries one sign, `-` or `+`, directly before its first
  digit or directly after its last
- **THEN** it SHALL be read as a number of that sign, so that `-1782,90` and `1782,90-` denote the
  same number, because the standard permits a negative figure to carry its sign on either side

#### Scenario: A sign is not a digit
- **WHEN** a `Numeric` value carries a sign, before or after its digits
- **THEN** its decimal places SHALL be counted without the sign, so that the column's declared
  accuracy is tested exactly as it would be for the same value unsigned

#### Scenario: A sign that is not part of the number
- **WHEN** a `Numeric` value has a space between its sign and its digits, carries a sign at both
  ends, or carries more than one sign at one end
- **THEN** it SHALL NOT be read as a number, and SHALL be reported as a value that does not
  match its declared type

#### Scenario: Padding around a signed number
- **WHEN** a signed `Numeric` value is padded, as a fixed-length column pads a value to its span
- **THEN** the padding SHALL be disregarded as it is for an unsigned value, and the sign SHALL
  still be read

#### Scenario: A declared value redefinition is applied
- **WHEN** a column declares a `Map` from one value to another
- **THEN** a matching value SHALL be presented as the mapped value, and the value as stored
  SHALL remain retrievable

#### Scenario: Records excluded by the declaration are not read as data
- **WHEN** a table declares a record range, or a count of bytes to skip
- **THEN** the excluded records SHALL NOT be reported as data records

#### Scenario: A table with no data records
- **WHEN** a table's file holds no data records at all, whether because it is empty or because
  the declaration excludes everything in it
- **THEN** the table SHALL be read as a table of no records, and no defect SHALL be reported,
  because a period with nothing in it is an ordinary thing for an export to describe
