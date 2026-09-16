import { Component, OnInit, OnDestroy, inject, signal, HostListener, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Subject, Subscription, of, timer } from 'rxjs';
import { debounceTime, distinctUntilChanged, switchMap, catchError } from 'rxjs/operators';
import { StockNewsService } from '../../services/stock-news.service';
import { StockShareholdingService } from '../../services/stock-shareholding.service';
import { StockCashflowService } from '../../services/stock-cashflow.service';
import { StockBalanceSheetService, BalanceSheetResponseDto } from '../../services/stock-balancesheet.service';
import { StockQuarterlyResultsService } from '../../services/stock-quarterly-results.service';
import { Stock, Company, StockNewsResponse, LoadingState } from '../../models/stock-news.model';
import { StockShareholdingResponse } from '../../models/stock-shareholding.model';
import { StockCashflowResponse } from '../../models/stock-cashflow.model';
import { StockQuarterlyResultsResponse } from '../../models/stock-quarterly-results.model';
import { StockNewsCardComponent } from '../stock-news-card/stock-news-card.component';
import { StockShareholdingCardComponent } from '../stock-shareholding-card/stock-shareholding-card.component';
import { StockCashflowCardComponent } from '../stock-cashflow-card/stock-cashflow-card.component';
import { StockAssetGrowthCardComponent } from '../stock-asset-growth-card/stock-asset-growth-card.component';
import { StockPriceChartComponent } from '../stock-price-chart/stock-price-chart.component';
import { StockRatiosValuationCardComponent } from '../stock-ratios-valuation-card/stock-ratios-valuation-card.component';
import { StockQuartersCardComponent } from '../stock-quarters-card/stock-quarters-card.component';

export type DashboardSection = 'overview' | 'ratios' | 'cashflow' | 'balancesheet' | 'shareholding' | 'quarters' | 'chart' | 'news';

@Component({
  selector: 'app-stock-dashboard',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    StockNewsCardComponent,
    StockShareholdingCardComponent,
    StockCashflowCardComponent,
    StockAssetGrowthCardComponent,
    StockPriceChartComponent,
    StockRatiosValuationCardComponent,
    StockQuartersCardComponent
  ],
  templateUrl: './stock-dashboard.component.html',
  styleUrl: './stock-dashboard.component.css'
})
export class StockDashboardComponent implements OnInit, OnDestroy {
  private readonly newsService = inject(StockNewsService);
  private readonly shareholdingService = inject(StockShareholdingService);
  private readonly cashflowService = inject(StockCashflowService);
  private readonly financialsService = inject(StockBalanceSheetService);
  private readonly quartersService = inject(StockQuarterlyResultsService);
  private readonly cd = inject(ChangeDetectorRef);

  // Quick select stocks
  readonly quickStocks = [
    { symbol: 'TATAMOTORS', name: 'Tata Motors Ltd', exchange: 'NSE' },
    { symbol: 'RELIANCE', name: 'Reliance Industries Ltd', exchange: 'NSE' },
    { symbol: 'TCS', name: 'Tata Consultancy Services', exchange: 'NSE' },
    { symbol: 'INFY', name: 'Infosys Limited', exchange: 'NSE' },
    { symbol: 'HDFCBANK', name: 'HDFC Bank Ltd', exchange: 'NSE' },
    { symbol: 'ICICIBANK', name: 'ICICI Bank Ltd', exchange: 'NSE' }
  ];

  // Active section for ScrollSpy and sticky tab highlight
  activeSection = signal<DashboardSection>('overview');
  availableStocks = signal<Stock[]>([]);
  selectedSymbol = signal<string>('RELIANCE');
  selectedExchange = signal<string>('NSE');
  searchQuery = signal<string>('');
  chartPeriod = signal<string>('5yr');

  // Flag to disable scrollspy tracking briefly during programmatic smooth scrolling
  private isProgrammaticScrolling = false;
  private scrollTimeout?: any;
  private pricePollingSubscription?: Subscription;

  // Shareholding State
  shareholdingResponse = signal<StockShareholdingResponse | null>(null);
  shareholdingLoadingState = signal<LoadingState>('idle');
  shareholdingErrorMessage = signal<string>('');
  isShareholdingRefreshing = signal<boolean>(false);

