using Microsoft.EntityFrameworkCore;
using TransactionProcessorAPI.WebAPI.Infrastructure;
using TransactionProcessor.Core.Contracts;
using TransactionProcessor.Integrations.DataAccess.Contexts;
using TransactionProcessor.Integrations.DataAccess.Extensions;
using TransactionProcessor.Integrations.DataAccess.Repositories;
using TransactionProcessor.Services.Options;
using TransactionProcessor.Services.Services;

var builder = WebApplication.CreateBuilder(args);
const string FrontendCorsPolicy = "FrontendCorsPolicy";

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

// Swagger (Swashbuckle)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.Configure<CsvOptions>(builder.Configuration.GetSection(CsvOptions.SectionName));
builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy =>
    {
        policy.WithOrigins("http://localhost:9000")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// DbContext (SQLite)
builder.Services.AddDbContext<TransactionProcessorContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("TransactionProcessor");
    options.UseSqlite(connectionString);
});

builder.Services.AddScoped<ITransactionService, TransactionService>();
builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();

var app = builder.Build();

app.Services.ApplyCsvDataProcessorMigrations();

app.UseExceptionHandler();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();

app.UseCors(FrontendCorsPolicy);

app.UseAuthorization();

app.MapControllers();

app.Run();
