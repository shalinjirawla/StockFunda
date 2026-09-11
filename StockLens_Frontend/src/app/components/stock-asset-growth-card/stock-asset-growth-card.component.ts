import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { BalanceSheetResponseDto } from '../../services/stock-financials.service';
import { LoadingState } from '../../models/stock-news.model';

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

  get gridData() {
    if (!this.balanceSheet || !this.balanceSheet.periods || this.balanceSheet.periods.length === 0) {
      return null;
    }

    const periods = this.balanceSheet.periods;
    const n = periods.length;

    const rows = this.balanceSheet.lineItems.map(item => {
      // Delta is always between the last two periods available
      let deltaPercent = 0;
      let trendText = 'Stable';
      let trendIcon = '➖';

      if (n >= 2) {
        const val1 = item.values[n - 2] ?? 0;
        const val2 = item.values[n - 1] ?? 0;
        if (val1 !== 0) {
          deltaPercent = ((val2 - val1) / Math.abs(val1)) * 100;
        }

        if (deltaPercent > 0) {
          trendText = 'Increase';
          trendIcon = '↗';
        } else if (deltaPercent < 0) {
          trendText = 'Decrease';
          trendIcon = '↘';
        }
      }

      return {
        name: item.name,
        isTotal: item.isTotal,
        values: item.values,
        deltaPercent,
        trendText,
        trendIcon
      };
    });

    return {
      periods,
      rows
    };
  }
}
