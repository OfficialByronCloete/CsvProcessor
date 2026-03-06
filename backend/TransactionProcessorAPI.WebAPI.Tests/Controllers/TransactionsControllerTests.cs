using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using TransactionProcessor.Common.Models;
using TransactionProcessor.Core.Contracts;
using TransactionProcessorAPI.WebAPI.Controllers;

namespace TransactionProcessorAPI.WebAPI.Tests.Controllers;

[TestClass]
public class TransactionsControllerTests
{
    private Mock<ITransactionService> _transactionService = null!;
    private TransactionsController _sut = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        _transactionService = new Mock<ITransactionService>();
        _sut = new TransactionsController(_transactionService.Object);
    }

    [TestMethod]
    public async Task GetTransactions_ReturnsOkWithPagedResult()
    {
        // Arrange
        var expected = new PagedResult<TransactionModel>
        {
            Data = [],
            PageNumber = 2,
            PageSize = 5,
            TotalCount = 0
        };
        _transactionService
            .Setup(s => s.GetTransactionsAsync(2, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        var result = await _sut.GetTransactions(2, 5, CancellationToken.None);

        // Assert
        var ok = result as OkObjectResult;
        Assert.IsNotNull(ok);
        Assert.AreSame(expected, ok.Value);
        _transactionService.Verify(s => s.GetTransactionsAsync(2, 5, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task UploadCsv_NoFile_ReturnsBadRequest()
    {
        // Act
        var result = await _sut.UploadCsv(null!, CancellationToken.None);

        // Assert
        var badRequest = result as BadRequestObjectResult;
        Assert.IsNotNull(badRequest);
        Assert.AreEqual("No file provided.", badRequest.Value);
    }

    [TestMethod]
    public async Task UploadCsv_InvalidExtension_ReturnsBadRequest()
    {
        // Arrange
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("test"));
        IFormFile file = new FormFile(stream, 0, stream.Length, "file", "transactions.txt");

        // Act
        var result = await _sut.UploadCsv(file, CancellationToken.None);

        // Assert
        var badRequest = result as BadRequestObjectResult;
        Assert.IsNotNull(badRequest);
        Assert.AreEqual("Invalid file type. Only CSV files are allowed.", badRequest.Value);
        _transactionService.Verify(s => s.ParseAndSubmitTransactionCsvAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task UploadCsv_ValidFile_CallsServiceAndReturnsOk()
    {
        // Arrange
        const string csv = "TransactionTime,Amount,Description,TransactionId\n01/03/2026,10.50,Payment,11111111-1111-1111-1111-111111111111";
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(csv));
        IFormFile file = new FormFile(stream, 0, stream.Length, "file", "transactions.csv");

        // Act
        var result = await _sut.UploadCsv(file, CancellationToken.None);

        // Assert
        var ok = result as OkObjectResult;
        Assert.IsNotNull(ok);
        _transactionService.Verify(s => s.ParseAndSubmitTransactionCsvAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task UpdateTransaction_RouteIdMismatch_ReturnsBadRequest()
    {
        // Arrange
        var routeId = Guid.NewGuid();
        var payload = new TransactionModel
        {
            TransactionId = Guid.NewGuid(),
            TransactionTime = DateTime.UtcNow,
            TransactionAmount = 15.30m,
            Description = "Payment"
        };

        // Act
        var result = await _sut.UpdateTransaction(routeId, payload, CancellationToken.None);

        // Assert
        var badRequest = result as BadRequestObjectResult;
        Assert.IsNotNull(badRequest);
        _transactionService.Verify(s => s.UpdateTransactionAsync(It.IsAny<TransactionModel>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task UpdateTransaction_NotFound_ReturnsNotFound()
    {
        // Arrange
        _transactionService
            .Setup(s => s.UpdateTransactionAsync(It.IsAny<TransactionModel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var payload = new TransactionModel
        {
            TransactionId = Guid.NewGuid(),
            TransactionTime = DateTime.UtcNow,
            TransactionAmount = 15.30m,
            Description = "Payment"
        };

        // Act
        var result = await _sut.UpdateTransaction(payload.TransactionId, payload, CancellationToken.None);

        // Assert
        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
        _transactionService.Verify(s => s.UpdateTransactionAsync(payload, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task UpdateTransaction_Success_ReturnsNoContent()
    {
        // Arrange
        _transactionService
            .Setup(s => s.UpdateTransactionAsync(It.IsAny<TransactionModel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var payload = new TransactionModel
        {
            TransactionId = Guid.NewGuid(),
            TransactionTime = DateTime.UtcNow,
            TransactionAmount = 15.30m,
            Description = "Payment"
        };

        // Act
        var result = await _sut.UpdateTransaction(payload.TransactionId, payload, CancellationToken.None);

        // Assert
        Assert.IsInstanceOfType<NoContentResult>(result);
        _transactionService.Verify(s => s.UpdateTransactionAsync(payload, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task DeleteTransaction_NotFound_ReturnsNotFound()
    {
        // Arrange
        _transactionService
            .Setup(s => s.DeleteTransactionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var transactionId = Guid.NewGuid();

        // Act
        var result = await _sut.DeleteTransaction(transactionId, CancellationToken.None);

        // Assert
        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
        _transactionService.Verify(s => s.DeleteTransactionAsync(transactionId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task DeleteTransaction_Success_ReturnsNoContent()
    {
        // Arrange
        _transactionService
            .Setup(s => s.DeleteTransactionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var transactionId = Guid.NewGuid();

        // Act
        var result = await _sut.DeleteTransaction(transactionId, CancellationToken.None);

        // Assert
        Assert.IsInstanceOfType<NoContentResult>(result);
        _transactionService.Verify(s => s.DeleteTransactionAsync(transactionId, It.IsAny<CancellationToken>()), Times.Once);
    }
}
