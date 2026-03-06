# CsvProcessorForCallbi

Technical assessment implementation for CSV import, validation, persistence, and dashboard display.

## Run with Docker (required)

This repository includes:
- `backend/Dockerfile`
- `frontend/Dockerfile`
- `docker-compose.yml` (SQLite + backend + frontend)

Start the full stack:

```bash
docker compose up --build
```

Services:
- Frontend: `http://localhost:9000`
- Backend API: `http://localhost:5062`
- SQLite DB persists in named volume `sqlite-data` at `/data/transactions.db`

## CSV configuration (delimiter + date format)

Both values are configured in:

`backend/TransactionProcessorAPI/appsettings.json`

```json
"Csv": {
  "Delimiter": ",",
  "TransactionTimeFormat": "dd/MM/yyyy"
}
```

### Change delimiter
- Set `"Delimiter"` to one of:
  - `","` (comma)
  - `";"` (semicolon)
  - `"|"` (pipe)
  - `"\\t"` or `"tab"` (tab)
- Restart backend after changes.

### Change transaction time format
- Set `"TransactionTimeFormat"` to your required .NET date format (for example `dd/MM/yyyy` or `dd/MM/yyyy HH:mm:ss`).
- CSV `TransactionTime` values must match this format exactly.
- Restart backend after changes.

## Sample CSV structure for testing

Expected header (exact order):

```csv
TransactionTime,Amount,Description,TransactionId
```

Field rules:
- `TransactionTime`: must match configured `TransactionTimeFormat`
- `Amount`: decimal with exactly 2 decimal places (example `123.45`)
- `Description`: non-empty string
- `TransactionId`: valid GUID

Sample files included in `frontend/`:
- `sample-transactions-1000.csv` (comma, 1000 records)
- `sample-transactions-comma.csv`
- `sample-transactions-semicolon.csv`
- `sample-default-time.csv`
- `sample-no-time.csv`
- `sample-transactions-with-time.csv`

### Generate your own sample CSV

From `frontend/`:

```bash
npm run generate:csv -- --count=1000 --output=sample-transactions-1000.csv
```

Optional:
- `--delimiter=,`
- `--delimiter=;`
- `--delimiter=|`
- `--delimiter=\t` or `--delimiter=tab`
- `--include-time=false` (time is included by default)

## Frontend upload testing

1. Open `http://localhost:9000`
2. Use the file input and upload a CSV
3. Review success/error feedback above the table
4. View imported rows in the table with pagination
