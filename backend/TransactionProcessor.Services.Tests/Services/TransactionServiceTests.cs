using Microsoft.Extensions.Options;
using Moq;
using TransactionProcessor.Common.Models;
using TransactionProcessor.Common.Options;
using TransactionProcessor.Core.Contracts;
using TransactionProcessor.Services.Services;

namespace TransactionProcessor.Services.Tests.Services;

[TestClass]
public class TransactionServiceTests
{
    private Mock<ITransactionRepository> _transactionRepository = null!;
    private TransactionService _sut = null!;
    private Mock<IOptions<CsvOptions>> _csvOptions = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        _transactionRepository = new Mock<ITransactionRepository>();
        _csvOptions = new Mock<IOptions<CsvOptions>>();
        _csvOptions
            .SetupGet(o => o.Value)
            .Returns(new CsvOptions
            {
                Delimiter = ",",
                TransactionTimeFormat = "dd/MM/yyyy"
            });
        _sut = new TransactionService(_transactionRepository.Object, _csvOptions.Object);
    }

    [TestMethod]
    public async Task ParseAndSubmitTransactionCsvAsync_ValidCsv_SubmitsTransactions()
    {
        // Arrange
        IReadOnlyCollection<TransactionModel>? capturedTransactions = null;
        _transactionRepository
            .Setup(r => r.SubmitTransactionsAsync(It.IsAny<IReadOnlyCollection<TransactionModel>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyCollection<TransactionModel>, CancellationToken>((transactions, _) => capturedTransactions = transactions)
            .ReturnsAsync(2);
        var csv = """
            TransactionTime,Amount,Description,TransactionId
            01/03/2026,10.50,Payment,11111111-1111-1111-1111-111111111111
            02/03/2026,20.25,Refund,22222222-2222-2222-2222-222222222222
            """;

        // Act
        await _sut.ParseAndSubmitTransactionCsvAsync(ToStream(csv), CancellationToken.None);

        // Assert
        Assert.IsNotNull(capturedTransactions);
        Assert.HasCount(2, capturedTransactions);
        _transactionRepository.Verify(r => r.SubmitTransactionsAsync(It.IsAny<IReadOnlyCollection<TransactionModel>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task ParseAndSubmitTransactionCsvAsync_EmptyFile_ThrowsInvalidDataException()
    {
        // Act
        var ex = await ExpectThrowsAsync<InvalidDataException>(
            () => _sut.ParseAndSubmitTransactionCsvAsync(ToStream(string.Empty), CancellationToken.None));

        // Assert
        Assert.Contains("CSV is empty", ex.Message);
        _transactionRepository.Verify(r => r.SubmitTransactionsAsync(It.IsAny<IReadOnlyCollection<TransactionModel>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task ParseAndSubmitTransactionCsvAsync_InvalidDate_ThrowsInvalidDataException()
    {
        // Arrange
        var csv = """
            TransactionTime,Amount,Description,TransactionId
            2026-03-01,10.50,Payment,11111111-1111-1111-1111-111111111111
            """;

        // Act
        var ex = await ExpectThrowsAsync<InvalidDataException>(
            () => _sut.ParseAndSubmitTransactionCsvAsync(ToStream(csv), CancellationToken.None));

        // Assert
        Assert.Contains("'TransactionTime' must match configured format", ex.Message);
        _transactionRepository.Verify(r => r.SubmitTransactionsAsync(It.IsAny<IReadOnlyCollection<TransactionModel>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task ParseAndSubmitTransactionCsvAsync_InvalidAmountScale_ThrowsInvalidDataException()
    {
        // Arrange
        var csv = """
            TransactionTime,Amount,Description,TransactionId
            01/03/2026,10,Payment,11111111-1111-1111-1111-111111111111
            """;

        // Act
        var ex = await ExpectThrowsAsync<InvalidDataException>(
            () => _sut.ParseAndSubmitTransactionCsvAsync(ToStream(csv), CancellationToken.None));

        // Assert
        Assert.Contains("'Amount' must have exactly 2 decimal places", ex.Message);
        _transactionRepository.Verify(r => r.SubmitTransactionsAsync(It.IsAny<IReadOnlyCollection<TransactionModel>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task ParseAndSubmitTransactionCsvAsync_InvalidHeaderOrder_ThrowsInvalidDataException()
    {
        // Arrange
        var csv = """
            Amount,TransactionTime,Description,TransactionId
            10.50,01/03/2026,Payment,11111111-1111-1111-1111-111111111111
            """;

        // Act
        var ex = await ExpectThrowsAsync<InvalidDataException>(
            () => _sut.ParseAndSubmitTransactionCsvAsync(ToStream(csv), CancellationToken.None));

        // Assert
        Assert.Contains("Invalid CSV header", ex.Message);
        _transactionRepository.Verify(r => r.SubmitTransactionsAsync(It.IsAny<IReadOnlyCollection<TransactionModel>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task ParseAndSubmitTransactionCsvAsync_InvalidTransactionId_ThrowsInvalidDataException()
    {
        // Arrange
        var csv = """
            TransactionTime,Amount,Description,TransactionId
            01/03/2026,10.50,Payment,not-a-guid
            """;

        // Act
        var ex = await ExpectThrowsAsync<InvalidDataException>(
            () => _sut.ParseAndSubmitTransactionCsvAsync(ToStream(csv), CancellationToken.None));

        // Assert
        Assert.Contains("'TransactionId' must be a valid GUID", ex.Message);
        _transactionRepository.Verify(r => r.SubmitTransactionsAsync(It.IsAny<IReadOnlyCollection<TransactionModel>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task ParseAndSubmitTransactionCsvAsync_EmptyDescription_ThrowsInvalidDataException()
    {
        // Arrange
        var csv = """
            TransactionTime,Amount,Description,TransactionId
            01/03/2026,10.50, ,11111111-1111-1111-1111-111111111111
            """;

        // Act
        var ex = await ExpectThrowsAsync<InvalidDataException>(
            () => _sut.ParseAndSubmitTransactionCsvAsync(ToStream(csv), CancellationToken.None));

        // Assert
        Assert.Contains("'Description' is required", ex.Message);
        _transactionRepository.Verify(r => r.SubmitTransactionsAsync(It.IsAny<IReadOnlyCollection<TransactionModel>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task ParseAndSubmitTransactionCsvAsync_InvalidColumnCount_ThrowsInvalidDataException()
    {
        // Arrange
        var csv = """
            TransactionTime,Amount,Description,TransactionId
            01/03/2026,10.50,Payment
            """;

        // Act
        var ex = await ExpectThrowsAsync<InvalidDataException>(
            () => _sut.ParseAndSubmitTransactionCsvAsync(ToStream(csv), CancellationToken.None));

        // Assert
        Assert.Contains("expected 4 columns but found 3", ex.Message);
        _transactionRepository.Verify(r => r.SubmitTransactionsAsync(It.IsAny<IReadOnlyCollection<TransactionModel>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task ParseAndSubmitTransactionCsvAsync_DelimiterMismatch_ThrowsClearMessage()
    {
        // Arrange
        var csv = """
            TransactionTime;Amount;Description;TransactionId
            01/03/2026;10.50;Payment;11111111-1111-1111-1111-111111111111
            """;

        // Act
        var ex = await ExpectThrowsAsync<InvalidDataException>(
            () => _sut.ParseAndSubmitTransactionCsvAsync(ToStream(csv), CancellationToken.None));

        // Assert
        Assert.Contains("CSV delimiter mismatch", ex.Message);
        Assert.Contains("','", ex.Message);
        Assert.Contains("';'", ex.Message);
        _transactionRepository.Verify(r => r.SubmitTransactionsAsync(It.IsAny<IReadOnlyCollection<TransactionModel>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task GetTransactionsAsync_InvalidPageNumber_ThrowsArgumentOutOfRangeException()
    {
        // Act
        _ = await ExpectThrowsAsync<ArgumentOutOfRangeException>(
            () => _sut.GetTransactionsAsync(0, 10, CancellationToken.None));

        // Assert
        _transactionRepository.Verify(r => r.GetTransactionsAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task GetTransactionsAsync_InvalidPageSize_ThrowsArgumentOutOfRangeException()
    {
        // Act
        _ = await ExpectThrowsAsync<ArgumentOutOfRangeException>(
            () => _sut.GetTransactionsAsync(1, 0, CancellationToken.None));

        // Assert
        _transactionRepository.Verify(r => r.GetTransactionsAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task UpdateTransactionAsync_EmptyDescription_ThrowsArgumentException()
    {
        // Act
        _ = await ExpectThrowsAsync<ArgumentException>(
            () => _sut.UpdateTransactionAsync(
                new TransactionModel
                {
                    TransactionId = Guid.NewGuid(),
                    TransactionAmount = 12.34m,
                    TransactionTime = DateTime.UtcNow,
                    Description = " "
                },
                CancellationToken.None));
        _transactionRepository.Verify(r => r.UpdateTransactionAsync(It.IsAny<TransactionModel>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static async Task<TException> ExpectThrowsAsync<TException>(Func<Task> action)
        where TException : Exception
    {
        try
        {
            await action();
        }
        catch (TException ex)
        {
            return ex;
        }

        throw new AssertFailedException($"Expected exception of type {typeof(TException).Name}.");
    }

    private static Stream ToStream(string content)
        => new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
}
