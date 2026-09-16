import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { StockQuarterlyResultsResponse, QuarterlyRecord } from '../../models/stock-quarterly-results.model';
import { LoadingState } from '../../models/stock-news.model';

@Component({
  selector: 'app-stock-quarters-card',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './stock-quarters-card.component.html',
  styleUrl: './stock-quarters-card.component.css'
})
export class StockQuartersCardComponent {
  @Input() quarters: StockQuarterlyResultsResponse | null = null;
  @Input() loadingState: LoadingState = 'idle';
  @Input() errorMessage: string = '';
  @Input() isRefreshing: boolean = false;

  @Output() refreshRequested = new EventEmitter<void>();

  selectedQuarter: QuarterlyRecord | null = null;

  get activeQuarter(): QuarterlyRecord | null {
    if (this.selectedQuarter) return this.selectedQuarter;
    return this.quarters?.summary || this.quarters?.history?.[0] || null;
  }

  get displayedHistory(): QuarterlyRecord[] {
    if (!this.quarters?.history) return [];

    // Deduplicate history by normalized period date / label
    const seen = new Set<string>();
    const uniqueRecords: QuarterlyRecord[] = [];

    for (const record of this.quarters.history) {
      const key = record.periodEndDate
        ? new Date(record.periodEndDate).toISOString().slice(0, 7) // 'yyyy-MM'
        : (record.period || record.periodKey || '').trim().toUpperCase();

      if (key && !seen.has(key)) {
        seen.add(key);
        uniqueRecords.push(record);
      }
    }

    // Show in chronological order for tables (oldest to newest left to right, exactly last 4 quarters)
    const reversed = [...uniqueRecords].reverse();
    return reversed.slice(-4);
  }

  selectQuarter(q: QuarterlyRecord | null): void {
    this.selectedQuarter = q;
  }

  onRefreshClick(): void {
    this.refreshRequested.emit();
  }

  formatCurrency(value?: number | null): string {
    if (value === null || value === undefined || isNaN(value)) return '—';
    return '₹' + Number(value).toLocaleString('en-IN', {
      maximumFractionDigits: 2,
      minimumFractionDigits: 0
    }) + ' Cr';
  }

  formatNumber(value?: number | null, decimals = 2): string {
    if (value === null || value === undefined || isNaN(value)) return '—';
    return Number(value).toLocaleString('en-IN', {
      maximumFractionDigits: decimals,
      minimumFractionDigits: decimals
    });
  }

  formatPercent(value?: number | null): string {
    if (value === null || value === undefined || isNaN(value)) return '—';
    const sign = value > 0 ? '+' : '';
    return `${sign}${Number(value).toFixed(2)}%`;
  }

  formatEps(value?: number | null): string {
    if (value === null || value === undefined || isNaN(value)) return '—';
    return '₹' + Number(value).toFixed(2);
  }
}
