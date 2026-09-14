import { Component, Input, OnChanges, SimpleChanges, inject, Output, EventEmitter, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HighchartsChartComponent } from 'highcharts-angular';
import * as Highcharts from 'highcharts';
import { StockPriceHistoryService, PriceHistoryResponseDto } from '../../services/stock-price-history.service';
import { TimeAgoPipe } from '../../pipes/time-ago.pipe';
@Component({
  selector: 'app-stock-price-chart',
  standalone: true,
  imports: [CommonModule, HighchartsChartComponent, TimeAgoPipe],
  templateUrl: './stock-price-chart.component.html',
  styleUrls: ['./stock-price-chart.component.css']
})
export class StockPriceChartComponent implements OnChanges, OnInit {
  @Input() symbol!: string;
  @Input() exchange: string = 'NSE';
  @Output() errorOccurred = new EventEmitter<string>();

  private priceService = inject(StockPriceHistoryService);
  private cd = inject(ChangeDetectorRef);

  loadingState: 'loading' | 'success' | 'error' | 'empty' = 'loading';
  errorMessage = '';
  priceHistory: PriceHistoryResponseDto | null = null;

  Highcharts: typeof Highcharts = Highcharts;
  chartOptions: Highcharts.Options = {};
  updateFlag = false;

  ngOnInit(): void {
    if (this.symbol && !this.priceHistory && this.loadingState !== 'success') {
      this.loadPriceHistory(false);
    }
  }

  ngOnChanges(changes: SimpleChanges): void {
    if ((changes['symbol'] || changes['exchange']) && this.symbol) {
      // Avoid refetching if ngOnInit already started it
      if (!changes['symbol']?.isFirstChange()) {
        this.loadPriceHistory(false);
      }
    }
  }

  loadPriceHistory(refresh: boolean = false): void {
    this.loadingState = 'loading';
    this.errorMessage = '';

    this.priceService.getPriceHistory(this.symbol, this.exchange, refresh).subscribe({
      next: (data) => {
        if (!data || !data.dates || data.dates.length === 0) {
          this.loadingState = 'empty';
          this.priceHistory = null;
          this.cd.detectChanges();
        } else {
          this.priceHistory = data;
          this.loadingState = 'success';
          this.renderChart(data);
        }
      },
      error: (err) => {
        console.error('Error fetching price history:', err);
        this.loadingState = 'error';
        
        if (err.status === 0 || (err.message && err.message.includes('Http failure response'))) {
          this.errorMessage = 'Failed to fetch';
        } else {
          this.errorMessage = err.message || 'Failed to fetch';
        }
        
        this.errorOccurred.emit(this.errorMessage);
        this.cd.detectChanges();
      }
    });
  }

  onRefreshClick(): void {
    this.loadPriceHistory(true);
  }

  private renderChart(data: PriceHistoryResponseDto): void {
    // Parse dates and zip with prices for Highcharts
    // Highcharts expects data as [timestamp, value]
    const priceData = data.dates.map((dateStr, i) => {
      const date = new Date(dateStr).getTime();
      return [date, data.closePrices[i]];
    });

    const volumeData = data.dates.map((dateStr, i) => {
      const date = new Date(dateStr).getTime();
      return [date, data.volumes[i]];
    });

    this.chartOptions = {
      chart: {
        backgroundColor: 'transparent',
        alignTicks: false,
        style: {
          fontFamily: 'Inter, system-ui, sans-serif'
        },
        height: 500
      },
      title: {
        text: ''
      },
      credits: {
        enabled: false
      },
      xAxis: {
        type: 'datetime',
        gridLineColor: 'rgba(255, 255, 255, 0.05)',
        labels: {
          style: { color: 'rgba(255, 255, 255, 0.6)' },
          format: '{value:%Y}'
        },
        tickInterval: 365 * 24 * 3600 * 1000, // 1 year interval
        lineColor: 'rgba(255, 255, 255, 0.1)',
        tickColor: 'rgba(255, 255, 255, 0.1)'
      },
      yAxis: [
        {
          // Primary yAxis for Prices (Moved to Right Side)
          opposite: true,
          title: { text: '' },
          tickInterval: 100,
          gridLineColor: 'rgba(255, 255, 255, 0.05)',
          labels: {
            style: { color: 'rgba(255, 255, 255, 0.6)' },
            formatter: function (this: any) {
              return Highcharts.numberFormat(this.value, 0, '', ',');
            }
          }
        },
        {
          // Secondary yAxis for Volumes (Moved to Left Side)
          opposite: false,
          title: { text: '' },
          gridLineWidth: 0,
          tickInterval: 20000000, // Enforce ticks every 20,000,000 (20000K)
          labels: {
            style: { color: 'rgba(255, 255, 255, 0.4)' },
            formatter: function (this: any) {
              const val = this.value as number;
              if (val >= 1000) return Math.floor(val / 1000) + 'K';
              return val.toString();
            }
          }
        }
      ],
      tooltip: {
        shared: true,
        backgroundColor: 'rgba(20, 25, 35, 0.95)',
        borderColor: 'rgba(255, 255, 255, 0.1)',
        style: { color: '#fff' },
        xDateFormat: '%d %b %Y'
      },
      plotOptions: {
        area: {
          fillColor: {
            linearGradient: { x1: 0, y1: 0, x2: 0, y2: 1 },
            stops: [
              [0, 'rgba(56, 189, 248, 0.5)'],
              [1, 'rgba(56, 189, 248, 0.0)']
            ]
          },
          marker: { radius: 2 },
          lineWidth: 2,
          states: { hover: { lineWidth: 3 } },
          threshold: null
        },
        column: {
          borderWidth: 0,
          borderRadius: 2
        }
      },
      series: [
        {
          type: 'area',
          name: 'Close Price',
          data: priceData,
          color: '#38bdf8', // Light blue (tailwind sky-400)
          yAxis: 0,
          tooltip: {
            pointFormat: '<b>₹{point.y:,.2f}</b><br/>'
          }
        },
        {
          type: 'column',
          name: 'Volume',
          data: volumeData,
          color: 'rgba(148, 163, 184, 0.4)', // Slate-400 with opacity
          yAxis: 1,
          tooltip: {
            pointFormatter: function (this: any) {
              const val = this.y as number;
              const formatted = new Intl.NumberFormat('en-IN').format(val);
              return '<span style="color:' + this.color + '">\u25CF</span> Volume: <b>' + formatted + '</b><br/>';
            }
          }
        }
      ]
    };

    this.updateFlag = true;
    this.cd.detectChanges();
  }
}
