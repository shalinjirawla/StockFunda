import { Component, Input, OnChanges, SimpleChanges, inject, Output, EventEmitter, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HighchartsChartComponent } from 'highcharts-angular';
import * as Highcharts from 'highcharts';
import { StockPriceHistoryService, PriceHistoryResponseDto } from '../../services/stock-price-history.service';
@Component({
  selector: 'app-stock-price-chart',
  standalone: true,
  imports: [CommonModule, HighchartsChartComponent],
  templateUrl: './stock-price-chart.component.html',
  styleUrls: ['./stock-price-chart.component.css']
})
export class StockPriceChartComponent implements OnChanges, OnInit {
  @Input() symbol!: string;
  @Input() exchange: string = 'NSE';
  @Input() period: string = '5yr';
  @Output() errorOccurred = new EventEmitter<string>();

  private priceService = inject(StockPriceHistoryService);
  private cd = inject(ChangeDetectorRef);

  loadingState: 'loading' | 'success' | 'error' | 'empty' = 'loading';
  errorMessage = '';
  priceHistory: PriceHistoryResponseDto | null = null;
  updateFlag = false;
  chart: any; // Reference to the live chart

  // Track user's legend selections so they persist across refreshes
  activeSeriesState: { [key: string]: boolean } = {
    'Price': true,
    '50 DMA': true,
    '200 DMA': true,
    'Volume': true
  };

  Highcharts: typeof Highcharts = Highcharts;
  chartOptions: Highcharts.Options = {};

  ngOnInit(): void {
    if (this.symbol && !this.priceHistory && this.loadingState !== 'success') {
      this.loadPriceHistory(false);
    }
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['symbol'] && changes['symbol'].isFirstChange()) return;