  // Quarters State
  quartersResponse = signal<StockQuarterlyResultsResponse | null>(null);
  quartersLoadingState = signal<LoadingState>('idle');
  quartersErrorMessage = signal<string>('');
  isQuartersRefreshing = signal<boolean>(false);

  // Cashflow State
  cashflowResponse = signal<StockCashflowResponse | null>(null);
  cashflowLoadingState = signal<LoadingState>('idle');
  cashflowErrorMessage = signal<string>('');
  isCashflowRefreshing = signal<boolean>(false);

  // Assets State
  assetsResponse = signal<BalanceSheetResponseDto | null>(null);
  assetsLoadingState = signal<LoadingState>('idle');
  assetsErrorMessage = signal<string>('');
  isAssetsRefreshing = signal<boolean>(false);

  // News State
  newsResponse = signal<StockNewsResponse | null>(null);
  newsLoadingState = signal<LoadingState>('idle');
  newsErrorMessage = signal<string>('');
  isNewsRefreshing = signal<boolean>(false);

  // Sync all state
  isSyncingAll = signal<boolean>(false);

  // Typeahead search
  searchResults = signal<Company[]>([]);
  isSearching = signal<boolean>(false);
  private searchSubject = new Subject<string>();
  private searchSubscription?: Subscription;

  ngOnInit(): void {
    this.loadAvailableStocks();
    this.setupSearch();
    this.fetchAllData(false);
    this.startLivePricePolling();
  }

  ngOnDestroy(): void {
    this.searchSubscription?.unsubscribe();
    this.stopLivePricePolling();
    if (this.scrollTimeout) {
      clearTimeout(this.scrollTimeout);
    }
  }

  /**
   * Checks whether the Indian Stock Market (NSE/BSE) is currently open for trading.
   * Trading hours: Monday to Friday, 09:15 AM to 03:30 PM IST (UTC+5:30).
   */
  isIndianMarketOpen(): boolean {
    const now = new Date();
    // Convert to Indian Standard Time (IST = UTC + 5 hours 30 minutes)
    const utcTime = now.getTime() + (now.getTimezoneOffset() * 60000);
    const istDate = new Date(utcTime + (3600000 * 5.5));

    const day = istDate.getDay(); // 0 = Sunday, 6 = Saturday
    if (day === 0 || day === 6) return false;

    const hours = istDate.getHours();
    const minutes = istDate.getMinutes();
    const totalMinutes = hours * 60 + minutes;

    // 09:15 AM = 555 mins; 03:30 PM = 930 mins
    return totalMinutes >= 555 && totalMinutes <= 930;
  }

  /**
   * Starts a silent 30-second background polling interval during market hours
   * to automatically refresh real-time stock price and price-dependent valuation ratios.
   */
  startLivePricePolling(): void {
    this.stopLivePricePolling();

    // Poll every 30 seconds
    this.pricePollingSubscription = timer(30000, 30000).subscribe(() => {
      if (this.isIndianMarketOpen()) {
        const symbol = this.selectedSymbol();
        const exchange = this.selectedExchange();

        this.cashflowService.getCashflowBySymbol(symbol, exchange, false).subscribe({
          next: (data) => {
            if (data && data.ratios && this.selectedSymbol() === symbol) {
              // Silently update cashflowResponse signal (updates Hero price, P/E, P/B, Market Cap, 52W progress)
              this.cashflowResponse.set(data);
            }
          },
          error: (err) => {
            console.debug('[Background Live Price] Polling skipped:', err);
          }
        });
      }
    });
  }

  stopLivePricePolling(): void {
    if (this.pricePollingSubscription) {
      this.pricePollingSubscription.unsubscribe();
      this.pricePollingSubscription = undefined;
    }
  }

