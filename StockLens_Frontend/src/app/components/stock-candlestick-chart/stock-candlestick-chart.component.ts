import { Component, Input, OnChanges, SimpleChanges, inject, Output, EventEmitter, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HighchartsChartComponent } from 'highcharts-angular';
import * as Highcharts from 'highcharts/highstock';
import { StockPriceHistoryService, PriceHistoryResponseDto } from '../../services/stock-price-history.service';

@Component({
  selector: 'app-stock-candlestick-chart',
  standalone: true,
  imports: [CommonModule, HighchartsChartComponent],
  templateUrl: './stock-candlestick-chart.component.html',
  styleUrls: ['./stock-candlestick-chart.component.css']
})
export class StockCandlestickChartComponent implements OnChanges, OnInit {
  @Input() symbol!: string;
  @Input() exchange: string = 'NSE';
  @Input() period: string = '1yr';
  @Input() customHeight: string = '100%';
  @Output() errorOccurred = new EventEmitter<string>();

  private priceService = inject(StockPriceHistoryService);
  private cd = inject(ChangeDetectorRef);

  loadingState: 'loading' | 'success' | 'error' | 'empty' = 'loading';
  errorMessage = '';
  priceHistory: PriceHistoryResponseDto | null = null;
  updateFlag = false;
  chart: any;

