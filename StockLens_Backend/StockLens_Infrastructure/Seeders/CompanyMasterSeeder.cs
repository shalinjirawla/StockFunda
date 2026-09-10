using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StockLens_DataLayer.Entities;
using StockLens_Infrastructure.DataContext;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace StockLens_Infrastructure.Seeders
{
    public class CompanyJsonDto
    {
        [JsonPropertyName("SYMBOL")]
        public string? Symbol { get; set; }

        [JsonPropertyName("NAME OF COMPANY")]
        public string? CompanyName { get; set; }
    }
    public class CompanyMasterSeeder
    {
        private readonly StockLensDataContext _dbContext;
        private readonly ILogger<CompanyMasterSeeder> _logger;
        public CompanyMasterSeeder(
            StockLensDataContext dbContext,
            ILogger<CompanyMasterSeeder> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        public async Task SeedAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("CompanyMasterSeeder is starting...");

            try
            {
                var filePath = Path.Combine(AppContext.BaseDirectory, "Seeders", "Data", "companies.json");
                if (!File.Exists(filePath))
                {
                    _logger.LogWarning("Seeder JSON file not found at path: {FilePath}", filePath);
                    return;
                }

                _logger.LogInformation("Reading NSE Stocks JSON from {FilePath}", filePath);
                var jsonText = await File.ReadAllTextAsync(filePath, cancellationToken);
                var jsonCompanies = JsonSerializer.Deserialize<List<CompanyJsonDto>>(jsonText);

                if (jsonCompanies == null || jsonCompanies.Count == 0)
                {
                    _logger.LogWarning("No companies found in the JSON file.");
                    return;
                }

                var newCompaniesToAdd = new List<Company>();

                var existingSymbols = await _dbContext.CompanyMaster
                    .Select(c => c.Symbol)
                    .ToListAsync(cancellationToken);

                var existingSet = new HashSet<string>(existingSymbols, StringComparer.OrdinalIgnoreCase);

                foreach (var item in jsonCompanies)
                {
                    if (string.IsNullOrWhiteSpace(item.Symbol) || string.IsNullOrWhiteSpace(item.CompanyName))
                        continue;

                    var symbol = item.Symbol.Trim();
                    var companyName = item.CompanyName.Trim();

                    if (!existingSet.Contains(symbol))
                    {
                        newCompaniesToAdd.Add(new Company
                        {
                            Symbol = symbol,
                            CompanyName = companyName,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        });
                        
                        existingSet.Add(symbol);
                    }
                }

                if (newCompaniesToAdd.Count > 0)
                {
                    _logger.LogInformation("Adding {Count} new Companies to the database...", newCompaniesToAdd.Count);
                    await _dbContext.CompanyMaster.AddRangeAsync(newCompaniesToAdd, cancellationToken);
                    await _dbContext.SaveChangesAsync(cancellationToken);
                    _logger.LogInformation("CompanyMasterSeeder completed successfully.");
                }
                else
                {
                    _logger.LogInformation("No new companies found. Database is already up to date.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while seeding CompanyMaster.");
            }
        }
    }
}
