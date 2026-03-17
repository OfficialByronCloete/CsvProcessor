using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Options;
using TransactionProcessor.Common.Models;
using TransactionProcessor.Common.Options;
using TransactionProcessor.Core.Contracts;

namespace TransactionProcessor.Services.Services;

public class TransactionService(
    ITransactionRepository transactionRepository,
    IOptions<CsvOptions> csvOptions) : ITransactionService
{
    private static readonly string[] ExpectedHeader =
    [
        "TransactionTime",
        "Amount",
        "Description",
        "TransactionId"
    ];

    private const int RequiredAmountDecimalPlaces = 2;

    public async Task<CsvImportSummary> ParseAndSubmitTransactionCsvAsync(Stream csvStream, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(csvStream);

        using var reader = new StreamReader(csvStream, leaveOpen: true);
        var transactionTimeFormat = GetTransactionTimeFormat(csvOptions.Value.TransactionTimeFormat);
        var delimiter = GetDelimiter(csvOptions.Value.Delimiter);
        var csvConfiguration = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            TrimOptions = TrimOptions.Trim
        };

        if (delimiter is null)
        {
            csvConfiguration.DetectDelimiter = true;
            csvConfiguration.DetectDelimiterValues = [",", ";", "\t"];
        }
        else
        {
            csvConfiguration.Delimiter = delimiter;
        }

        using var csv = new CsvReader(reader, csvConfiguration);
        var transactions = new List<TransactionModel>();

        var hasHeader = await csv.ReadAsync();
        if (!hasHeader)
            throw new InvalidDataException("CSV is empty. Expected a header row.");

        csv.ReadHeader();
        GuardHeaderFormat(csv.HeaderRecord ?? [], delimiter);

        var lineNo = 1;

        while (await csv.ReadAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();
            lineNo++;
            var columns = csv.Context.Parser?.Record ?? [];

            if (columns.Length == 0 || columns.All(string.IsNullOrWhiteSpace))
                throw new InvalidDataException($"Invalid CSV data at line {lineNo}: line is empty.");

            var transaction = GuardAndMapRow(columns, lineNo, transactionTimeFormat);
            transactions.Add(transaction);
        }

        var addedCount = await transactionRepository.SubmitTransactionsAsync(transactions, cancellationToken);
        var duplicateCount = transactions.Count - addedCount;

        return new CsvImportSummary
        {
            AddedCount = addedCount,
            DuplicateCount = duplicateCount,
            TotalProcessedCount = transactions.Count
        };
    }

    public Task<PagedResult<TransactionModel>> GetTransactionsAsync(int pageNumber, int pageSize, CancellationToken cancellationToken)
    {
        if (pageNumber <= 0)
            throw new ArgumentOutOfRangeException(nameof(pageNumber), "PageNumber must be greater than 0.");

        if (pageSize <= 0 || pageSize > 200)
            throw new ArgumentOutOfRangeException(nameof(pageSize), "PageSize must be between 1 and 200.");

        return transactionRepository.GetTransactionsAsync(pageNumber, pageSize, cancellationToken);
    }

    public Task<bool> UpdateTransactionAsync(TransactionModel transaction, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        if (transaction.TransactionId == Guid.Empty)
            throw new ArgumentException("TransactionId is required.", nameof(transaction));

        if (string.IsNullOrWhiteSpace(transaction.Description))
            throw new ArgumentException("Description is required.", nameof(transaction));

        return transactionRepository.UpdateTransactionAsync(transaction, cancellationToken);
    }

    public Task<bool> DeleteTransactionAsync(Guid transactionId, CancellationToken cancellationToken)
    {
        if (transactionId == Guid.Empty)
            throw new ArgumentException("TransactionId is required.", nameof(transactionId));

        return transactionRepository.DeleteTransactionAsync(transactionId, cancellationToken);
    }

    private static TransactionModel GuardAndMapRow(string[] columns, int lineNo, string transactionTimeFormat)
    {
        if (columns.Length != 4)
            throw new InvalidDataException($"Invalid CSV data at line {lineNo}: expected 4 columns but found {columns.Length}.");

        var transactionTimeValue = GetColumnValue(columns, 0);
        var transactionAmountValue = GetColumnValue(columns, 1);
        var descriptionValue = GetColumnValue(columns, 2);
        var transactionIdValue = GetColumnValue(columns, 3);

        if (string.IsNullOrWhiteSpace(descriptionValue))
            throw new InvalidDataException($"Invalid CSV data at line {lineNo}: 'Description' is required.");

        if (!TryParseTransactionTime(transactionTimeValue, transactionTimeFormat, out var transactionTime))
            throw new InvalidDataException($"Invalid CSV data at line {lineNo}: 'TransactionTime' must match configured format '{transactionTimeFormat}'.");

        if (!decimal.TryParse(transactionAmountValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var transactionAmount))
            throw new InvalidDataException($"Invalid CSV data at line {lineNo}: 'Amount' must be a valid decimal.");

        if (!HasExactDecimalPlaces(transactionAmount, RequiredAmountDecimalPlaces))
            throw new InvalidDataException($"Invalid CSV data at line {lineNo}: 'Amount' must have exactly {RequiredAmountDecimalPlaces} decimal places.");

        if (string.IsNullOrWhiteSpace(transactionIdValue))
            throw new InvalidDataException($"Invalid CSV data at line {lineNo}: 'TransactionId' is required.");

        if (!Guid.TryParse(transactionIdValue, out Guid transactionId))
            throw new InvalidDataException($"Invalid CSV data at line {lineNo}: 'TransactionId' must be a valid GUID.");

        return new TransactionModel
        {
            TransactionId = transactionId,
            TransactionTime = transactionTime,
            TransactionAmount = transactionAmount,
            Description = descriptionValue
        };
    }

    private static void GuardHeaderFormat(string[] incomingCsvHeaders, string? configuredDelimiter)
    {
        if (incomingCsvHeaders.Length != ExpectedHeader.Length)
        {
            if (incomingCsvHeaders.Length == 1 &&
                configuredDelimiter is not null &&
                TryDetectDelimiter(incomingCsvHeaders[0], configuredDelimiter, out var detectedDelimiter))
                throw new InvalidDataException(
                    $"CSV delimiter mismatch. The API is configured for delimiter '{DisplayDelimiter(configuredDelimiter)}', " +
                    $"but the uploaded file appears to use '{DisplayDelimiter(detectedDelimiter)}'.");

            throw new InvalidDataException(
                $"Invalid CSV header column count. Expected {ExpectedHeader.Length} columns: '{string.Join(",", ExpectedHeader)}'. " +
                $"Got {incomingCsvHeaders.Length}: '{string.Join(",", incomingCsvHeaders)}'.");
        }

        for (var i = 0; i < ExpectedHeader.Length; i++)
        {
            var actualCsvHeader = incomingCsvHeaders[i].Trim();
            var expectedCsvHeader = ExpectedHeader[i];

            if (!string.Equals(actualCsvHeader, expectedCsvHeader, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException(
                    $"Invalid CSV header at position {i + 1}. Expected '{expectedCsvHeader}', got '{actualCsvHeader}'. " +
                    $"Expected header: '{string.Join(",", ExpectedHeader)}'.");
        }
    }

    private static string GetColumnValue(string[] cols, int index)
    {
        if ((uint)index >= (uint)cols.Length)
            return string.Empty;

        return cols[index].Trim();
    }

    // This method is used to detect if the uploaded CSV file uses a different delimiter than the one configured in the API.
    private static bool TryDetectDelimiter(string header, string configuredDelimiter, out string detectedDelimiter)
    {
        foreach (var candidate in new[] { ",", ";", "|", "\t" })
        {
            if (candidate == configuredDelimiter)
                continue;

            if (header.Contains(candidate, StringComparison.Ordinal))
            {
                detectedDelimiter = candidate;
                return true;
            }
        }

        detectedDelimiter = string.Empty;
        return false;
    }

    private static string DisplayDelimiter(string delimiter)
        => delimiter == "\t" ? "\\t" : delimiter;

    private static string? GetDelimiter(string? configuredDelimiter)
    {
        if (string.IsNullOrWhiteSpace(configuredDelimiter))
            return null;

        var delimiter = configuredDelimiter.Trim();

        if (string.Equals(delimiter, "\\t", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(delimiter, "tab", StringComparison.OrdinalIgnoreCase))
            return "\t";

        return delimiter;
    }

    private static string GetTransactionTimeFormat(string? configuredFormat)
    {
        if (string.IsNullOrWhiteSpace(configuredFormat))
            throw new InvalidOperationException("CSV setting 'TransactionTimeFormat' is required.");

        return configuredFormat.Trim();
    }

    private static bool TryParseTransactionTime(string value, string format, out DateTime transactionTime)
        => DateTime.TryParseExact(
            value,
            format,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out transactionTime);

    private static bool HasExactDecimalPlaces(decimal value, int requiredDecimalPlaces)
        => value == Math.Round(value, requiredDecimalPlaces);
}
