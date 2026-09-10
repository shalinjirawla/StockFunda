export interface Company {
  id: number;
  symbol: string;
  companyName: string;
  industry?: string;
  logoUrl?: string;
}

export interface Stock {
  id: number;
  symbol: string;
  companyName: string;
  exchange: string;
  industry?: string;
}

export interface StockNewsItem {
  id: number;
  title: string;
  description?: string;
  content?: string;
  sourceName: string;
  sourceUrl: string;
  articleUrl?: string;
  imageUrl?: string;
  publishedAt: string;
  category?: string;
  externalNewsId?: string;
}

export interface StockNewsResponse {
  stockId: number;
  symbol: string;
  exchange: string;
  companyName: string;
  news: StockNewsItem[];
  page: number;
  limit: number;
  lastFetchedAt?: string;
}

export type LoadingState = 'idle' | 'loading' | 'success' | 'empty' | 'error';
