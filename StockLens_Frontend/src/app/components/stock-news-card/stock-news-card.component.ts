import { Component, Input, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { StockNewsItem } from '../../models/stock-news.model';
import { TimeAgoPipe } from '../../pipes/time-ago.pipe';

@Component({
  selector: 'app-stock-news-card',
  standalone: true,
  imports: [CommonModule, TimeAgoPipe],
  templateUrl: './stock-news-card.component.html',
  styleUrl: './stock-news-card.component.css'
})
export class StockNewsCardComponent {
  @Input({ required: true }) newsItem!: StockNewsItem;

  readonly defaultImage = 'https://images.unsplash.com/photo-1611974789855-9c2a0a7236a3?w=800&auto=format&fit=crop&q=60';
  imageLoadedFailed = signal(false);

  onImageError(): void {
    this.imageLoadedFailed.set(true);
  }

  get displayImageUrl(): string {
    if (this.imageLoadedFailed() || !this.newsItem.imageUrl) {
      return this.defaultImage;
    }
    return this.newsItem.imageUrl;
  }

  get articleUrl(): string {
    const url = this.newsItem.articleUrl || this.newsItem.sourceUrl || '';
    return url.trim();
  }

  get hasValidArticleUrl(): boolean {
    const url = this.articleUrl.toLowerCase();
    return url.startsWith('http://') || url.startsWith('https://');
  }

  openArticle(event: Event): void {
    if (this.hasValidArticleUrl) {
      window.open(this.articleUrl, '_blank', 'noopener,noreferrer');
    }
  }
}
