import { Component, OnInit, OnDestroy, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Subject, Subscription, of } from 'rxjs';
import { debounceTime, distinctUntilChanged, switchMap, catchError } from 'rxjs/operators';
import { StockNewsService } from '../../services/stock-news.service';
import { Stock, Company, StockNewsResponse, LoadingState } from '../../models/stock-news.model';
import { StockNewsCardComponent } from '../stock-news-card/stock-news-card.component';
import { TimeAgoPipe } from '../../pipes/time-ago.pipe';

@Component({
  selector: 'app-stock-dashboard',
  standalone: true,
  imports: [CommonModule, FormsModule, StockNewsCardComponent, TimeAgoPipe],
  templateUrl: './stock-dashboard.component.html',
  styleUrl: './stock-dashboard.component.css'
})
export class StockDashboardComponent implements OnInit, OnDestroy {
  private readonly newsService = inject(StockNewsService);

  // Quick select stocks
  readonly quickStocks = [
    { symbol: 'RELIANCE', name: 'Reliance Industries', exchange: 'NSE' },
    { symbol: 'TCS', name: 'Tata Consultancy Services', exchange: 'NSE' },
    { symbol: 'INFY', name: 'Infosys Limited', exchange: 'NSE' },
    { symbol: 'TATAMOTORS', name: 'Tata Motors', exchange: 'NSE' },
    { symbol: 'HDFCBANK', name: 'HDFC Bank', exchange: 'NSE' },
    { symbol: 'ICICIBANK', name: 'ICICI Bank', exchange: 'NSE' }
  ];

  availableStocks = signal<Stock[]>([]);
  selectedSymbol = signal<string>('RELIANCE');
  selectedExchange = signal<string>('NSE');
  searchQuery = signal<string>('');

  newsResponse = signal<StockNewsResponse | null>(null);
  loadingState = signal<LoadingState>('idle');
  errorMessage = signal<string>('');
  isRefreshing = signal<boolean>(false);

  // Typeahead search
  searchResults = signal<Company[]>([]);
  isSearching = signal<boolean>(false);
  private searchSubject = new Subject<string>();
  private searchSubscription?: Subscription;

  ngOnInit(): void {
    this.loadAvailableStocks();
    this.fetchNews(false);
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

  selectStock(symbol: string, exchange: string = 'NSE'): void {
    this.selectedSymbol.set(symbol.toUpperCase());
    this.selectedExchange.set(exchange.toUpperCase());
    this.searchQuery.set('');
    this.fetchNews(false);
  }

  toggleExchange(exchange: string): void {
    if (this.selectedExchange() !== exchange) {
      this.selectedExchange.set(exchange);
      this.searchQuery.set('');
      this.searchResults.set([]);
      this.fetchNews(false);
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
      this.searchResults.set([]); // Hide dropdown
      this.fetchNews(false);
    }
  }

  selectSearchResult(company: Company): void {
    this.searchQuery.set(''); // Clear search box
    this.searchResults.set([]); // Hide dropdown
    this.selectStock(company.symbol, this.selectedExchange());
  }

  refreshNews(): void {
    this.fetchNews(true);
  }

  private fetchNews(isRefresh: boolean): void {
    if (isRefresh) {
      this.isRefreshing.set(true);
    } else {
      this.loadingState.set('loading');
    }
    this.errorMessage.set('');

    const symbol = this.selectedSymbol();
    const exchange = this.selectedExchange();

    this.newsService.getNewsBySymbol(symbol, exchange, 20, 1, isRefresh).subscribe({
      next: (data) => {
        this.newsResponse.set(data);
        this.isRefreshing.set(false);
        if (data.news && data.news.length > 0) {
          this.loadingState.set('success');
        } else {
          this.loadingState.set('empty');
        }
      },
      error: (err) => {
        this.isRefreshing.set(false);
        this.loadingState.set('error');
        this.errorMessage.set(err.error?.detail || err.error?.message || 'Failed to retrieve news from backend. Please try again.');
      }
    });
  }
}
