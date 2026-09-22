## MODIFIED Requirements

### Requirement: Report entry presence without reading entry content
The system SHALL determine whether each referenced entry exists using only the export's
directory or archive listing, and SHALL NOT read the content of a data file in order to
establish that it is present. Reading a data file's content SHALL happen only when a content
check is asked for, and SHALL never be required to answer whether a declared file is there.

#### Scenario: Declared table file is absent
- **WHEN** a `Table/URL` resolves to an entry that is not present in the export
- **THEN** the system SHALL report an error naming the table and the unresolved URL

#### Scenario: Multi-gigabyte data files are present
- **WHEN** the export contains data files of several gigabytes
- **THEN** validation SHALL complete without reading their contents and without extracting
  them to disk

#### Scenario: Presence is answered without opening the file
- **WHEN** the presence of every declared file is established
- **THEN** no data file SHALL have been opened, whether or not a content check is also requested

#### Scenario: Content is read only on request
- **WHEN** no content check is requested
- **THEN** the system SHALL behave exactly as it does today: the listing alone answers presence,
  and no data file is read
