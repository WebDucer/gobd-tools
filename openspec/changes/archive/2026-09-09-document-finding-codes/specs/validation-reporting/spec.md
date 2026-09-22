## ADDED Requirements

### Requirement: Every finding code is documented
Finding codes are the interface downstream systems suppress and track by, and the identifier a
person searches for when a build fails. The project SHALL carry a reference documenting every
code the system can emit, and that reference SHALL be verified against the code catalogue rather
than maintained by good intentions.

#### Scenario: Every code the system can emit is documented
- **WHEN** the code catalogue is compared against the reference
- **THEN** every code in the catalogue SHALL have an entry in the reference

#### Scenario: The reference documents no code that does not exist
- **WHEN** the reference is compared against the code catalogue
- **THEN** every code documented SHALL exist in the catalogue, so that a removed or renamed code
  cannot leave a stale entry behind

#### Scenario: Adding a code without documenting it fails the build
- **WHEN** a new finding code is added to the catalogue and no entry is written for it
- **THEN** the verification SHALL fail, because a reference that silently falls behind is worse
  than none once people rely on it

#### Scenario: The reference agrees with the catalogue about severity and category
- **WHEN** an entry states a code's severity or its category
- **THEN** that statement SHALL match the catalogue, so the reference cannot disagree about
  whether a code counts against the verdict

#### Scenario: An entry explains rather than restates
- **WHEN** a reader consults an entry for a code
- **THEN** it SHALL say what the finding means, what triggers it, why it carries the severity it
  does, and what a producer should change — not merely repeat the message text, which the reader
  already has
