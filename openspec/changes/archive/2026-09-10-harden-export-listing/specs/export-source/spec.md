## ADDED Requirements

### Requirement: The listing never exposes anything outside the export root
The entry listing is what every later check trusts, so it is the boundary that keeps an
untrusted medium from reaching the rest of the filesystem. The system SHALL exclude from the
listing any entry that does not denote a file inside the export root, and SHALL confine every
read to that root regardless of how the entry was obtained.

#### Scenario: An entry name that escapes the root is not listed
- **WHEN** an entry's normalised name contains a `..` segment that leaves the export root, which
  can arise from a file whose literal name contains a backslash on a filesystem that permits one
- **THEN** the entry SHALL NOT appear in the listing, and SHALL NOT be found by a lookup

#### Scenario: A symbolic link is not listed
- **WHEN** the export contains a symbolic link
- **THEN** it SHALL NOT appear in the listing, because it denotes a file the medium does not
  carry and following it would read whatever it points at

#### Scenario: A symbolically linked directory is not descended into
- **WHEN** a directory inside the export is a symbolic link
- **THEN** no file beneath it SHALL appear in the listing, because such files are not links
  themselves and would otherwise arrive under ordinary in-root names

#### Scenario: A lookup and a read describe the same file
- **WHEN** an entry is found in the listing and then opened
- **THEN** the bytes read SHALL be those of the entry that was found, so that the size a check
  saw and the content it inspected cannot come from two different files

#### Scenario: Reads are confined even when a name reaches the index
- **WHEN** a read is attempted for a name that resolves outside the export root
- **THEN** the system SHALL refuse it rather than open the file, so that confinement does not
  depend on the listing having excluded it first

#### Scenario: A ZIP entry named to escape the root is not listed
- **WHEN** a ZIP archive declares a member whose name escapes the export root
- **THEN** the entry SHALL NOT appear in the listing, even though nothing is extracted and the
  name could not reach the filesystem today

#### Scenario: No hash or path of an out-of-root file can reach a report
- **WHEN** an export is crafted so that a file outside its root would be selected for inspection
- **THEN** no finding SHALL disclose that file's content, fingerprint or absolute path

### Requirement: Colliding entry names are reported
Two entries can resolve to one name, and which of them an importer reads is undefined. The
system SHALL report such a collision rather than resolving it silently, and SHALL NOT fail the
run because of one.

#### Scenario: Two files in a folder resolve to one name
- **WHEN** a folder export holds `data/x.csv` alongside a file whose literal name is
  `data\x.csv`, so both normalise to the same entry name
- **THEN** the system SHALL report an error naming the collision and how many entries share it

#### Scenario: A ZIP carries the same member name twice
- **WHEN** an archive declares the same member name more than once, which the format permits
- **THEN** the system SHALL report the collision, because differing tools may read different
  members and reach different conclusions from one medium

#### Scenario: A collision does not stop the run
- **WHEN** a collision is present
- **THEN** validation SHALL continue and report its other findings, rather than failing before
  it can report anything

#### Scenario: An export without collisions reports none
- **WHEN** every entry resolves to a distinct name
- **THEN** no collision SHALL be reported
