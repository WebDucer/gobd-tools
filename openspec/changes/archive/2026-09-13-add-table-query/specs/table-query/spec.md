## Purpose

Lets a person narrow, reorder and total a table's records by what its columns declare. Each view
leaves every value as the file stores it and keeps every record citable by its number.

## ADDED Requirements

### Requirement: Records are filtered by what their columns declare
Comparing `1.234,50` with `999,00` as text gets the answer wrong, and so does reading a date under
the machine's locale. The system SHALL let a person filter a table's records by any of its
columns, with the comparisons that column's declared type supports, and SHALL read what the
person enters under the same declaration the grid's values are written in.

#### Scenario: A number compares as a number
- **WHEN** a `Numeric` column is filtered to values less than `10`
- **THEN** a record holding `9,50` SHALL remain and one holding `10,00` SHALL NOT, and a value
  written with its sign after the digits, such as `1782,90-`, SHALL compare as the negative
  number it denotes

#### Scenario: A range of numbers, dates or times
- **WHEN** a `Numeric`, `Date` or Time column is filtered between two bounds
- **THEN** only records whose value lies within both bounds, inclusive, SHALL remain, and a bound
  left open SHALL leave that side unbounded

#### Scenario: A two-digit year is read with the declared window
- **WHEN** a `Date` column whose mask writes a two-digit year is filtered or sorted
- **THEN** each year SHALL be read with the table's declared two-digit-year window, as the
  validator reads it

#### Scenario: A column that declares time
- **WHEN** an `AlphaNumeric` column declares a Time `Map`
- **THEN** it SHALL be offered the comparisons of a time, and its values SHALL be read under the
  declared time mask

#### Scenario: Text matched exactly or by pattern
- **WHEN** an `AlphaNumeric` column is filtered by equals, not equals, contains, starts with, ends
  with, or a pattern in which `*` stands for any text and `?` for one character
- **THEN** only records matching it SHALL remain, and every other character of the filter,
  including `%` and `_`, SHALL match only itself

#### Scenario: Case is ignored only when asked
- **WHEN** a text filter is marked to ignore case
- **THEN** values that differ from it only in letter case SHALL match, and without that mark they
  SHALL NOT

#### Scenario: One of several values
- **WHEN** a column is filtered to one of a list of values
- **THEN** records whose value matches any value in the list SHALL remain

#### Scenario: Empty values
- **WHEN** a column is filtered to empty, or to not empty, values
- **THEN** records SHALL remain accordingly, and an empty value SHALL satisfy no comparison other
  than "is empty", because an empty field is an absent value

#### Scenario: A redefined value is filtered as it is shown
- **WHEN** a column declaring a `Map` is filtered by text
- **THEN** the value compared SHALL be the redefined value the grid shows, not the value as
  stored

#### Scenario: What a person enters is read under the declaration
- **WHEN** a person enters a number, a date or a time as a filter bound
- **THEN** it SHALL be read with the table's declared decimal and grouping symbols, or the
  column's date or time mask, never the machine's locale; and input that cannot be read SHALL be
  refused, stating the form expected, rather than applied

#### Scenario: Filters on several columns
- **WHEN** filters are set on more than one column
- **THEN** only records satisfying every one of them SHALL remain

#### Scenario: Nothing matches
- **WHEN** no record satisfies the filters
- **THEN** the view SHALL state that no record matches, rather than presenting an unexplained
  empty table

### Requirement: Records are sorted by what a column declares
Sorting `9` after `10` because `1` comes before `9` is sorting text, not numbers. The system SHALL
let a person sort a table's records by up to three columns, each in either direction, applied in
the order that person chose them, by what each column's declared type means.

#### Scenario: Sorted by value, not by text
- **WHEN** a table is sorted by a `Numeric`, `Date` or Time column
- **THEN** records SHALL be ordered by the number, date or time each value denotes, so that `9`
  comes before `10` and `31.12.2024` before `01.01.2025`

#### Scenario: Text is sorted the same way on every machine
- **WHEN** a table is sorted by an `AlphaNumeric` column
- **THEN** records SHALL be ordered by the value the grid shows, in an order that does not depend
  on the machine's locale

#### Scenario: Equal values keep file order
- **WHEN** several records carry the same value in every sorted column
- **THEN** they SHALL keep the order the file delivers them in

#### Scenario: Empty values come last
- **WHEN** a table is sorted, in either direction
- **THEN** records whose value in a sorted column is empty SHALL come after every record that has
  one in that column

#### Scenario: Several columns, in the order they were chosen
- **WHEN** a person sorts by more than one column
- **THEN** records SHALL be ordered by the first column chosen, records equal in it by the second,
  and those equal in both by the third

#### Scenario: No more than three columns
- **WHEN** three columns are already sorted by
- **THEN** the system SHALL NOT apply a fourth, and SHALL say so rather than silently dropping one

### Requirement: A view says what it is and can be undone
A filtered or sorted table that looks like the table itself invites a person to cite a partial
list as the whole. The system SHALL state what a view shows and SHALL let a person return to the
table in file order in one action.

