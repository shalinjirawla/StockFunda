export interface QuarterlyRecord {
  periodKey: string;
  period: string;
  periodEndDate?: string;
  sales?: number | null;
  expenses?: number | null;
  operatingProfit?: number | null;
  opmPercentage?: number | null;
  otherIncome?: number | null;
  interest?: number | null;
  depreciation?: number | null;
  profitBeforeTax?: number | null;
  tax?: number | null;
  taxPercentage?: number | null;
  netProfit?: number | null;
  eps?: number | null;
  consolidationType?: string;
}

export interface QuarterlyGrowth {
  salesGrowthPercent?: number | null;
  operatingProfitGrowthPercent?: number | null;
  netProfitGrowthPercent?: number | null;
  epsGrowthPercent?: number | null;
  depreciationGrowthPercent?: number | null;
  interestGrowthPercent?: number | null;
  taxGrowthPercent?: number | null;
}

export interface StockQuarterlyResultsResponse {
  stockId: number;
  symbol: string;
  exchange: string;
  companyName: string;
  latestQuarter: string;
  source: string;
  lastSyncedAt: string;
  summary: QuarterlyRecord;
  qoQGrowth: QuarterlyGrowth;
  yoYGrowth: QuarterlyGrowth;
  history: QuarterlyRecord[];
}
