import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { StockCashflowResponse, StockRatios, LoadingState } from '../../models/stock-cashflow.model';

@Component({
  selector: 'app-stock-cashflow-card',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './stock-cashflow-card.component.html',
  styleUrl: './stock-cashflow-card.component.css'
})
export class StockCashflowCardComponent {
  @Input() cashflow: StockCashflowResponse | null = null;
  @Input() loadingState: LoadingState = 'idle';
  @Input() errorMessage: string = '';
  @Input() isRefreshing: boolean = false;

  @Output() refreshRequested = new EventEmitter<void>();

  /**
   * Returns the actively displayed annual financial summary.
   */
  get activePeriod(): {
    fiscalYear: string;
    periodEndDate?: string | null;
    dataAsOf?: string | null;
    operatingCashFlow?: number | null;
    capex?: number | null;
    freeCashFlow?: number | null;
    netCashFlow?: number | null;
    revenue?: number | null;
    operatingProfit?: number | null;
    netProfit?: number | null;
    eps?: number | null;
    otherEquity?: number | null;
    totalEquity?: number | null;
    cfoToOperatingProfitRatio?: number | null;
    cfoToNetProfitRatio?: number | null;
    fcfMarginPercent?: number | null;
    capexToCfoPercent?: number | null;
    consolidationType?: string | null;
    ratios?: StockRatios | null;
  } | null {
    if (this.cashflow?.summary) {
      return {
        ...this.cashflow.summary,
        dataAsOf: this.cashflow.dataAsOf,
        ratios: this.cashflow.summary.ratios || this.cashflow.ratios
      };
    }
    return null;
  }

  /**
   * Returns the valuation and profitability ratios.
   */
  get ratios(): StockRatios | null {
    return this.cashflow?.summary?.ratios || this.cashflow?.ratios || null;
  }

  onRefreshClick(): void {
    if (!this.isRefreshing) {
      this.refreshRequested.emit();
    }
  }

  formatCurrency(value: number | null | undefined): string {
    if (value === null || value === undefined) return '—';

    const isNegative = value < 0;
    const absVal = Math.abs(value);

    // If value is raw rupees (e.g. 691970000000), convert to Crores (691970000000 / 10,000,000 = 69,197 Cr)
    // 1 Crore = 10,000,000 (10^7)
    const valInCrores = absVal >= 10_000_000 ? absVal / 10_000_000 : absVal;

    // Format with Indian numbering system (Crores)
    const formatted = new Intl.NumberFormat('en-IN', {
      maximumFractionDigits: 2,
      minimumFractionDigits: 0
    }).format(valInCrores);

    return `${isNegative ? '-' : ''}₹${formatted} Cr`;
  }

  formatPrice(value: number | null | undefined): string {
    if (value === null || value === undefined) return '—';
    return new Intl.NumberFormat('en-IN', {
      style: 'currency',
      currency: 'INR',
      maximumFractionDigits: 2,
      minimumFractionDigits: 2
    }).format(value);
  }

  formatPercent(value: number | null | undefined): string {
    if (value === null || value === undefined) return '—';
    const sign = value > 0 ? '+' : '';
    return `${sign}${value.toFixed(2)}%`;
  }

  formatPercentage(value: number | null | undefined): string {
    if (value === null || value === undefined) return '—';
    return `${value.toFixed(2)}%`;
  }

  formatRatio(value: number | null | undefined): string {
    if (value === null || value === undefined) return '—';
    return `${value.toFixed(2)}x`;
  }

  formatCfoToOp(value: number | null | undefined): string {
    if (value === null || value === undefined) return '—';
    const pct = value > 5 ? value : value * 100;
    return `${Math.round(pct)}%`;
  }

  getChangeClass(value: number | null | undefined): string {
    if (value === null || value === undefined) return 'neutral';
    if (value > 0) return 'positive';
    if (value < 0) return 'negative';
    return 'neutral';
  }

  getValueColorClass(value: number | null | undefined): string {
    if (value === null || value === undefined || isNaN(value)) return 'neutral';
    return value < 0 ? 'negative' : 'positive';
  }