#### Scenario: The view states what it shows
- **WHEN** a table is filtered or sorted
- **THEN** the system SHALL state how many records the view shows out of how many the table
  holds, which filters apply and which sort

#### Scenario: Back to file order
- **WHEN** a person asks to return to file order
- **THEN** every filter and the sort SHALL be removed at once, and the records SHALL appear in
  the order the file delivers them

#### Scenario: Position and record number are told apart
- **WHEN** a view is presented
- **THEN** each record SHALL show both its position in the view and its record number as the file
  counts records, and the two SHALL be labelled so they cannot be confused

### Requirement: A view belongs to its table's tab
Filters and sorts are part of where a person is in a table. The system SHALL keep a table's view
with that table's tab, as it keeps the tab's position.

#### Scenario: Returning to a filtered table
- **WHEN** a person leaves a filtered or sorted table and returns to it
- **THEN** its filters, sort and chosen figures SHALL be as they were left

#### Scenario: Closing a filtered table
- **WHEN** a person closes a filtered or sorted table's tab and opens the table again
- **THEN** the table SHALL open in file order, unfiltered, with no figures chosen

#### Scenario: Views do not affect each other
- **WHEN** one table is filtered or sorted
- **THEN** no other table's view SHALL change

### Requirement: Figures are computed over the view, as the person chooses
Whether a column's sum means anything depends on what the column holds: the sum of amounts is a
figure, and the sum of account numbers is not. The declaration cannot tell them apart, so the
system SHALL compute only the figures a person chooses, SHALL compute them over the records the
view holds, and SHALL compute them exactly or say that it cannot.

#### Scenario: Nothing is totalled unasked
- **WHEN** a table's data is presented
- **THEN** no figure SHALL be shown for any column until a person chooses one

#### Scenario: Figures offered by type
- **WHEN** a person chooses a figure for a column
- **THEN** count and distinct count SHALL be offered for every column, minimum and maximum for
  `Numeric`, `Date` and Time columns, and sum and average for `Numeric` columns only, including
  columns forming a key

#### Scenario: Figures follow the view
- **WHEN** the filters of a view change
- **THEN** every chosen figure SHALL be computed again over the records the view now holds

#### Scenario: Figures are exact
- **WHEN** a count, distinct count, sum, minimum or maximum is shown
- **THEN** it SHALL be exact, and a sum that cannot be computed exactly SHALL say so rather than
  show an approximation

#### Scenario: An average is marked as rounded
- **WHEN** an average is shown
- **THEN** it SHALL be rounded to the column's decimal places and SHALL be marked as rounded

#### Scenario: Empty values are counted, not computed with
- **WHEN** a column holds empty values among the records of the view
- **THEN** count SHALL count the values that are not empty, sum, average, minimum and maximum SHALL
  leave empty values out, and the figure SHALL state how many were left out

#### Scenario: Figures are written as the export writes values
- **WHEN** a figure is shown
- **THEN** a number SHALL be written with the table's declared decimal and grouping symbols, and a
  date or time in the column's mask

### Requirement: What the reader cannot compute with is stated
A figure that silently leaves out a value it could not hold presents a guess as a fact. The
system SHALL state which columns it cannot filter, sort or compute with, and why, and SHALL still
show their values.

#### Scenario: A number beyond what the reader computes exactly
- **WHEN** a `Numeric` column holds a value the reader cannot represent exactly
- **THEN** the column's values SHALL still be shown, no filter, sort or figure SHALL be offered for
  that column, and the column SHALL say why

#### Scenario: The limit is stated when the export is read
- **WHEN** reading an export finds such a column
- **THEN** the summary SHALL state it as a limit of the reader, apart from the findings and
  carrying no finding code, because the export is not defective and the validator would not
  report it

#### Scenario: A Time column holding values that are not times
- **WHEN** a Time column holds values that cannot be read under its time mask
- **THEN** those values SHALL be treated as absent by time filters, sorting and figures, and the
  column SHALL state how many there are

### Requirement: Preparing a view keeps the reader responsive
Filtering or sorting a table of millions of records takes time. The system SHALL prepare a view
without blocking the reader and without holding the table's records in its own memory.

#### Scenario: The reader stays responsive
- **WHEN** a view is being prepared
- **THEN** the reader SHALL continue to respond to input, SHALL show that the view is being
  prepared, and SHALL keep showing the previous view until the new one is ready

#### Scenario: A newer request replaces an older one
- **WHEN** a person changes a filter or sort before the previous change has been prepared
- **THEN** the previous preparation SHALL be abandoned, and only the latest view SHALL be
  presented

#### Scenario: A view that cannot be prepared
- **WHEN** preparing a view fails, including for lack of space
- **THEN** the table's tab SHALL say so and keep its previous view, and every other tab SHALL
  remain usable

#### Scenario: Memory does not grow with the table
- **WHEN** a filtered or sorted view of a table of millions of records is prepared and scrolled
- **THEN** the reader SHALL hold only the records it is presenting, not the records the view
  selects
