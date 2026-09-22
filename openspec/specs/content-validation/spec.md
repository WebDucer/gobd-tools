# content-validation

## Purpose

Defines what the system detects when it reads a table's data file: whether each record conforms
to the layout the export declares for it, whether declared keys hold, and how much of that it
promises to report before it stops.

## Requirements

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

### Requirement: A header row is checked against the declaration
When a file carries column names, they are the one place the file's own structure can be
compared against what `index.xml` claims. The system SHALL compare a header row against the
declared columns whenever one is present, because a header whose order differs from the
declaration means every value is attributed to the wrong column, and nothing else detects it.

#### Scenario: Header matches the declaration
- **WHEN** the declaration excludes the first record and that record carries exactly the
  declared column names in the declared order
- **THEN** no defect SHALL be reported

#### Scenario: Header carries the declared names in a different order
- **WHEN** the excluded first record carries the declared column names but not in the declared
  order
- **THEN** the system SHALL report an error, because the file's columns and the declaration
  disagree about which value belongs to which column

#### Scenario: The excluded record is not a header
- **WHEN** the excluded first record bears no resemblance to the declared column names
- **THEN** the system SHALL NOT report a header defect, because a declaration may legitimately
  exclude a preamble

#### Scenario: An undeclared header row
- **WHEN** the declaration excludes no record, and the first record carries the declared column
  names
- **THEN** the system SHALL report an error, because that record will otherwise be read as data

### Requirement: Records that do not conform to the declaration are reported
The system SHALL report each way in which a record fails the layout declared for its table, so
that a producer learns what to correct rather than only that something is wrong.

#### Scenario: A value does not match its declared type or format
- **WHEN** a value in a `Date` column does not match that column's declared format, or a value
  in a `Numeric` column is not a number under the table's declared symbols
- **THEN** the system SHALL report an error naming the table, the record, the column and the
  offending value

#### Scenario: A value exceeds what its column declares
- **WHEN** a value carries more decimal places than the column's declared accuracy, or is
  longer than the column's declared maximum length
- **THEN** the system SHALL report the defect against that record and column

#### Scenario: A record has the wrong number of columns
- **WHEN** a record yields more or fewer columns than the table declares
- **THEN** the system SHALL report an error naming the record and both counts

#### Scenario: A finding does not carry the rest of the record
- **WHEN** any content finding names an offending value
- **THEN** it SHALL carry that value alone and SHALL NOT carry the remainder of the record,
  because a report is shared onward and a record of a GoBD export is a person's data

#### Scenario: A record cannot be decoded or delimited
- **WHEN** bytes cannot be decoded in the declared codepage, or a text encapsulator is never
  closed, or a fixed-length record's length differs from the declared length
- **THEN** the system SHALL report an error naming the record

### Requirement: What a table reports is bounded and reproducible
A table whose every record is defective would otherwise produce a finding per record and a scan
of the whole file for no added information. The system SHALL stop analysing a table once a
bounded number of findings has been reached, and SHALL report which findings those are in terms
of record order rather than the order in which they were detected, so that two runs of the same
export report the same findings.

#### Scenario: A table exceeds the bound
- **WHEN** analysing a table reaches the configured maximum number of findings
- **THEN** analysis of that table SHALL stop, the report SHALL carry those findings, and it
  SHALL state that analysis stopped rather than implying the table holds no further defects

#### Scenario: Two runs agree
- **WHEN** the same defective table is analysed twice, by the same or a different engine
- **THEN** the reported findings SHALL be the same findings — the first by record order — and
  SHALL NOT depend on how the reading was scheduled or parallelised

#### Scenario: A table within the bound
- **WHEN** a table's defects number fewer than the bound
- **THEN** every defect SHALL be reported and the table SHALL be read to its end

### Requirement: Declared primary keys are unique
The standard's `References` resolves to a table's primary key, so a duplicate key makes every
reference to that table ambiguous. The system SHALL report primary keys that occur more than
once in a table.

#### Scenario: A duplicate primary key
- **WHEN** two records of a table carry the same declared primary key value
- **THEN** the system SHALL report an error naming the table, the key value and the records
  carrying it

#### Scenario: An empty primary key
- **WHEN** a record's declared primary key is empty, or a composite key is only partly present
- **THEN** the system SHALL report an error naming the table and the record

#### Scenario: A table with unique keys
- **WHEN** every record of a table carries a distinct primary key
- **THEN** no key defect SHALL be reported for that table

### Requirement: Every foreign key value resolves
A dangling reference is the defect that most often makes an export unusable to the auditor
importing it. The system SHALL check that each foreign key value matches a primary key of the
referenced table, using the column mapping the declaration establishes.

#### Scenario: A value with no matching record
- **WHEN** a record's foreign key value matches no primary key in the referenced table
- **THEN** the system SHALL report an error naming the referring table, the record, the value
  and the referenced table

#### Scenario: A composite foreign key
- **WHEN** a foreign key names more than one column
- **THEN** the whole tuple SHALL be matched against the referenced table's primary key, mapped
  column by column as the declaration establishes, rather than each column independently

#### Scenario: The referenced table could not be read
- **WHEN** the table a foreign key references could not itself be read
- **THEN** the system SHALL report that the reference could not be checked, and SHALL NOT report
  its values as unresolved

### Requirement: The limits of a run are stated, never silently exceeded
Checking keys requires remembering every key of a table, so an engine has a size beyond which it
cannot answer. The system SHALL state when a table is too large for the engine performing the
check rather than exhausting memory or reporting an unchecked table as clean.

#### Scenario: A table beyond the engine's capacity
- **WHEN** a table carries more records than the running engine can check keys for
- **THEN** the system SHALL report that the check was not performed for that table, and SHALL
  NOT report the table as having passed

#### Scenario: The verdict reflects what was checked
- **WHEN** any check was skipped because of a limit
- **THEN** the overall result SHALL NOT be reported as conformant on the strength of checks that
  did not run
