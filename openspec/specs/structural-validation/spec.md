# structural-validation

## Purpose

Checks the semantic coherence of a validated `index.xml` — keys, references, aliases, ranges
and declared metadata values — using only the description file and the export's entry listing,
so that a producer learns an export is inconsistent without any data file being read.

## Requirements

### Requirement: Foreign key columns must be declared in the same table
A `ForeignKey` never introduces a column. The system SHALL verify that every `ForeignKey/Name`
is already declared as a column or primary key of the table that contains it.

#### Scenario: Foreign key names an undeclared column
- **WHEN** a `ForeignKey/Name` does not match the `Name` of any `VariableColumn`,
  `VariablePrimaryKey`, `FixedColumn` or `FixedPrimaryKey` in the same table
- **THEN** the system SHALL report an error naming the table, the foreign key and the
  undeclared column

#### Scenario: Foreign key names a declared column
- **WHEN** every `ForeignKey/Name` matches a column declared in the same table
- **THEN** the system SHALL report no finding for column declaration

### Requirement: Foreign key references must resolve to exactly one table
A `ForeignKey/References` names a table, identified by its `Name` or, when `Name` is absent, by
its `URL`. The system SHALL verify that each reference resolves to exactly one table in the
`DataSet`.

#### Scenario: Reference names no table
- **WHEN** `References` matches no table identity in the `DataSet`
- **THEN** the system SHALL report an error for the dangling reference

#### Scenario: Reference matches a table identified only by URL
- **WHEN** `References` matches the `URL` of a table that declares no `Name`
- **THEN** the reference SHALL be considered resolved

#### Scenario: Reference matches more than one table
- **WHEN** two or more tables share the identity named by `References`
- **THEN** the system SHALL report an error for the ambiguous reference, listing the candidates

#### Scenario: Reference crosses media boundaries
- **WHEN** `References` resolves to a table declared under a different `Media` element
- **THEN** the reference SHALL be considered resolved, because references are scoped to the
  `DataSet` rather than to a single medium

### Requirement: Referenced tables must declare a primary key
A foreign key joins to the referenced table's primary key. The system SHALL verify that every
referenced table declares at least one primary key.

#### Scenario: Referenced table has no primary key
- **WHEN** a `ForeignKey` resolves to a table declaring only columns and no primary key
- **THEN** the system SHALL report an error stating that the reference cannot be joined

### Requirement: Foreign key arity must match the referenced primary key
The system SHALL verify that the number of `ForeignKey/Name` elements equals the number of
primary key columns declared by the referenced table.

#### Scenario: Arity mismatch
- **WHEN** a `ForeignKey` declares two names but the referenced table declares a single
  primary key column
- **THEN** the system SHALL report an error stating the declared and expected arity

#### Scenario: Composite key arity matches
- **WHEN** a `ForeignKey` declares two names and the referenced table declares two primary key
  columns
- **THEN** the system SHALL report no finding for arity

### Requirement: Foreign key and primary key datatypes must match
The standard states that joining columns of differing datatypes produces undefined results.
The system SHALL verify that each foreign key column's declared datatype matches the datatype
of the primary key column it maps to.

#### Scenario: Datatype mismatch
- **WHEN** a foreign key column is declared `AlphaNumeric` and the primary key column it maps
  to is declared `Numeric`
- **THEN** the system SHALL report an error naming both columns and both datatypes

#### Scenario: Matching datatypes
- **WHEN** each foreign key column's datatype equals that of its mapped primary key column
- **THEN** the system SHALL report no finding for datatypes

### Requirement: Alias elements must map foreign key columns onto primary key columns
Without `Alias`, foreign key columns map to the referenced table's primary key columns
positionally. Each `Alias` overrides that for one column. The system SHALL verify the
consistency of the resulting mapping.

#### Scenario: Alias From is not part of this foreign key
- **WHEN** an `Alias/From` does not match any `ForeignKey/Name` in the same `ForeignKey`
- **THEN** the system SHALL report an error

#### Scenario: Alias To is not a primary key column of the referenced table
- **WHEN** an `Alias/To` does not match a primary key column of the table named by `References`
- **THEN** the system SHALL report an error

#### Scenario: Two aliases target the same primary key column
- **WHEN** two `Alias` elements in one `ForeignKey` name the same `To` column
- **THEN** the system SHALL report an error for the duplicated mapping

#### Scenario: Only some columns of a composite key are aliased
- **WHEN** a `ForeignKey` declares more than one name and supplies aliases for some but not all
  of them
- **THEN** the system SHALL report a warning that the remaining columns fall back to positional
  matching, which is order-dependent and likely unintended

### Requirement: Recognise the Map convention that declares a Time column
Standard 1.6 overloads `Map` so that a `Map` whose `From` and `To` are equal and are a valid
time mask declares an alphanumeric column as carrying time values, rather than substituting a
value. The system SHALL apply that convention and SHALL flag declarations that appear to
intend it but do not meet it.

#### Scenario: Valid time declaration
- **WHEN** an `AlphaNumeric` column carries a `Map` with `From` and `To` both equal to a valid
  time mask such as `HHMMSS` or `HH:MM:SS TT`
- **THEN** the system SHALL treat the column as a Time column and report no finding

#### Scenario: Time masks differ between From and To
- **WHEN** a `Map` has `From` and `To` that are both valid time masks but are not equal, such
  as `HHMMSS` mapped to `HH:MM:SS`
- **THEN** the system SHALL report a warning that this is a value substitution rather than a
  Time declaration, and is very likely unintended

