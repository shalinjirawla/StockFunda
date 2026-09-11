import { Component, OnInit, OnDestroy, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Subject, Subscription, of } from 'rxjs';
import { debounceTime, distinctUntilChanged, switchMap, catchError } from 'rxjs/operators';
import { StockNewsService } from '../../services/stock-news.service';
import { StockShareholdingService } from '../../services/stock-shareholding.service';
import { StockCashflowService } from '../../services/stock-cashflow.service';
import { StockFinancialsService } from '../../services/stock-financials.service';
import { Stock, Company, StockNewsResponse, LoadingState } from '../../models/stock-news.model';
import { StockShareholdingResponse } from '../../models/stock-shareholding.model';
import { StockCashflowResponse } from '../../models/stock-cashflow.model';
import { StockNewsCardComponent } from '../stock-news-card/stock-news-card.component';
import { StockShareholdingCardComponent } from '../stock-shareholding-card/stock-shareholding-card.component';
import { StockCashflowCardComponent } from '../stock-cashflow-card/stock-cashflow-card.component';
import { StockAssetGrowthCardComponent } from '../stock-asset-growth-card/stock-asset-growth-card.component';
import { BalanceSheetResponseDto } from '../../services/stock-financials.service';
import { TimeAgoPipe } from '../../pipes/time-ago.pipe';

export type DashboardTab = 'cashflow' | 'shareholding' | 'news' | 'assets';

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
    TimeAgoPipe
  ],
  templateUrl: './stock-dashboard.component.html',
  styleUrl: './stock-dashboard.component.css'
})
export class StockDashboardComponent implements OnInit, OnDestroy {
  private readonly newsService = inject(StockNewsService);
  private readonly shareholdingService = inject(StockShareholdingService);
  private readonly cashflowService = inject(StockCashflowService);
  private readonly financialsService = inject(StockFinancialsService);

  // Quick select stocks
  readonly quickStocks = [
    { symbol: 'RELIANCE', name: 'Reliance Industries', exchange: 'NSE' },
    { symbol: 'TCS', name: 'Tata Consultancy Services', exchange: 'NSE' },
    { symbol: 'INFY', name: 'Infosys Limited', exchange: 'NSE' },
    { symbol: 'TATAMOTORS', name: 'Tata Motors', exchange: 'NSE' },
    { symbol: 'HDFCBANK', name: 'HDFC Bank', exchange: 'NSE' },
    { symbol: 'ICICIBANK', name: 'ICICI Bank', exchange: 'NSE' }
  ];

  activeTab = signal<DashboardTab>('assets');
  availableStocks = signal<Stock[]>([]);
  selectedSymbol = signal<string>('RELIANCE');
  selectedExchange = signal<string>('NSE');
  searchQuery = signal<string>('');

  // Track loaded symbol per tab to enable on-demand lazy loading
  private loadedShareholdingSymbol: string = '';
  private loadedCashflowSymbol: string = '';
  private loadedNewsSymbol: string = '';
  private loadedAssetsSymbol: string = '';

  // Shareholding State
  shareholdingResponse = signal<StockShareholdingResponse | null>(null);
  shareholdingLoadingState = signal<LoadingState>('idle');
  shareholdingErrorMessage = signal<string>('');
  isShareholdingRefreshing = signal<boolean>(false);

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

  // Typeahead search
  searchResults = signal<Company[]>([]);
  isSearching = signal<boolean>(false);
  private searchSubject = new Subject<string>();
  private searchSubscription?: Subscription;

  ngOnInit(): void {
    this.loadAvailableStocks();
    this.fetchActiveTabData(false);
    this.setupSearch();
  }

  ngOnDestroy(): void {
    this.searchSubscription?.unsubscribe();
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

  setTab(tab: DashboardTab): void {
    this.activeTab.set(tab);
    const currentSymbol = this.selectedSymbol();
    if (tab === 'shareholding' && this.loadedShareholdingSymbol !== currentSymbol) {
      this.fetchShareholding(false);
    } else if (tab === 'cashflow' && this.loadedCashflowSymbol !== currentSymbol) {
      this.fetchCashflow(false);
    } else if (tab === 'news' && this.loadedNewsSymbol !== currentSymbol) {
      this.fetchNews(false);
    }
  }

  selectStock(symbol: string, exchange: string = 'NSE'): void {
    const cleanSymbol = symbol.toUpperCase();
    const cleanExchange = exchange.toUpperCase();
    this.selectedSymbol.set(cleanSymbol);
    this.selectedExchange.set(cleanExchange);
    this.searchQuery.set('');
    this.searchResults.set([]);

    // Invalidate per-tab caches for the old symbol
    this.loadedShareholdingSymbol = '';
    this.loadedCashflowSymbol = '';
    this.loadedNewsSymbol = '';

    // Fetch ONLY the currently active tab's data
    this.fetchActiveTabData(false);
  }

  toggleExchange(exchange: string): void {
    if (this.selectedExchange() !== exchange) {
      this.selectedExchange.set(exchange);
      this.searchQuery.set('');
      this.searchResults.set([]);
      this.loadedShareholdingSymbol = '';
      this.loadedCashflowSymbol = '';
      this.loadedNewsSymbol = '';
      this.fetchActiveTabData(false);
    }
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
      this.loadedShareholdingSymbol = '';
      this.loadedCashflowSymbol = '';
      this.loadedNewsSymbol = '';
      this.fetchActiveTabData(false);
    }
  }

  selectSearchResult(company: Company): void {
    this.searchQuery.set('');
    this.searchResults.set([]);
    this.selectStock(company.symbol, this.selectedExchange());
  }

  fetchActiveTabData(isRefresh: boolean): void {
    if (this.activeTab() === 'shareholding') {
      this.fetchShareholding(isRefresh);
    } else if (this.activeTab() === 'cashflow') {
      this.fetchCashflow(isRefresh);
    } else if (this.activeTab() === 'assets') {
      this.fetchAssets(isRefresh);
    } else {
      this.fetchNews(isRefresh);
    }
  }

  fetchAssets(isRefresh: boolean): void {
    const symbol = this.selectedSymbol();
    if (!symbol) return;

    if (isRefresh) {
      this.isAssetsRefreshing.set(true);
    } else {
      this.assetsLoadingState.set('loading');
    }

    this.assetsErrorMessage.set('');

    this.financialsService.getBalanceSheet(symbol).subscribe({
      next: (response) => {
        this.assetsResponse.set(response);
        this.loadedAssetsSymbol = symbol;
        this.assetsLoadingState.set('success');
        this.isAssetsRefreshing.set(false);
      },
      error: (err) => {
        console.error('Error fetching assets:', err);
        this.assetsErrorMessage.set(err.error?.message || 'Could not fetch asset growth data. Please try again.');
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
        this.loadedShareholdingSymbol = symbol;
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
          err.error?.detail || err.error?.message || 'Failed to retrieve shareholding data from BharatStock. Please try again.'
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
        this.loadedCashflowSymbol = symbol;
        if (data.summary && data.history && data.history.length > 0) {
          this.cashflowLoadingState.set('success');
        } else {
          this.cashflowLoadingState.set('empty');
        }
      },
      error: (err) => {
        this.isCashflowRefreshing.set(false);
        this.cashflowLoadingState.set('error');
        this.cashflowErrorMessage.set(
          err.error?.detail || err.error?.message || 'Failed to retrieve cash flow and financial data from BharatStock. Please try again.'
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
        this.loadedNewsSymbol = symbol;
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
          err.error?.detail || err.error?.message || 'Failed to retrieve news from backend. Please try again.'
        );
      }
    });
  }
}
