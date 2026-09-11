import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';



export interface BalanceSheetLineItemDto {
    name: string;
    isTotal: boolean;
    values: (number | null)[];
}

export interface BalanceSheetResponseDto {
  symbol: string;
  currency?: string;
  periods: string[];
  lineItems: BalanceSheetLineItemDto[];
  assetGrowthPercentage?: number;
  consolidationType?: string;
  latestPeriodEnd?: string;
  source?: string;
  lastSyncedAt?: string;
  errorMessage?: string;
}

@Injectable({
  providedIn: 'root'
})
export class StockFinancialsService {
  private apiUrl = environment.apiUrl;

  constructor(private http: HttpClient) { }

  getBalanceSheet(symbol: string): Observable<BalanceSheetResponseDto> {
    return this.http.get<BalanceSheetResponseDto>(`${this.apiUrl}/api/stocks/balancesheet?symbol=${encodeURIComponent(symbol)}`);
  }
}