  /**
   * ScrollSpy Listener: Automatically tracks active section on window scroll
   */
  @HostListener('window:scroll', [])
  onWindowScroll(): void {
    if (this.isProgrammaticScrolling) return;

    // 1. If at the absolute top of the page (within 10px), activate 'overview'
    if (window.scrollY <= 10) {
      if (this.activeSection() !== 'overview') {
        this.activeSection.set('overview');
      }
      return;
    }

    // 2. If near the bottom of document, activate the last section ('news')
    const scrollBottom = window.innerHeight + window.scrollY;
    const docHeight = document.documentElement.scrollHeight;
    if (scrollBottom >= docHeight - 60) {
      if (this.activeSection() !== 'news') {
        this.activeSection.set('news');
      }
      return;
    }

    const sections: DashboardSection[] = [
      'overview',
      'ratios',
      'cashflow',
      'balancesheet',
      'shareholding',
      'quarters',
      'chart',
      'news'
    ];

    // The reading focal line directly below the sticky header (62px) + sticky subnav (~48px)
    const focalY = 135;

    for (const sectionId of sections) {
      const el = document.getElementById('section-' + sectionId);
      if (el) {
        const rect = el.getBoundingClientRect();
        // Check if the focal point is inside this section's visible bounds
        if (rect.top <= focalY && rect.bottom > focalY) {
          if (this.activeSection() !== sectionId) {
            this.activeSection.set(sectionId);
          }
          return;
        }
      }
    }
  }

  /**
   * Smoothly scroll to a specific section and highlight its tab
   */
  scrollToSection(sectionId: DashboardSection): void {
    this.activeSection.set(sectionId);
    this.isProgrammaticScrolling = true;

    if (this.scrollTimeout) {
      clearTimeout(this.scrollTimeout);
    }

    const el = document.getElementById('section-' + sectionId);
    if (el) {
      const stickyHeaderOffset = 116; // 62px header + 48px subnav + 6px buffer
      const elementPosition = el.getBoundingClientRect().top;
      const targetY = elementPosition + window.scrollY - stickyHeaderOffset;

      window.scrollTo({
        top: Math.max(0, targetY),
        behavior: 'smooth'
      });
    }

    // Reset programmatic scrolling flag after smooth scroll completes
    this.scrollTimeout = setTimeout(() => {
      this.isProgrammaticScrolling = false;
    }, 600);
  }

  setupSearch(): void {
    this.searchSubscription = this.searchSubject.pipe(
      debounceTime(300),
      distinctUntilChanged(),
      switchMap((query) => {
        if (!query.trim() || query.trim().length < 2) {
          this.searchResults.set([]);
          this.isSearching.set(false);
          return of([]);
        }
        this.isSearching.set(true);
        return this.newsService.searchCompanies(query).pipe(
          catchError(() => {
            return of([]);
          })
        );
      })
    ).subscribe((results) => {
      this.searchResults.set(results);
      this.isSearching.set(false);
    });
  }

  loadAvailableStocks(): void {
    this.newsService.getStocks().subscribe({
      next: (stocks) => {
        this.availableStocks.set(stocks);
      },
      error: (err) => {
        console.warn('Could not load pre-seeded stocks list:', err);
      }
    });
  }

  selectStock(symbol: string, exchange: string = 'NSE'): void {
    const cleanSymbol = symbol.toUpperCase();
    const cleanExchange = exchange.toUpperCase();
    this.selectedSymbol.set(cleanSymbol);
    this.selectedExchange.set(cleanExchange);
    this.searchQuery.set('');
    this.searchResults.set([]);

    // Fetch all fundamental and news datasets concurrently
    this.fetchAllData(false);
    this.startLivePricePolling();

    // Scroll to overview top smoothly
    this.scrollToSection('overview');
  }

  toggleExchange(exchange: string): void {
    if (this.selectedExchange() !== exchange) {
      this.selectedExchange.set(exchange);
      this.searchQuery.set('');
      this.searchResults.set([]);
      this.fetchAllData(false);
      this.startLivePricePolling();
    }
  }

  setChartPeriod(period: string): void {
    this.chartPeriod.set(period);
  }

  onSearchInput(event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    this.searchQuery.set(value);
    this.searchSubject.next(value);
  }

  onSearchSubmit(): void {
    const query = this.searchQuery().trim();
    if (query) {
      this.selectedSymbol.set(query.toUpperCase());
      this.searchResults.set([]);
      this.fetchAllData(false);
      this.startLivePricePolling();
      this.scrollToSection('overview');
    }
  }

