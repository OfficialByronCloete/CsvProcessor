namespace TransactionProcessor.Common.Options;

public sealed class CsvOptions
{
    public const string SectionName = "Csv";

    public string? Delimiter { get; init; }
    public string? TransactionTimeFormat { get; init; }
}
