using Microsoft.EntityFrameworkCore;
using StockLens_DataLayer.Entities;
using System;

namespace StockLens_Infrastructure.DataContext
{
    public class StockLensDataContext : DbContext
    {
        public StockLensDataContext(DbContextOptions<StockLensDataContext> options)
            : base(options)
        {
        }

        public DbSet<Stock> Stocks => Set<Stock>();
        public DbSet<StockNews> StockNews => Set<StockNews>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Stock configuration
            modelBuilder.Entity<Stock>(entity =>
            {
                entity.ToTable("Stocks");
                entity.HasKey(s => s.Id);

                entity.Property(s => s.Symbol)
                    .HasMaxLength(20)
                    .IsRequired();

                entity.Property(s => s.CompanyName)
                    .HasMaxLength(200)
                    .IsRequired();

                entity.Property(s => s.Exchange)
                    .HasMaxLength(10)
                    .IsRequired();

                entity.Property(s => s.Industry)
                    .HasMaxLength(100);

                entity.HasIndex(s => new { s.Symbol, s.Exchange })
                    .IsUnique()
                    .HasDatabaseName("IX_Stocks_Symbol_Exchange");

                // Seed initial top Indian stocks for quick test/use
                var seedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                entity.HasData(
                    new Stock { Id = 1, Symbol = "RELIANCE", CompanyName = "Reliance Industries Limited", Exchange = "NSE", Industry = "Oil & Gas / Conglomerate", CreatedAt = seedDate, UpdatedAt = seedDate },
                    new Stock { Id = 2, Symbol = "TCS", CompanyName = "Tata Consultancy Services Limited", Exchange = "NSE", Industry = "Information Technology", CreatedAt = seedDate, UpdatedAt = seedDate },
                    new Stock { Id = 3, Symbol = "INFY", CompanyName = "Infosys Limited", Exchange = "NSE", Industry = "Information Technology", CreatedAt = seedDate, UpdatedAt = seedDate },
                    new Stock { Id = 4, Symbol = "TATAMOTORS", CompanyName = "Tata Motors Limited", Exchange = "NSE", Industry = "Automobile", CreatedAt = seedDate, UpdatedAt = seedDate },
                    new Stock { Id = 5, Symbol = "HDFCBANK", CompanyName = "HDFC Bank Limited", Exchange = "NSE", Industry = "Banking / Financial Services", CreatedAt = seedDate, UpdatedAt = seedDate },
                    new Stock { Id = 6, Symbol = "ICICIBANK", CompanyName = "ICICI Bank Limited", Exchange = "NSE", Industry = "Banking / Financial Services", CreatedAt = seedDate, UpdatedAt = seedDate },
                    new Stock { Id = 7, Symbol = "RELIANCE", CompanyName = "Reliance Industries Limited", Exchange = "BSE", Industry = "Oil & Gas / Conglomerate", CreatedAt = seedDate, UpdatedAt = seedDate },
                    new Stock { Id = 8, Symbol = "TCS", CompanyName = "Tata Consultancy Services Limited", Exchange = "BSE", Industry = "Information Technology", CreatedAt = seedDate, UpdatedAt = seedDate }
                );
            });

            // StockNews configuration
            modelBuilder.Entity<StockNews>(entity =>
            {
                entity.ToTable("StockNews");
                entity.HasKey(sn => sn.Id);

                entity.Property(sn => sn.Title)
                    .HasMaxLength(500)
                    .IsRequired();

                entity.Property(sn => sn.Description)
                    .HasColumnType("nvarchar(max)");

                entity.Property(sn => sn.Content)
                    .HasColumnType("nvarchar(max)");

                entity.Property(sn => sn.SourceName)
                    .HasMaxLength(200)
                    .IsRequired();

                entity.Property(sn => sn.SourceUrl)
                    .HasMaxLength(1000)
                    .IsRequired();

                entity.Property(sn => sn.ImageUrl)
                    .HasMaxLength(1000);

                entity.Property(sn => sn.Category)
                    .HasMaxLength(100);

                entity.Property(sn => sn.ExternalNewsId)
                    .HasMaxLength(255);

                // Relationship
                entity.HasOne(sn => sn.Stock)
                    .WithMany(s => s.News)
                    .HasForeignKey(sn => sn.StockId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Composite index for fast sorting and retrieval by stock
                entity.HasIndex(sn => new { sn.StockId, sn.PublishedAt })
                    .HasDatabaseName("IX_StockNews_StockId_PublishedAt");

                // Index on published date
                entity.HasIndex(sn => sn.PublishedAt)
                    .HasDatabaseName("IX_StockNews_PublishedAt");

                // Unique constraint on ExternalNewsId when present
                entity.HasIndex(sn => sn.ExternalNewsId)
                    .IsUnique()
                    .HasFilter("[ExternalNewsId] IS NOT NULL")
                    .HasDatabaseName("IX_StockNews_ExternalNewsId");

                // Unique constraint on (StockId, SourceUrl) to prevent duplicate articles for the same stock
                entity.HasIndex(sn => new { sn.StockId, sn.SourceUrl })
                    .IsUnique()
                    .HasDatabaseName("IX_StockNews_StockId_SourceUrl");
            });
        }
    }
}
