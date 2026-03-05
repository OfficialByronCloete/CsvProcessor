using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TransactionProcessor.Integrations.DataAccess.Contexts;

public sealed class TransactionProcessorContextFactory : IDesignTimeDbContextFactory<TransactionProcessorContext>
{
    public TransactionProcessorContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<TransactionProcessorContext>();

        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__TransactionProcessor")
            ?? "Data Source=transactions.db";

        optionsBuilder.UseSqlite(connectionString);
        return new TransactionProcessorContext(optionsBuilder.Options);
    }
}
