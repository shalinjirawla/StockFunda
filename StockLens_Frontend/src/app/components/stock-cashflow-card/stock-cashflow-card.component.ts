import { Component, Input, Output, EventEmitter, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { StockCashflowResponse, AnnualCashflowItem, LoadingState } from '../../models/stock-cashflow.model';
import { TimeAgoPipe } from '../../pipes/time-ago.pipe';

@Component({
  selector: 'app-stock-cashflow-card',
  standalone: true,
  imports: [CommonModule, TimeAgoPipe],
  templateUrl: './stock-cashflow-card.component.html',
  styleUrl: './stock-cashflow-card.component.css'
})
export class StockCashflowCardComponent {
  @Input() cashflow: StockCashflowResponse | null = null;
  @Input() loadingState: LoadingState = 'idle';
  @Input() errorMessage: string = '';
  @Input() isRefreshing: boolean = false;

  @Output() refreshRequested = new EventEmitter<void>();

  selectedHistoricalPeriod = signal<AnnualCashflowItem | null>(null);

  /**
   * Returns the actively displayed annual period (either user-selected historical year or latest).
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
    netProfit?: number | null;
    eps?: number | null;
    otherEquity?: number | null;
    cfoToNetProfitRatio?: number | null;
    fcfMarginPercent?: number | null;
    capexToCfoPercent?: number | null;
    consolidationType?: string | null;
  } | null {
    const selected = this.selectedHistoricalPeriod();
    if (selected) {
      return selected;
    }
    if (this.cashflow?.summary) {
      return {
        ...this.cashflow.summary,
        dataAsOf: this.cashflow.dataAsOf
      };
    }
    return null;
  }

  selectPeriod(item: AnnualCashflowItem | null): void {
    this.selectedHistoricalPeriod.set(item);
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

    // Format with Indian numbering system (Crores)
    const formatted = new Intl.NumberFormat('en-IN', {
      maximumFractionDigits: 2,
      minimumFractionDigits: 0
    }).format(absVal);

    return `${isNegative ? '-' : ''}₹${formatted} Cr`;
  }

  formatEps(value: number | null | undefined): string {
    if (value === null || value === undefined) return '—';
    return `₹${value.toFixed(2)}`;
  }

  formatPercent(value: number | null | undefined): string {
    if (value === null || value === undefined) return '—';
    const sign = value > 0 ? '+' : '';
    return `${sign}${value.toFixed(2)}%`;
  }

  formatRatio(value: number | null | undefined): string {
    if (value === null || value === undefined) return '—';
    return `${value.toFixed(2)}x`;
  }

  getChangeClass(value: number | null | undefined): string {
    if (value === null || value === undefined) return 'neutral';
    if (value > 0) return 'positive';
    if (value < 0) return 'negative';
    return 'neutral';
  }

  getArrow(value: number | null | undefined): string {
    if (value === null || value === undefined) return '';
    if (value > 0) return '▲';
    if (value < 0) return '▼';
    return '—';
  }

  getBarHeightPercentage(value: number | null | undefined, maxReference: number): number {
    if (!value || maxReference <= 0) return 4;
    const pct = Math.round((Math.abs(value) / maxReference) * 100);
    return Math.min(100, Math.max(6, pct));
  }

  getMaxOperatingCashFlow(): number {
    if (!this.cashflow?.history || this.cashflow.history.length === 0) return 1;
    let max = 1;
    for (const h of this.cashflow.history) {
      if (h.operatingCashFlow && h.operatingCashFlow > max) {
        max = h.operatingCashFlow;
      }
      if (h.revenue && h.revenue > max) {
        max = Math.max(max, h.operatingCashFlow || 1);
      }
    }
    return max;
  }
}