  getArrow(value: number | null | undefined): string {
    if (value === null || value === undefined) return '';
    if (value > 0) return '▲';
    if (value < 0) return '▼';
    return '—';
  }

  getBarHeightPercentage(value: number | null | undefined, maxReference: number): number {
    if (!value || maxReference <= 0) return 4;
    const absVal = Math.abs(value);
    const valInCrores = absVal >= 10_000_000 ? absVal / 10_000_000 : absVal;
    const maxInCrores = maxReference >= 10_000_000 ? maxReference / 10_000_000 : maxReference;
    const pct = Math.round((valInCrores / maxInCrores) * 100);
    return Math.min(100, Math.max(6, pct));
  }

  getMaxOperatingCashFlow(): number {
    if (!this.cashflow?.summary) return 1;
    let max = 1;
    const cfo = this.cashflow.summary.operatingCashFlow;
    if (cfo) {
      const absVal = Math.abs(cfo);
      const inCr = absVal >= 10_000_000 ? absVal / 10_000_000 : absVal;
      if (inCr > max) {
        max = inCr;
      }
    }
    const rev = this.cashflow.summary.revenue;
    if (rev) {
      const absVal = Math.abs(rev);
      const inCr = absVal >= 10_000_000 ? absVal / 10_000_000 : absVal;
      if (inCr > max) {
        max = inCr;
      }
    }
    return max;
  }

  getRatioProgressPercentage(ratio: number | null | undefined): number {
    if (!ratio || ratio <= 0) return 4;
    const normalized = ratio > 5 ? ratio / 100 : ratio;
    const pct = Math.round((normalized / 1.5) * 100);
    return Math.min(100, Math.max(8, pct));
  }

  getRoeProgress(roe: number | null | undefined): number {
    if (!roe || roe <= 0) return 4;
    const pct = Math.round((roe / 25.0) * 100);
    return Math.min(100, Math.max(8, pct));
  }

  getRoceProgress(roce: number | null | undefined): number {
    if (!roce || roce <= 0) return 4;
    const pct = Math.round((roce / 30.0) * 100);
    return Math.min(100, Math.max(8, pct));
  }

  getPeProgress(pe: number | null | undefined): number {
    if (!pe || pe <= 0) return 4;
    const pct = Math.round((pe / 50.0) * 100);
    return Math.min(100, Math.max(8, pct));
  }

  get52WeekProgress(price: number | null | undefined, low: number | null | undefined, high: number | null | undefined): number {
    if (!low || !high || high <= low) return 50;
    const current = price ?? ((low + high) / 2);
    const range = high - low;
    const pos = ((current - low) / range) * 100;
    return Math.min(100, Math.max(4, Math.round(pos)));
  }

  getFaceValueProgress(fv: number | null | undefined): number {
    if (!fv || fv <= 0) return 10;
    const pct = Math.round((fv / 10.0) * 100);
    return Math.min(100, Math.max(10, pct));
  }

  getTotalEquityProgress(te: number | null | undefined): number {
    if (!te || te <= 0) return 60;
    const absVal = Math.abs(te);
    const inCr = absVal >= 10_000_000 ? absVal / 10_000_000 : absVal;
    const pct = Math.round((inCr / 500_000) * 100);
    return Math.min(100, Math.max(15, pct));
  }

  getBookValueProgress(bv: number | null | undefined): number {
    if (!bv || bv <= 0) return 20;
    const pct = Math.round((bv / 1500.0) * 100);
    return Math.min(100, Math.max(12, pct));
  }

  getMarketCapProgress(mc: number | null | undefined): number {
    if (!mc || mc <= 0) return 30;
    const absVal = Math.abs(mc);
    const inCr = absVal >= 10_000_000 ? absVal / 10_000_000 : absVal;
    const pct = Math.round((inCr / 2_000_000) * 100);
    return Math.min(100, Math.max(15, pct));
  }

  getSectorPeProgress(pe: number | null | undefined): number {
    if (!pe || pe <= 0) return 40;
    const pct = Math.round((pe / 50.0) * 100);
    return Math.min(100, Math.max(10, pct));
  }
}


