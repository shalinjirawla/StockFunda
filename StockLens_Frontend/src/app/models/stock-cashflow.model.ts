export type LoadingState = 'idle' | 'loading' | 'success' | 'empty' | 'error';

export interface CashflowYoYChange {
  freeCashFlowChange?: number | null;
  freeCashFlowGrowth?: number | null; // YoY %
  operatingCashFlowChange?: number | null;
  operatingCashFlowGrowth?: number | null; // YoY %
  capexChange?: number | null;
  capexGrowth?: number | null; // YoY %
  netProfitChange?: number | null;
  netProfitGrowth?: number | null; // YoY %
  revenueChange?: number | null;
  revenueGrowth?: number | null; // YoY %
  netCashFlowChange?: number | null;
  netCashFlowGrowth?: number | null; // YoY %
}

export interface CashflowSummary {
  fiscalYear: string;
  periodEndDate?: string | null;
  operatingCashFlow?: number | null;
  capex?: number | null;
  freeCashFlow?: number | null;
  netCashFlow?: number | null;
  revenue?: number | null;
  netProfit?: number | null;
  eps?: number | null;
  otherEquity?: number | null;
  cfoToNetProfitRatio?: number | null;
  fcfMarginPercent?: number | null;
  capexToCfoPercent?: number | null;
  consolidationType?: string | null;
  yoYChange: CashflowYoYChange;
}

export interface AnnualCashflowItem {
  id: number;
  fiscalYear: string;
  periodKey?: string;
  periodEndDate?: string | null;
  dataAsOf?: string | null;
  periodType: string;
  revenue?: number | null;
  netProfit?: number | null;
  eps?: number | null;
  netProfitAttributableToMinorityInterest?: number | null;
  otherEquity?: number | null;
  operatingCashFlow?: number | null;
  capex?: number | null;
  freeCashFlow?: number | null;
  netCashFlow?: number | null;
  cfoToNetProfitRatio?: number | null;
  fcfMarginPercent?: number | null;
  capexToCfoPercent?: number | null;
  consolidationType?: string | null;
  source: string;
  lastSyncedAt: string;
}

export interface StockCashflowResponse {
  stockId: number;
  symbol: string;
  exchange: string;
  companyName: string;
  latestFiscalYear: string;
  dataAsOf?: string | null;
  source: string;
  lastSyncedAt: string;
  summary: CashflowSummary;
  history: AnnualCashflowItem[];
}
