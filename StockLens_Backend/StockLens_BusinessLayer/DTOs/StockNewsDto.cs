using System;
using System.Collections.Generic;

namespace StockLens_BusinessLayer.DTOs
{
    public class StockNewsItemDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Content { get; set; }
        public string SourceName { get; set; } = string.Empty;
        public string SourceUrl { get; set; } = string.Empty;
        public string ArticleUrl { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public DateTime PublishedAt { get; set; }
        public string? Category { get; set; }
        public string? ExternalNewsId { get; set; }
    }

    public class StockNewsResponseDto
    {
        public int StockId { get; set; }
        public string Symbol { get; set; } = string.Empty;
        public string Exchange { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public List<StockNewsItemDto> News { get; set; } = new();
        public int Page { get; set; } = 1;
        public int Limit { get; set; } = 20;
        public DateTime? LastFetchedAt { get; set; }
    }
}
