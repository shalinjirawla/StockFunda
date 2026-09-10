export interface ShareholdingPeriod {
  period: string;
  periodKey: string;
  periodDate?: string;
  periodType?: string;
  promoter?: number | null;
  fii?: number | null;
  dii?: number | null;
  government?: number | null;
  public?: number | null;
  others?: number | null;
  shareholdersCount?: number | null;
  total?: number | null;
  dataAsOf?: string;
}

export interface ShareholdingChange {
  promoter: number | null;
  fii: number | null;
  dii: number | null;
  government?: number | null;
  public: number | null;
  others?: number | null;
}

export interface ShareholdingRelativeChange {
  promoter: number | null;
  fii: number | null;
  dii: number | null;
  government?: number | null;
  public: number | null;
  others?: number | null;
}

export interface ShareholdingValidationSummary {
  isValid: boolean;
  totalPercentage: number;
  notes?: string;
}

export interface StockShareholdingResponse {
  stockId: number;
  symbol: string;
  exchange: string;
  companyName: string;
  currentPeriod: ShareholdingPeriod;
  previousPeriod: ShareholdingPeriod | null;
  change: ShareholdingChange;
  relativeChange: ShareholdingRelativeChange;
  history: ShareholdingPeriod[];
  dataAsOf?: string;
  source: string;
  lastSyncedAt: string;
  validation?: ShareholdingValidationSummary;
}