  selectSearchResult(company: Company): void {
    this.searchQuery.set('');
    this.searchResults.set([]);
    this.selectStock(company.symbol, this.selectedExchange());
  }

  fetchAllData(isRefresh: boolean = false): void {
    this.fetchCashflow(isRefresh);
    this.fetchBalanceSheet(isRefresh);
    this.fetchShareholding(isRefresh);
    this.fetchQuarterlyResults(isRefresh);
    this.fetchNews(isRefresh);
  }

  syncAllData(): void {
    this.isSyncingAll.set(true);
    this.fetchAllData(true);
    setTimeout(() => {
      this.isSyncingAll.set(false);
    }, 1500);
  }

  fetchQuarterlyResults(isRefresh: boolean): void {
    if (isRefresh) {
      this.isQuartersRefreshing.set(true);
    } else {
      this.quartersLoadingState.set('loading');
    }
    this.quartersErrorMessage.set('');

    const symbol = this.selectedSymbol();
    const exchange = this.selectedExchange();

    this.quartersService.getQuarterlyResultsBySymbol(symbol, exchange, isRefresh).subscribe({
      next: (data) => {
        this.quartersResponse.set(data);
        this.isQuartersRefreshing.set(false);
        if (data && data.history && data.history.length > 0) {
          this.quartersLoadingState.set('success');
        } else {
          this.quartersLoadingState.set('empty');
        }
      },
      error: (err) => {
        this.isQuartersRefreshing.set(false);
        this.quartersLoadingState.set('error');
        this.quartersErrorMessage.set(
          err.error?.detail || err.error?.message || 'Failed to retrieve quarterly results.'
        );
      }
    });
  }

  fetchBalanceSheet(isRefresh: boolean): void {
    const symbol = this.selectedSymbol();
    const exchange = this.selectedExchange();
    if (!symbol) return;

    if (isRefresh) {
      this.isAssetsRefreshing.set(true);
    } else {
      this.assetsLoadingState.set('loading');
    }
    this.assetsErrorMessage.set('');

    this.financialsService.getBalanceSheet(symbol, exchange, isRefresh).subscribe({
      next: (response) => {
        this.assetsResponse.set(response);
        this.assetsLoadingState.set('success');
        this.isAssetsRefreshing.set(false);
      },
      error: (err) => {
        console.error('Error fetching balance sheet:', err);
        this.assetsErrorMessage.set(err.error?.message || 'Could not fetch asset data.');
        this.assetsLoadingState.set('error');
        this.isAssetsRefreshing.set(false);
      }
    });
  }

  fetchShareholding(isRefresh: boolean): void {
    if (isRefresh) {
      this.isShareholdingRefreshing.set(true);
    } else {
      this.shareholdingLoadingState.set('loading');
    }
    this.shareholdingErrorMessage.set('');

    const symbol = this.selectedSymbol();
    const exchange = this.selectedExchange();

    this.shareholdingService.getShareholdingBySymbol(symbol, exchange, isRefresh).subscribe({
      next: (data) => {
        this.shareholdingResponse.set(data);
        this.isShareholdingRefreshing.set(false);
        if (data.currentPeriod) {
          this.shareholdingLoadingState.set('success');
        } else {
          this.shareholdingLoadingState.set('empty');
        }
      },
      error: (err) => {
        this.isShareholdingRefreshing.set(false);
        this.shareholdingLoadingState.set('error');
        this.shareholdingErrorMessage.set(
          err.error?.detail || err.error?.message || 'Failed to retrieve shareholding data.'
        );
      }
    });
  }

  fetchCashflow(isRefresh: boolean): void {
    if (isRefresh) {
      this.isCashflowRefreshing.set(true);
    } else {
      this.cashflowLoadingState.set('loading');
    }
    this.cashflowErrorMessage.set('');

    const symbol = this.selectedSymbol();
    const exchange = this.selectedExchange();

    this.cashflowService.getCashflowBySymbol(symbol, exchange, isRefresh).subscribe({
      next: (data) => {
        this.cashflowResponse.set(data);
        this.isCashflowRefreshing.set(false);
        if (data && data.summary) {
          this.cashflowLoadingState.set('success');
        } else {
          this.cashflowLoadingState.set('empty');
        }
      },
      error: (err) => {
        this.isCashflowRefreshing.set(false);
        this.cashflowLoadingState.set('error');
        this.cashflowErrorMessage.set(
          err.error?.detail || err.error?.message || 'Failed to retrieve cash flow and financial data.'
        );
      }
    });
  }