#### Scenario: Map applied to a non-alphanumeric column
- **WHEN** a `Map` appears on a column declared `Numeric` or `Date`
- **THEN** the system SHALL report an error, because the standard permits `Map` only on
  alphanumeric columns

### Requirement: A medium without tables must say so explicitly
Standard 1.6 permits a `Media` element containing no `Table`, but only as a deliberate choice
signalled by `AcceptNoTables`. The system SHALL report a medium that describes no data, and
SHALL distinguish the deliberate case from the accidental one.

#### Scenario: Empty medium without the marker
- **WHEN** a `Media` element declares no `Table` and no `AcceptNoTables`
- **THEN** the system SHALL report a warning that the medium describes no data

#### Scenario: Empty medium with the marker
- **WHEN** a `Media` element declares no `Table` but does declare `AcceptNoTables`
- **THEN** the system SHALL report an informational finding only

### Requirement: Fixed-length column ranges must be coherent
For `FixedLength` tables the system SHALL verify that every `FixedRange` describes a valid
1-based inclusive span and SHALL report spans that overlap, leave gaps, or exceed the declared
record length.

#### Scenario: Range end precedes range start
- **WHEN** a `FixedRange` declares a `To` smaller than its `From`, or a non-positive `Length`
- **THEN** the system SHALL report an error

#### Scenario: Ranges overlap
- **WHEN** two columns in the same `FixedLength` table declare overlapping spans
- **THEN** the system SHALL report a warning naming both columns

#### Scenario: Ranges leave a gap
- **WHEN** the spans of a `FixedLength` table leave unclaimed positions between the first and
  last declared position
- **THEN** the system SHALL report an informational finding describing the gap

#### Scenario: Range exceeds the declared record length
- **WHEN** a `FixedLength` table declares a `Length` and a column span ends beyond it
- **THEN** the system SHALL report an error

### Requirement: Declared metadata values must be well formed
The system SHALL verify that the scalar values `index.xml` declares are usable, since a
grammar-valid document may still carry values that no importer can act on.

#### Scenario: Non-numeric or negative numeric settings
- **WHEN** `Accuracy`, `ImpliedAccuracy`, `MaxLength`, `SkipNumBytes`, `Epoch` or a `Range`
  bound is not a non-negative integer, or `MaxLength` is zero
- **THEN** the system SHALL report an error naming the element and its value

#### Scenario: Decimal and grouping symbols collide
- **WHEN** a table declares the same character for `DecimalSymbol` and `DigitGroupingSymbol`
- **THEN** the system SHALL report an error

#### Scenario: Delimiters collide
- **WHEN** a `VariableLength` table declares a `ColumnDelimiter` equal to its `RecordDelimiter`
  or to its `TextEncapsulator`
- **THEN** the system SHALL report an error

#### Scenario: Unusable date mask
- **WHEN** a `Date/Format` or `Validity/Format` mask contains no year, month or day placeholder,
  or repeats one of them
- **THEN** the system SHALL report an error naming the mask

#### Scenario: Table record range starts below one
- **WHEN** a `Table/Range/From` is zero or negative
- **THEN** the system SHALL report an error, because the value counts records from one

### Requirement: Table identities must be unambiguous
The system SHALL report tables that cannot be told apart, since `References` resolves on
identity.

#### Scenario: Two tables share a Name
- **WHEN** two `Table` elements declare the same `Name`
- **THEN** the system SHALL report a warning, escalating to an error if any `ForeignKey`
  references that name

#### Scenario: Two tables share a URL
- **WHEN** two `Table` elements declare the same `URL`
- **THEN** the system SHALL report a warning that the same file is described more than once

#### Scenario: Duplicate column names within a table
- **WHEN** two columns of the same table declare the same `Name`
- **THEN** the system SHALL report an error, because foreign keys and aliases resolve columns
  by name

### Requirement: Report names and descriptions that violate documented limits
The standard forbids the characters `"`, `&`, `<` and `>` in table and column names and in
descriptive fields, and states that description text should not exceed 255 characters. The
system SHALL report names and descriptions that breach either limit.

#### Scenario: Reserved character in a name
- **WHEN** a table or column `Name` contains `"`, `&`, `<` or `>` after XML entity decoding
- **THEN** the system SHALL report a warning naming the element and the character

#### Scenario: Overlong description
- **WHEN** a `Description` exceeds 255 characters
- **THEN** the system SHALL report an informational finding with the actual length

### Requirement: Report Command elements without executing them
The standard defines `Command` as an operating-system command run around the import. The
system SHALL surface every `Command` for human review and SHALL never execute one, nor resolve,
open or inspect the file it names.

#### Scenario: Export declares commands
- **WHEN** `index.xml` declares one or more `Command` elements
- **THEN** the system SHALL report a warning for each, quoting the command text and stating
  that it was not executed

#### Scenario: Command names a script present in the export
- **WHEN** a `Command` names a script file that exists in the export
- **THEN** the system SHALL still not execute or read it, and SHALL report it identically

### Requirement: Report Extension elements
Standard 1.3 added `Extension` for application-specific supplements, whose meaning is outside
this standard. The system SHALL surface every `Extension` for human review and SHALL verify
that the supplementary file it names is present in the export.

#### Scenario: Export declares an extension
- **WHEN** `index.xml` declares an `Extension`
- **THEN** the system SHALL report an informational finding naming the extension and its URL,
  and SHALL report an error if that URL does not resolve to an entry in the export
