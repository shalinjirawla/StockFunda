using System;
using System.Collections.Generic;

namespace StockLens_BusinessLayer.DTOs
{
    public class CategoryScoreDto
    {
        public string CategoryName { get; set; } = string.Empty;
        public int EarnedPoints { get; set; }
        public int MaxPoints { get; set; }
        public double Percentage => MaxPoints > 0 ? Math.Round((double)EarnedPoints / MaxPoints * 100, 1) : 0;
        public string Status { get; set; } = "Good"; // "Excellent", "Good", "Average", "Poor"
        public List<string> Highlights { get; set; } = new();
    }

    public class StockHealthScoreDto
    {
        public string Symbol { get; set; } = string.Empty;
        public string Exchange { get; set; } = "NSE";
        public string CompanyName { get; set; } = string.Empty;
        
        /// <summary>
        /// Total composite score out of 100.
        /// </summary>
        public int TotalScore { get; set; }

        /// <summary>
        /// Signal verdict: "Strong Buy", "Good for Buy", "Neutral / Hold", "Risky / Avoid"
        /// </summary>
        public string Signal { get; set; } = "Neutral / Hold";

        /// <summary>
        /// UI CSS Badge class: "badge-success", "badge-good", "badge-warning", "badge-danger"
        /// </summary>
        public string SignalClass { get; set; } = "badge-warning";

        public string SummaryText { get; set; } = string.Empty;

        // Sub-breakdowns
        public CategoryScoreDto ProfitabilityScore { get; set; } = new();
        public CategoryScoreDto ValuationScore { get; set; } = new();
        public CategoryScoreDto SolvencyScore { get; set; } = new();
        public CategoryScoreDto GrowthScore { get; set; } = new();
        public CategoryScoreDto SmartMoneyScore { get; set; } = new();
        public CategoryScoreDto EfficiencyScore { get; set; } = new();

        public List<string> Pros { get; set; } = new();
        public List<string> Cons { get; set; } = new();
        public List<string> RedFlags { get; set; } = new();

        public DateTime EvaluatedAt { get; set; } = DateTime.UtcNow;
    }
}
