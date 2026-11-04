# Design decisions

## Structure

- Controllers handle API requests.
- `ImportService` handles the import rules.
- Source clients return sample transaction lists.
- Angular displays the data.
- SQLite stores the records and history.

## Data rules

- Use source name and transaction ID together to identify a record.
- Store money as whole cents. Use `decimal` when processing amounts.
- Allow negative amounts for refunds.
- Use UTC for all dates.
- Text fields must have fewer than 20 characters.
- Card numbers contain four digits or are empty.
- Use `USB-C Dock` instead of the longer sample product name.

## Import rules

- Check the full source list before making changes.
- Reject incomplete, stale, or invalid lists.
- Add new records and update changed fields.
- Save each field's old and new values in history.
- Repeating the same import does not add duplicate records or history.
- Revoke missing active records only within the source's covered time period and the last 24 hours. Exactly 24 hours is included.
- Keep missing older records unchanged.
- Make returning revoked records active again.
- Save a source's changes and history together. Undo them if saving fails.
- Let other sources continue if one fails.
- Allow one import at a time.

## Limits

- Sample sources only; no real source APIs.
- One backend instance per database.
- No sign-in, pagination, or scheduled job.
- Older records are not finalized.
- A crash can leave an import marked Running.
- Very large amounts need extra decimal handling in the browser.

A future scheduler can call `ImportService.ImportAsync()`.