  // Visibility state for Candlestick series
  activeSeriesState: { [key: string]: boolean } = {
    'Candlestick': true,
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
        console.error('Error fetching candlestick price history:', err);
        this.loadingState = 'error';

        if (err.status === 0 || (err.message && err.message.includes('Http failure response'))) {
          this.errorMessage = 'Failed to fetch OHLC data';
        } else {
          this.errorMessage = err.message || 'Failed to fetch OHLC data';
        }

        this.errorOccurred.emit(this.errorMessage);
        this.cd.detectChanges();
      }
    });
  }

  onRefreshClick(): void {
    this.loadPriceHistory(true);
  }

  toggleSeries(seriesName: string): void {
    this.activeSeriesState[seriesName] = !this.activeSeriesState[seriesName];
    if (this.chart && this.chart.series) {
      const s = this.chart.series.find((x: any) => x.name === seriesName);
      if (s) {
        s.setVisible(this.activeSeriesState[seriesName], true);
      }
    }
  }

  private renderChart(data: PriceHistoryResponseDto): void {
    if (this.chart && this.chart.series) {
      this.chart.series.forEach((s: any) => {
        if (s.name) {
          this.activeSeriesState[s.name] = s.visible;
        }
      });
    }

    const p = (this.period || '1yr').toLowerCase().trim();

    // Prepare true OHLC Candlestick data: [timestamp, open, high, low, close]
    const candlestickData = data.dates.map((dateStr, i) => {
      const date = new Date(dateStr).getTime();
      const open = data.opens && data.opens.length > i && data.opens[i] > 0 ? data.opens[i] : data.closePrices[i];
      const high = data.highs && data.highs.length > i && data.highs[i] > 0 ? Math.max(data.highs[i], Math.max(open, data.closePrices[i])) : Math.max(open, data.closePrices[i]);
      const low = data.lows && data.lows.length > i && data.lows[i] > 0 ? Math.min(data.lows[i], Math.min(open, data.closePrices[i])) : Math.min(open, data.closePrices[i]);
      const close = data.closePrices[i];
      return [date, open, high, low, close];
    });

    // Prepare Volume data with bullish/bearish color encoding
    const volumeData = data.dates.map((dateStr, i) => {
      const date = new Date(dateStr).getTime();
      const open = data.opens && data.opens.length > i ? data.opens[i] : data.closePrices[i];
      const close = data.closePrices[i];
      const isBullish = close >= open;
      return {
        x: date,
        y: data.volumes[i],
        color: isBullish ? 'rgba(34, 197, 94, 0.45)' : 'rgba(239, 68, 68, 0.45)'
      };
    });

    // Determine axis formatting based on period
    let tickInterval = 365 * 24 * 3600 * 1000;
    let labelFormat = '{value:%Y}';

    if (p === '1m') {
      tickInterval = 7 * 24 * 3600 * 1000;
      labelFormat = '{value:%e %b}';
    } else if (p === '3m') {
      tickInterval = 14 * 24 * 3600 * 1000;
      labelFormat = '{value:%e %b}';
    } else if (p === '6m') {
      tickInterval = 30 * 24 * 3600 * 1000;
      labelFormat = '{value:%b %Y}';
    } else if (p === '1yr' || p === '1y') {
      tickInterval = 60 * 24 * 3600 * 1000;
      labelFormat = '{value:%b %Y}';
    } else if (p === '3yr' || p === '3y') {
      tickInterval = 180 * 24 * 3600 * 1000;
      labelFormat = '{value:%b %Y}';
    } else if (p === '5yr' || p === '5y') {
      tickInterval = 365 * 24 * 3600 * 1000;
      labelFormat = '{value:%b %Y}';
    } else {
      tickInterval = 2 * 365 * 24 * 3600 * 1000;
      labelFormat = '{value:%Y}';
    }

    const chartSeries: Highcharts.SeriesOptionsType[] = [
      {
        type: 'candlestick',
        id: 'candlestick-series-' + p,
        name: 'Candlestick',
        legendIndex: 1,
        data: candlestickData as any,
        color: '#ef4444',        // Bearish / Down candle body & wick
        upColor: '#22c55e',      // Bullish / Up candle body
        lineColor: '#ef4444',    // Bearish wick
        upLineColor: '#22c55e',  // Bullish wick
        visible: this.activeSeriesState['Candlestick'] !== false,
        yAxis: 0,
        zIndex: 2,
        dataGrouping: {
          enabled: false
        }
      } as any,
      {
        type: 'column',
        id: 'volume-series-' + p,
        name: 'Volume',
        legendIndex: 4,
        data: volumeData as any,
        visible: this.activeSeriesState['Volume'] !== false,
        yAxis: 1,
        zIndex: 1
      } as any
    ];

    // 50 DMA
    if (data.dma50 && data.dma50.length > 0) {
      const dma50Points = data.dates
        .map((dateStr, i) => [new Date(dateStr).getTime(), data.dma50![i]])
        .filter(pt => pt[1] !== null && pt[1] !== undefined);

      if (dma50Points.length > 0) {
        chartSeries.push({
          type: 'line',
          id: 'dma50-series-' + p,
          name: '50 DMA',
          legendIndex: 2,
          data: dma50Points as any,
          color: '#eab308', // Amber-500
          visible: this.activeSeriesState['50 DMA'] !== false,
          yAxis: 0,
          lineWidth: 1.8,
          marker: { enabled: false },
          zIndex: 3
        } as any);
      }
    }

    // 200 DMA
    if (data.dma200 && data.dma200.length > 0) {
      const dma200Points = data.dates
        .map((dateStr, i) => [new Date(dateStr).getTime(), data.dma200![i]])
        .filter(pt => pt[1] !== null && pt[1] !== undefined);

      if (dma200Points.length > 0) {
        chartSeries.push({
          type: 'line',
          id: 'dma200-series-' + p,
          name: '200 DMA',
          legendIndex: 3,
          data: dma200Points as any,
          color: '#a855f7', // Purple-500
          visible: this.activeSeriesState['200 DMA'] !== false,
          yAxis: 0,
          lineWidth: 1.8,
          marker: { enabled: false },
          zIndex: 3
        } as any);
      }
    }

    this.chartOptions = {
      chart: {
        backgroundColor: 'transparent',
        style: { fontFamily: 'Inter, sans-serif' },
        marginRight: 60,
        marginLeft: 48,
        marginTop: 12,
        marginBottom: 38,
        spacing: [4, 4, 8, 4]
      },
      title: {
        text: ''
      },
      legend: {
        enabled: false
      },
      credits: {
        enabled: false
      },
      rangeSelector: {
        enabled: false
      },
      navigator: {
        enabled: false
      },
      scrollbar: {
        enabled: false
      },
      xAxis: {
        type: 'datetime',
        gridLineColor: 'rgba(255, 255, 255, 0.05)',
        crosshair: {
          color: 'rgba(56, 189, 248, 0.35)',
          dashStyle: 'Dash',
          width: 1
        },
        labels: {
          style: { color: 'rgba(255, 255, 255, 0.9)', fontSize: '11px', fontWeight: '600' },
          format: labelFormat,
          y: 20
        },
        tickInterval: tickInterval,
        lineColor: 'rgba(255, 255, 255, 0.15)',
        tickColor: 'rgba(255, 255, 255, 0.15)'
      },
      yAxis: [
        {
          // Primary yAxis for Candlestick Price (Right Side)
          opposite: true,
          title: { text: '' },
          crosshair: {
            color: 'rgba(56, 189, 248, 0.35)',
            dashStyle: 'Dash',
            width: 1
          },
          gridLineColor: 'rgba(255, 255, 255, 0.05)',
          labels: {
            style: { color: 'rgba(255, 255, 255, 0.75)', fontSize: '11px', fontWeight: '500' },
            formatter: function (this: any) {
              return '₹' + Highcharts.numberFormat(this.value, 0, '', ',');
            }
          }
        },
        {
          // Secondary yAxis for Volumes (Left Side)
          opposite: false,
          title: { text: '' },
          gridLineWidth: 0,
          labels: {
            style: { color: 'rgba(255, 255, 255, 0.45)', fontSize: '10px', fontWeight: '500' },
            formatter: function (this: any) {
              const val = this.value as number;
              if (val >= 10000000) return (val / 10000000).toFixed(0) + 'Cr';
              if (val >= 1000000) return (val / 1000000).toFixed(0) + 'M';
              if (val >= 1000) return (val / 1000).toFixed(0) + 'K';
              return val.toString();
            }
          }
        }
      ],
      tooltip: {
        shared: true,
        useHTML: true,
        backgroundColor: 'rgba(15, 23, 42, 0.96)',
        borderColor: 'rgba(56, 189, 248, 0.3)',
        borderRadius: 8,
        shadow: true,
        style: { color: '#f8fafc', fontSize: '12px' },
        formatter: function (this: any) {
          const dateStr = Highcharts.dateFormat('%d %b %Y', this.x);
          let html = `<div style="font-weight:700; color:#38bdf8; margin-bottom:6px; border-bottom:1px solid rgba(255,255,255,0.12); padding-bottom:4px; font-size:12px;">${dateStr}</div>`;

          const candlePt = this.points?.find((p: any) => p.series.type === 'candlestick');
          if (candlePt && candlePt.point) {
            const pt = candlePt.point;
            const isUp = pt.close >= pt.open;
            const diff = pt.close - pt.open;
            const diffPct = pt.open > 0 ? (diff / pt.open) * 100 : 0;
            const changeSign = diff >= 0 ? '+' : '';
            const diffColor = isUp ? '#34d399' : '#f87171';

            html += `<div style="display:grid; grid-template-columns: 1fr 1fr; gap:3px 12px; margin-bottom:5px; font-size:11.5px;">`;
            html += `<div><span style="color:#94a3b8">Open:</span> <b>₹${Highcharts.numberFormat(pt.open, 2, '.', ',')}</b></div>`;
            html += `<div><span style="color:#94a3b8">High:</span> <b style="color:#34d399">₹${Highcharts.numberFormat(pt.high, 2, '.', ',')}</b></div>`;
            html += `<div><span style="color:#94a3b8">Low:</span> <b style="color:#f87171">₹${Highcharts.numberFormat(pt.low, 2, '.', ',')}</b></div>`;
            html += `<div><span style="color:#94a3b8">Close:</span> <b>₹${Highcharts.numberFormat(pt.close, 2, '.', ',')}</b></div>`;
            html += `</div>`;
            html += `<div style="margin-bottom:6px; font-size:11px; color:${diffColor}; font-weight:600;">Day Movement: <b>${changeSign}₹${Highcharts.numberFormat(diff, 2, '.', ',')} (${changeSign}${diffPct.toFixed(2)}%)</b></div>`;
          }

          const dma50Pt = this.points?.find((p: any) => p.series.name === '50 DMA');
          if (dma50Pt && dma50Pt.y) {
            html += `<div style="margin-bottom:2px;"><span style="color:#eab308">●</span> 50 DMA: <b>₹${Highcharts.numberFormat(dma50Pt.y, 2, '.', ',')}</b></div>`;
          }

          const dma200Pt = this.points?.find((p: any) => p.series.name === '200 DMA');
          if (dma200Pt && dma200Pt.y) {
            html += `<div style="margin-bottom:2px;"><span style="color:#a855f7">●</span> 200 DMA: <b>₹${Highcharts.numberFormat(dma200Pt.y, 2, '.', ',')}</b></div>`;
          }

          const volPt = this.points?.find((p: any) => p.series.name === 'Volume');
          if (volPt && volPt.y) {
            const formattedVol = new Intl.NumberFormat('en-IN').format(volPt.y);
            html += `<div style="margin-top:4px; padding-top:4px; border-top:1px dashed rgba(255,255,255,0.08);"><span style="color:#94a3b8">●</span> Volume: <b>${formattedVol}</b></div>`;
          }

          return html;
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
        candlestick: {
          color: '#ef4444',
          upColor: '#22c55e',
          lineColor: '#ef4444',
          upLineColor: '#22c55e',
          pointPadding: 0.1,
          groupPadding: 0.1
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
