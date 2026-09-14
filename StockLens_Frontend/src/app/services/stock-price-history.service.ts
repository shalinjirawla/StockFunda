import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface PriceHistoryResponseDto {
  symbol: string;
  errorMessage?: string;
  dates: string[];
  closePrices: number[];
  volumes: number[];
  openPrices: number[];
  highPrices: number[];
  lowPrices: number[];
  source?: string;
  lastSyncedAt?: string;
}

@Injectable({
  providedIn: 'root'
})
export class StockPriceHistoryService {
  private apiUrl = environment.apiUrl;

  constructor(private http: HttpClient) { }

  getPriceHistory(symbol: string, exchange = 'NSE', refresh = false): Observable<PriceHistoryResponseDto> {
    const params = new HttpParams()
      .set('symbol', symbol)
      .set('exchange', exchange)
      .set('refresh', refresh.toString());

    return this.http.get<PriceHistoryResponseDto>(`${this.apiUrl}/api/stocks/prices`, { params });
  }

  getPriceHistoryByStockId(stockId: number, refresh = false): Observable<PriceHistoryResponseDto> {
    const params = new HttpParams().set('refresh', refresh.toString());
    return this.http.get<PriceHistoryResponseDto>(`${this.apiUrl}/api/stocks/${stockId}/prices`, { params });
  }
}
