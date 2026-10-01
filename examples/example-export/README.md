# Example GoBD export (anonymized, 2025)

A minimal, fully **GoBD/Beschreibungsstandard 1.6-conformant** example export,
derived from a real Papierkram CSV+PDF dump
(`export_webducer_papierkram_2026-09-30_165437_csv_and_pdf_2025`, year 2025).
The source dump is a plain bookkeeping export — CSV tables plus PDFs with no
`index.xml` — so it is **not** a GoBD data carrier as shipped.

```console
$ gobd-validate examples/example-export --contents
GoBD export validation: CONFORMANT
Export: examples/example-export
0 error(s), 0 warning(s), 0 note(s)
```

## Scope (core tables only)

| Table (`Name`) | File | Rows | Primary key | Foreign keys |
| --- | --- | --- | --- | --- |
| `Kontakte` | `kontakte.csv` | 92 | `Kontakt-Nr` | — |
| `Rechnungen` | `rechnungen.csv` | 13 | `Rechnungsnr` | `Kundennummer` → `Kontakte` |
| `Belege` | `belege.csv` | 72 | `Belegnummer` | `Lieferantennummer` → `Kontakte` |
| `Zahlungen` | `zahlungen.csv` | 83 | `Buchung` | `Rechnungsnummer` → `Rechnungen`, `Belegnummer` → `Belege` |
| `Mahnungen` | `mahnungen.csv` | 1 | `Mahnungsnr` | `Rechnungsnummer` → `Rechnungen`, `Kundennummer` → `Kontakte` |

Deliberately **excluded**: PDF receipts/invoices, SEPA mandates, bank
connections, time entries, tasks, projects, articles and all empty tables.
A payment-side `Kundennummer` column was dropped (derivable via
`Zahlungen → Rechnungen → Kontakte`); `Belege` gained an explicit
`Lieferantennummer` (the source only names the supplier, which cannot be a
foreign key).

Reconstructed/dropped rows, so `--contents` resolves every reference:

- Added `R-2024120001` (Dec 2024 opening invoice, customer `K-0027`), known
  only from its Jan 2025 payment of `1.995,00`. Without it that payment would
  dangle (`GOBD5003`).
- Dropped 3 payments to `B-2024000090/91/92`: 2024 documents outside this
  carrier's scope whose parents are not shipped.

## Anonymization

Every personal identifier was replaced with a consistent fictitious value;
system IDs (`K-…`, `L-…`, `R-…`, `B-…`) are kept so references stay traceable:

- Names → `Beispiel-Kunde NN` / `Beispiel-Lieferant NN` (29 + 63); contact
  persons → `Alex-NN Beispiel-NN`.
- Addresses → `Beispielstrasse N`, `10116…`, `Musterstadt`; country kept
  (not personal).
- E-mails → `kontakt-NN@beispiel.example`, `ansprech-NN@beispiel.example`
  (`.example` is RFC 2606-reserved, never real).
- Phones → `+49 30 901810NN` (Berlin test range); any of phone/mobile/cell
  present in the source yields one `Telefon` value.
- Websites → `https://beispiel-NN.example`; UStIDs → `DE999000NNN`;
  IBANs → `DE009999999999999999NN` (check digits `00`, never a real account).
- Invoice subjects: `OSOI` → `Beispiel`; receipt subjects naming real
  suppliers neutralised (`Amazon (AWS) Backup`/`Azure Backup` → `Cloud-Backup`,
  `IONOS Homepage` → `Homepage`, `Netlify Domain` → `Domain`,
  `Google Commerce Ltd - …` → `Computerhardware`,
  `Rechnung Papierkram.de 2025` → `Software-Abo 2025`).
- Dropped as out of scope: fax, Skype, bank account/BLZ/BIC details, SEPA
  mandate references, free-text notes (incl. multiline `Notizen`), `Benutzer`
  (`Eugen Richter`) and `Bankverbindungen` (real IBANs).

Verified: no source contact name, street, e-mail, phone, UStID or IBAN occurs
anywhere in this folder; remaining brand words (`ForkLift`, `Nextcloud`,
order refs) are product/order labels, not personal data.

## Format normalisation (what makes it GoBD)

- `index.xml` + byte-identical `gdpdu-01-03-2019.dtd` at the root; one
  `Media`, five `VariableLength` tables, `UTF8`, `,` decimals / `.` grouping.
- No header rows in the CSVs (column order = `index.xml` order, as in
  `tests/GoBd.Validation.Tests/Fixtures/good-export/`); `;` columns, CRLF
  records, `"` encapsulator, UTF-8 without BOM.
- All dates `DD.MM.YYYY` (ISO `2025-01-01` forms converted); all amounts
  German format with `Accuracy` 2 (`520.0` → `520,00`); `Level` is
  `Accuracy` 0. No `MaxLength` is declared (unbounded beats a wrong bound).
- All key columns `AlphaNumeric` on both sides of each join; empty foreign
  key fields mean "absent" (each payment links exactly one of invoice/receipt).

## Reproduce

The generator is not committed (it reads the non-public source dump outside
this repo). The rules above are the specification; re-running them against the
source dump must yield byte-identical CSVs (modulo the DTD copy, which is
`src/GoBd.Validation/Resources/gdpdu-01-03-2019.dtd`).

Note on line endings: the CSVs and the DTD use CRLF. They are marked `binary`
in the root `.gitattributes` so Git never normalises them — an LF-converted
checkout would fail validation (`GOBD2004`/`GOBD4007` class defects).
