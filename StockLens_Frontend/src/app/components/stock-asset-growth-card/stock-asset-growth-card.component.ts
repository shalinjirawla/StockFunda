import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { BalanceSheetResponseDto } from '../../services/stock-balancesheet.service';
import { LoadingState } from '../../models/stock-news.model';

export interface AssetPeriodMetric {
  period: string;
  value: number | null;
  formattedValue: string;
}

@Component({
  selector: 'app-stock-asset-growth-card',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './stock-asset-growth-card.component.html',
  styleUrl: './stock-asset-growth-card.component.css'
})
export class StockAssetGrowthCardComponent {
  @Input() balanceSheet: BalanceSheetResponseDto | null = null;
  @Input() loadingState: LoadingState = 'idle';
  @Input() errorMessage: string = '';
  @Input() isRefreshing: boolean = false;

  @Output() refreshRequested = new EventEmitter<void>();

  onRefresh(): void {
    if (!this.isRefreshing && this.loadingState !== 'loading') {
      this.refreshRequested.emit();
    }
  }

  get totalAssetsData() {
    if (!this.balanceSheet || !this.balanceSheet.periods || this.balanceSheet.periods.length === 0) {
      return null;
    }

    const periods = this.balanceSheet.periods;
    const totalAssetsItem = this.balanceSheet.lineItems?.find(
      item => item.name?.toLowerCase().includes('total assets') || item.isTotal
    );

    const values = totalAssetsItem ? totalAssetsItem.values : [];

    const metrics: AssetPeriodMetric[] = periods.map((period, index) => {
      const val = values[index] ?? null;
      return {
        period,
        value: val,
        formattedValue: this.formatCurrency(val)
      };
    });

    let growthPercent = this.balanceSheet.assetGrowthPercentage ?? 0;
    const n = periods.length;
    if (n >= 2 && (!growthPercent || isNaN(growthPercent))) {
      const prev = values[n - 2] ?? 0;
      const latest = values[n - 1] ?? 0;
      if (prev !== 0) {
        growthPercent = ((latest - prev) / Math.abs(prev)) * 100;
      }
    }

    const startPeriod = periods[0];
    const endPeriod = periods[periods.length - 1];
    const growthRangeText = periods.length >= 2
      ? `YoY Growth (${startPeriod} → ${endPeriod})`
      : 'YoY Growth';

    return {
      metrics,
      growthPercent,
      isPositive: growthPercent >= 0,
      growthRangeText
    };
  }

  formatCurrency(value: number | null): string {
    if (value === null || value === undefined || isNaN(value)) {
      return '₹ 0 Cr';
    }
    const formatted = Math.round(value).toLocaleString('en-IN');
    return `₹ ${formatted} Cr`;
  }
}