    if (this.symbol && (changes['symbol'] || changes['exchange'] || changes['period'])) {
      this.loadPriceHistory(false);
    }
  }

  loadPriceHistory(refresh: boolean = false): void {
    this.loadingState = 'loading';
    this.errorMessage = '';

    this.priceService.getPriceHistory(this.symbol, this.exchange, this.period, refresh).subscribe({
      next: (data) => {
        if (!data || !data.dates || data.dates.length === 0) {
          this.loadingState = 'empty';
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
    // ROOT CAUSE FIX: Read the exact visibility state directly from the LIVE chart
    // before we update the data. This bypasses all legend HTML click bugs!
    if (this.chart && this.chart.series) {
      this.chart.series.forEach((s: any) => {
        if (s.name) {
          this.activeSeriesState[s.name] = s.visible;
        }
      });
    }

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

    // Determine axis formatting based on period
    let tickInterval = 365 * 24 * 3600 * 1000; // 1 year default
    let labelFormat = '{value:%Y}';
    const p = (this.period || '5yr').toLowerCase();

    if (p === '1m') {
      tickInterval = 7 * 24 * 3600 * 1000; // 1 week
      labelFormat = '{value:%e %b}';
    } else if (p === '6m') {
      tickInterval = 30 * 24 * 3600 * 1000; // 1 month
      labelFormat = '{value:%e %b}';
    } else if (p === '1yr') {
      tickInterval = 3 * 30 * 24 * 3600 * 1000; // 3 months
      labelFormat = '{value:%b %Y}';
    } else if (p === '3yr') {
      tickInterval = 6 * 30 * 24 * 3600 * 1000; // 6 months
      labelFormat = '{value:%b %Y}';
    } else if (p === '5yr') {
      tickInterval = 365 * 24 * 3600 * 1000; // 1 year
      labelFormat = '{value:%b %Y}';
    } else {
      tickInterval = 2 * 365 * 24 * 3600 * 1000; // 2 years
      labelFormat = '{value:%Y}';
    }

    const chartSeries: Highcharts.SeriesOptionsType[] = [
      {
        type: 'area',
        id: 'price-' + p,
        name: 'Price',
        legendIndex: 1,
        data: priceData,
        color: '#38bdf8', // Light blue (tailwind sky-400)
        visible: this.activeSeriesState['Price'],
        yAxis: 0
      },
      {
        type: 'column',
        id: 'volume-' + p,
        name: 'Volume',
        legendIndex: 4,
        data: volumeData,
        color: 'rgba(148, 163, 184, 0.4)', // Slate-400 with opacity
        visible: this.activeSeriesState['Volume'],
        yAxis: 1
      }
    ];

    if (data.dma50 && data.dma50.length > 0) {
      chartSeries.push({
        type: 'line',
        id: 'dma50-' + p,
        name: '50 DMA',
        legendIndex: 2,
        data: data.dates.map((dateStr, i) => [new Date(dateStr).getTime(), data.dma50![i]]),
        color: '#f97316', // Orange-500
        visible: this.activeSeriesState['50 DMA'],
        yAxis: 0,
        lineWidth: 1.5,
        marker: { enabled: false },
        tooltip: { pointFormat: '<b>₹{point.y:,.2f}</b><br/>' }
      } as any);
    }

    if (data.dma200 && data.dma200.length > 0) {
      chartSeries.push({
        type: 'line',
        id: 'dma200-' + p,
        name: '200 DMA',
        legendIndex: 3,
        data: data.dates.map((dateStr, i) => [new Date(dateStr).getTime(), data.dma200![i]]),
        color: '#a855f7', // Purple-500
        visible: this.activeSeriesState['200 DMA'],
        yAxis: 0,
        lineWidth: 1.5,
        marker: { enabled: false },
        tooltip: { pointFormat: '<b>₹{point.y:,.2f}</b><br/>' }
      } as any);
    }

    this.chartOptions = {
      chart: {
        backgroundColor: 'transparent',
        style: { fontFamily: 'Inter, sans-serif' },
        marginRight: 100, // Space for right Y axis labels + title
        marginLeft: 100  // Space for left Y axis labels + title
      },
      title: {
        text: ''
      },
      legend: {
        useHTML: true,
        symbolWidth: 0,
        symbolHeight: 0,
        symbolPadding: 0,
        squareSymbol: false,
        labelFormatter: function (this: any) {
          const textOpacity = this.visible ? 1 : 0.4;
          return `<span style="font-size: 15px; font-weight: 500;"><span style="color: ${this.color}; font-size: 1.3em; margin-right: 6px; vertical-align: middle;">\u25CF</span><span style="opacity: ${textOpacity}; vertical-align: middle;">${this.name}</span></span>`;
        },
        itemStyle: { fontSize: '16px', color: 'rgba(255, 255, 255, 0.9)', cursor: 'pointer', fontWeight: '500' },
        itemHiddenStyle: { color: 'rgba(255, 255, 255, 0.8)', textDecoration: 'none' }
      },
      credits: {
        enabled: false
      },
      xAxis: {
        type: 'datetime',
        gridLineColor: 'rgba(255, 255, 255, 0.05)',
        labels: {
          style: { color: 'rgba(255, 255, 255, 0.6)' },
          format: labelFormat
        },
        tickInterval: tickInterval,
        lineColor: 'rgba(255, 255, 255, 0.1)',
        tickColor: 'rgba(255, 255, 255, 0.1)'
      },
      yAxis: [
        {
          // Primary yAxis for Prices (Moved to Right Side)
          opposite: true,
          title: { text: 'Price (₹)', style: { color: 'rgba(255, 255, 255, 0.8)', fontWeight: '500', fontSize: '14px', letterSpacing: '0.5px' } },
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
          title: { text: 'Volume', style: { color: 'rgba(255, 255, 255, 0.8)', fontWeight: '500', fontSize: '14px', letterSpacing: '0.5px' } },
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
        useHTML: true,
        backgroundColor: 'rgba(20, 25, 35, 0.95)',
        borderColor: 'rgba(255, 255, 255, 0.1)',
        style: { color: '#fff' },
        formatter: function (this: any) {
          const dateStr = Highcharts.dateFormat('%d %b %Y', this.x);
          let tooltipHtml = `<div style="margin-bottom: 6px; font-size: 13px;"><b>${dateStr}</b></div>`;

          const pointMap: { [key: string]: any } = {};
          if (this.points) {
            this.points.forEach((p: any) => {
              pointMap[p.series.name] = p;
            });
          }

          if (pointMap['Price']) {
            tooltipHtml += `<span style="color:${pointMap['Price'].color}">\u25CF</span> Price: <b>₹${Highcharts.numberFormat(pointMap['Price'].y, 2, '.', ',')}</b><br/>`;
          }
          if (pointMap['50 DMA']) {
            tooltipHtml += `<span style="color:${pointMap['50 DMA'].color}">\u25CF</span> 50 DMA: <b>₹${Highcharts.numberFormat(pointMap['50 DMA'].y, 2, '.', ',')}</b><br/>`;
          }
          if (pointMap['200 DMA']) {
            tooltipHtml += `<span style="color:${pointMap['200 DMA'].color}">\u25CF</span> 200 DMA: <b>₹${Highcharts.numberFormat(pointMap['200 DMA'].y, 2, '.', ',')}</b><br/>`;
          }
          if (pointMap['Volume']) {
            const val = pointMap['Volume'].y;
            const formattedVol = new Intl.NumberFormat('en-IN').format(val);
            tooltipHtml += `<span style="color:${pointMap['Volume'].color}">\u25CF</span> Volume: <b>${formattedVol}</b><br/>`;
          }

          return tooltipHtml;
        }
      },
      plotOptions: {
        series: {
          states: {
            inactive: {
              opacity: 1
            }
          }
        },
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
      series: chartSeries
    };

    this.updateFlag = true;
    this.cd.detectChanges();
  }
}
