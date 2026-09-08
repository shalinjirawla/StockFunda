using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace StockLens_Infrastructure.ExternalServices.IndianApi.Models
{
    public class IndianApiStockResponse
    {
        [JsonPropertyName("tickerId")]
        [JsonConverter(typeof(StringOrNumberJsonConverter))]
        public string? TickerId { get; set; }

        [JsonPropertyName("companyName")]
        public string? CompanyName { get; set; }

        [JsonPropertyName("recentNews")]
        public List<IndianApiRecentNewsItem>? RecentNews { get; set; }
    }

    public class IndianApiRecentNewsItem
    {
        [JsonPropertyName("id")]
        [JsonConverter(typeof(StringOrNumberJsonConverter))]
        public string? Id { get; set; }

        [JsonPropertyName("headline")]
        public string? Headline { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("link")]
        public string? Link { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("summary")]
        public string? Summary { get; set; }

        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [JsonPropertyName("sourceName")]
        public string? SourceName { get; set; }

        [JsonPropertyName("imageUrl")]
        public string? ImageUrl { get; set; }

        [JsonPropertyName("image_url")]
        public string? Image_Url { get; set; }

        [JsonPropertyName("image")]
        public string? Image { get; set; }

        [JsonPropertyName("date")]
        public string? Date { get; set; }

        [JsonPropertyName("publishedAt")]
        public string? PublishedAt { get; set; }

        [JsonPropertyName("pub_date")]
        public string? PubDate { get; set; }

        [JsonPropertyName("category")]
        public string? Category { get; set; }

        [JsonPropertyName("topics")]
        public List<string>? Topics { get; set; }
    }

    /// <summary>
    /// Standardized IndianAPI News Article returned by IndianApiNewsClient
    /// </summary>
    public class IndianApiStandardArticle
    {
        public string? ExternalNewsId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Content { get; set; }
        public string SourceName { get; set; } = string.Empty;
        public string SourceUrl { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public DateTime PublishedAt { get; set; }
        public string? Category { get; set; }
    }
}
