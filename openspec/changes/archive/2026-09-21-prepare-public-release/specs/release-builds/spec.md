## Purpose

Says what every release of the tools carries and how it is identified: the version a build
reports, a software bill of materials for each tool, and attestations that let a person check
where a download came from.

## ADDED Requirements

### Requirement: A release takes its version from its tag
A version that nobody sets says nothing: every release so far reported the same one, so a person
could not tell which release they were running, and an auditor could not say which version found
a defect. The tag is the one place a release is named, so the version has to come from it. The
system SHALL derive the version of every build made for a release tag from that tag, and SHALL
report that same version from both tools and from the macOS application.

A release tag is `v`, then a major version, then optionally `.` and a minor version, then
optionally `.` and a patch version, each a whole number. A part that is omitted is `0`. The
version is reported as `<major>.<minor>.<patch>.0`.

#### Scenario: A tag with every part
- **WHEN** a release is built for the tag `v0.3.7`
- **THEN** its builds SHALL report the version `0.3.7.0`

#### Scenario: A tag with the major version only
- **WHEN** a release is built for the tag `v1`
- **THEN** its builds SHALL report the version `1.0.0.0`

#### Scenario: A tag without a patch version
- **WHEN** a release is built for the tag `v1.2`
- **THEN** its builds SHALL report the version `1.2.0.0`

#### Scenario: Every build of a release reports the same version
- **WHEN** a release is built for a tag
- **THEN** the validator and the reader SHALL report the same version on every platform, and the
  macOS application SHALL present it to macOS as `<major>.<minor>.<patch>`, such as `1.0.0` for
  the tag `v1`

### Requirement: A build that is not for a release cannot be taken for one
Builds are made for every commit, by the pipeline and on developers' machines, and a person may
be handed one of them. If it reported a release's version, it would pass for a release it is not.
The system SHALL report the version `0.0.0.0` from every build that is not made for a release tag.

#### Scenario: A build of a branch
- **WHEN** the pipeline builds a commit that is not being released under a tag
- **THEN** its builds SHALL report the version `0.0.0.0`

#### Scenario: A build on a developer's machine
- **WHEN** a tool is built without naming a release tag
- **THEN** it SHALL report the version `0.0.0.0`

### Requirement: A tag that is not a version releases nothing
The release pipeline starts for every tag that begins with `v`, and a tag such as `v1.2-rc1` or
`v1.2.3.4` names no version this rule can read. Guessing one would publish a release under a
version nobody chose. The system SHALL refuse to build a release for such a tag, and SHALL say
which tag it refused and what form a release tag takes.

#### Scenario: A tag with a suffix
- **WHEN** a release is built for the tag `v1.2-rc1`
- **THEN** the build SHALL fail, naming the tag and the expected form, and nothing SHALL be
  attached to a release

#### Scenario: A tag with a fourth part
- **WHEN** a release is built for the tag `v1.2.3.4`
- **THEN** the build SHALL fail, naming the tag and the expected form

#### Scenario: A part too large for a version
- **WHEN** a release is built for a tag with a part larger than a version can hold, such as
  `v70000`
- **THEN** the build SHALL fail, naming the tag and the largest value a part may take

### Requirement: Each release carries a software bill of materials for each tool
A person deciding whether a download is affected by a newly published vulnerability, or an
organisation keeping an inventory of the software it runs, needs to know which components each
tool contains and in which version, in a form their own tools can read. The notices say the same
for a person, but not for a machine. The system SHALL attach to every release one software bill
of materials for each tool, in CycloneDX JSON of specification version 1.6 or later. Each SHALL
list every third-party component that tool's downloads contain, with its version.

#### Scenario: One bill of materials per tool
- **WHEN** a release is published
- **THEN** it SHALL carry `gobd-validate.cdx.json` and `gobd-reader.cdx.json` beside the
  downloads

#### Scenario: Every platform is covered
- **WHEN** a component ships in a tool's download for any one platform
- **THEN** that tool's bill of materials SHALL list it

#### Scenario: The runtime is listed
- **WHEN** a tool contains the .NET runtime
- **THEN** its bill of materials SHALL list the runtime, with the version the build used

#### Scenario: What only builds a tool is not listed
- **WHEN** a package is used to build a tool but does not ship in any of its downloads
- **THEN** that tool's bill of materials SHALL NOT list it

#### Scenario: The bill of materials names its tool and version
- **WHEN** a person reads a tool's bill of materials
- **THEN** it SHALL name the tool and the same version the tool's downloads report

#### Scenario: The bill of materials agrees with the notices
- **WHEN** a release is built
- **THEN** each tool's bill of materials and the third-party notices SHALL name the same
  third-party components, and the build SHALL fail when they do not

### Requirement: Every download can be traced to the build that made it
The downloads are not code-signed, so no operating system tells a person who made them, and a
copy passed around an organisation could have been replaced on its way. A signed attestation,
made by the workflow that built the file, lets anyone check that a file is exactly what this
project's release workflow produced, and which bill of materials describes it. The system SHALL
publish, for every file attached to a release, an attestation of the build that produced it. For
each tool's downloads, it SHALL also publish an attestation binding them to that tool's bill of
materials. Both SHALL be verifiable with GitHub's command-line tool against the project's
repository.

#### Scenario: A download verifies
- **WHEN** a person runs `gh attestation verify <file> -R <repository>` for any file attached to
  a release
- **THEN** the verification SHALL succeed, and SHALL name the release workflow and the tag it
  ran for

#### Scenario: A changed file does not verify
- **WHEN** a file attached to a release has been changed in any byte, and a person verifies it
- **THEN** the verification SHALL fail

#### Scenario: A download leads to its bill of materials
- **WHEN** a person verifies a tool's download with the CycloneDX predicate type,
  `--predicate-type https://cyclonedx.org/bom`
- **THEN** the verification SHALL succeed and yield that tool's bill of materials

#### Scenario: Only releases are attested
- **WHEN** a build is made that is not released, such as a CI build or a dry run of the release
  workflow
- **THEN** no attestation SHALL be published for it