  fetchNews(isRefresh: boolean): void {
    if (isRefresh) {
      this.isNewsRefreshing.set(true);
    } else {
      this.newsLoadingState.set('loading');
    }
    this.newsErrorMessage.set('');

    const symbol = this.selectedSymbol();
    const exchange = this.selectedExchange();

    this.newsService.getNewsBySymbol(symbol, exchange, 20, 1, isRefresh).subscribe({
      next: (data) => {
        this.newsResponse.set(data);
        this.isNewsRefreshing.set(false);
        if (data.news && data.news.length > 0) {
          this.newsLoadingState.set('success');
        } else {
          this.newsLoadingState.set('empty');
        }
      },
      error: (err) => {
        this.isNewsRefreshing.set(false);
        this.newsLoadingState.set('error');
        this.newsErrorMessage.set(
          err.error?.detail || err.error?.message || 'Failed to retrieve news from backend.'
        );
      }
    });
  }

  getCompanyName(): string {
    return this.quartersResponse()?.companyName ||
      this.cashflowResponse()?.companyName ||
      this.shareholdingResponse()?.companyName ||
      this.newsResponse()?.companyName ||
      this.selectedSymbol();
  }

  // Financial Number Formatters (Live API Data Only)
  formatCurrency(val?: number | null): string {
    if (val === null || val === undefined || isNaN(val)) return '—';
    return '₹ ' + val.toLocaleString('en-IN', { maximumFractionDigits: 2, minimumFractionDigits: 0 });
  }

  formatMarketCap(val?: number | null): string {
    if (val === null || val === undefined || isNaN(val)) return '—';
    if (val >= 10000000) {
      return '₹ ' + (val / 10000000).toLocaleString('en-IN', { maximumFractionDigits: 0 }) + ' Cr.';
    }
    return '₹ ' + val.toLocaleString('en-IN', { maximumFractionDigits: 0 }) + ' Cr.';
  }

  formatHighLow(): string {
    const high = this.cashflowResponse()?.ratios?.week52High;
    const low = this.cashflowResponse()?.ratios?.week52Low;
    if (high !== null && high !== undefined && low !== null && low !== undefined) {
      return `₹ ${high.toLocaleString('en-IN', { maximumFractionDigits: 2 })} / ${low.toLocaleString('en-IN', { maximumFractionDigits: 2 })}`;
    }
    if (high !== null && high !== undefined) {
      return `₹ ${high.toLocaleString('en-IN', { maximumFractionDigits: 2 })}`;
    }
    if (low !== null && low !== undefined) {
      return `₹ ${low.toLocaleString('en-IN', { maximumFractionDigits: 2 })}`;
    }
    return '—';
  }

  formatRatio(val?: number | null): string {
    if (val === null || val === undefined || isNaN(val)) return '—';
    return val.toFixed(1);
  }

  formatPercent(val?: number | null): string {
    if (val === null || val === undefined || isNaN(val)) return '—';
    return val.toFixed(1) + ' %';
  }

  formatAsOfDate(dateStr?: string | null): string {
    if (!dateStr) return '—';
    let parseable = dateStr.trim();
    if (/^\d{4}-\d{2}-\d{2}\s\d{2}:\d{2}:\d{2}$/.test(parseable)) {
      parseable = parseable.replace(' ', 'T') + 'Z';
    } else if (/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}$/.test(parseable)) {
      parseable += 'Z';
    }

    const date = new Date(parseable);
    if (isNaN(date.getTime())) return dateStr;

    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');
    const hours = String(date.getHours()).padStart(2, '0');
    const minutes = String(date.getMinutes()).padStart(2, '0');
    const seconds = String(date.getSeconds()).padStart(2, '0');

    return `${year}-${month}-${day} ${hours}:${minutes}:${seconds}`;
  }
}
