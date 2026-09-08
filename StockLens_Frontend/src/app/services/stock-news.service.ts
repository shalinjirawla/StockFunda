import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Stock, StockNewsResponse } from '../models/stock-news.model';
import { environment } from '../../environments/environment';

@Injectable({
  providedIn: 'root'
})
export class StockNewsService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiUrl;

  /**
   * Fetch all registered stocks for the selector/search dropdown.
   */
  getStocks(): Observable<Stock[]> {
    return this.http.get<Stock[]>(`${this.baseUrl}/api/stocks`);
  }

  /**
   * Fetch latest news by Stock ID.
   */
  getNewsByStockId(stockId: number, limit = 20, page = 1, refresh = false): Observable<StockNewsResponse> {
    const params = new HttpParams()
      .set('limit', limit.toString())
      .set('page', page.toString())
      .set('refresh', refresh.toString());

    return this.http.get<StockNewsResponse>(`${this.baseUrl}/api/stocks/${stockId}/news`, { params });
  }

  /**
   * Fetch latest news by Stock Ticker Symbol and Exchange.
   */
  getNewsBySymbol(symbol: string, exchange = 'NSE', limit = 20, page = 1, refresh = false): Observable<StockNewsResponse> {
    const params = new HttpParams()
      .set('symbol', symbol)
      .set('exchange', exchange)
      .set('limit', limit.toString())
      .set('page', page.toString())
      .set('refresh', refresh.toString());

    return this.http.get<StockNewsResponse>(`${this.baseUrl}/api/stocks/news`, { params });
  }
}
