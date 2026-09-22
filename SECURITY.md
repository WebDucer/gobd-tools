# Security policy

## Reporting a vulnerability

Report it privately, through GitHub's
[report a vulnerability](https://github.com/WebDucer/gobd-tools/security/advisories/new) form.
Please do not open a public issue for one.

This is a spare-time project. Expect an acknowledgement within a week, and a fix in the next
release once the report is confirmed. If something serious is still unfixed after 90 days, publish
it; that is fair.

## What is in scope

Both tools read files that came from somewhere else: an export a company produced, or a data
carrier an auditor handed over. Anything that lets such a file do more than be read and reported
on is worth reporting, for example:

- reading or writing a path outside the export, from `index.xml`, a data file or an archive entry
- the reader's store, which holds an export's data unpacked, becoming readable by another account
  on the machine
- anything that runs code from an export's content

Out of scope: that the downloads are not code-signed. They are attested instead, and the README
says how to check a download against the run that built it. Defects with no security consequence
belong in an issue rather than here.

## Which versions

The most recent release. There are no maintained older lines.

## What each release carries

A software bill of materials for each tool, listing every component it contains, and an
attestation for every file the release carries. The README's "Check it" section says how to verify
both.
