import { ComponentFixture, TestBed } from '@angular/core/testing';
import { StockShareholdingCardComponent } from './stock-shareholding-card.component';
import { StockShareholdingResponse } from '../../models/stock-shareholding.model';

describe('StockShareholdingCardComponent', () => {
  let component: StockShareholdingCardComponent;
  let fixture: ComponentFixture<StockShareholdingCardComponent>;

  const mockResponse: StockShareholdingResponse = {
    stockId: 1,
    symbol: 'RELIANCE',
    exchange: 'NSE',
    companyName: 'Reliance Industries Limited',
    currentPeriod: {
      period: 'Q1 FY2026',
      periodKey: '2025-06-30',
      periodDate: '2025-06-30',
      promoter: 50.25,
      fii: 18.20,
      dii: 16.30,
      public: 15.25,
      total: 100.00,
      dataAsOf: '30 Jun 2025'
    },
    previousPeriod: {
      period: 'Q4 FY2025',
      periodKey: '2025-03-31',
      periodDate: '2025-03-31',
      promoter: 49.80,
      fii: 19.10,
      dii: 15.80,
      public: 15.30,
      total: 100.00,
      dataAsOf: '31 Mar 2025'
    },
    change: {
      promoter: 0.45,
      fii: -0.90,
      dii: 0.50,
      public: -0.05
    },
    relativeChange: {
      promoter: 0.90,
      fii: -4.71,
      dii: 3.16,
      public: -0.33
    },
    history: [
      {
        period: 'Q1 FY2026',
        periodKey: '2025-06-30',
        promoter: 50.25,
        fii: 18.20,
        dii: 16.30,
        public: 15.25,
        total: 100.00
      }
    ],
    dataAsOf: '30 Jun 2025',
    source: 'BharatStock',
    lastSyncedAt: new Date().toISOString()
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [StockShareholdingCardComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(StockShareholdingCardComponent);
    component = fixture.componentInstance;
  });

  it('should create the component', () => {
    expect(component).toBeTruthy();
  });

  it('should format percentage-point changes with explicit pp suffix', () => {
    expect(component.formatPp(0.45)).toBe('+0.45 pp');
    expect(component.formatPp(-0.90)).toBe('-0.90 pp');
    expect(component.formatPp(null)).toBe('—');
  });

  it('should format relative changes with explicit relative suffix', () => {
    expect(component.formatRelative(0.90)).toBe('+0.90% relative');
    expect(component.formatRelative(-4.71)).toBe('-4.71% relative');
  });

  it('should display holding cards when success state is active', async () => {
    component.loadingState = 'success';
    component.shareholding = mockResponse;
    fixture.detectChanges();
    await fixture.whenStable();

    const compiled = fixture.nativeElement as HTMLElement;
    const cards = compiled.querySelectorAll('.metric-card');
    expect(cards.length).toBe(4);
    expect(compiled.textContent).toContain('50.25%');
    expect(compiled.textContent).toContain('+0.45 pp');
  });

  it('should not display ownership distribution section', async () => {
    component.loadingState = 'success';
    component.shareholding = mockResponse;
    fixture.detectChanges();
    await fixture.whenStable();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.distribution-section')).toBeNull();
    expect(compiled.textContent).not.toContain('Ownership Distribution');
  });

  it('should only show the first 3 history records in displayedHistory and in the table', async () => {
    const multiHistoryResponse: StockShareholdingResponse = {
      ...mockResponse,
      history: [
        { period: 'Q1 FY2026', periodKey: '2025-06-30', promoter: 50.25, total: 100 },
        { period: 'Q4 FY2025', periodKey: '2025-03-31', promoter: 49.80, total: 100 },
        { period: 'Q3 FY2025', periodKey: '2024-12-31', promoter: 49.50, total: 100 },
        { period: 'Q2 FY2025', periodKey: '2024-09-30', promoter: 49.00, total: 100 },
        { period: 'Q1 FY2025', periodKey: '2024-06-30', promoter: 48.50, total: 100 }
      ]
    };

    component.loadingState = 'success';
    component.shareholding = multiHistoryResponse;
    fixture.detectChanges();
    await fixture.whenStable();

    expect(component.displayedHistory.length).toBe(3);
    expect(component.displayedHistory[0].period).toBe('Q1 FY2026');
    expect(component.displayedHistory[2].period).toBe('Q3 FY2025');

    const compiled = fixture.nativeElement as HTMLElement;
    const rows = compiled.querySelectorAll('.history-table tbody tr');
    expect(rows.length).toBe(3);
    expect(compiled.querySelector('.history-count')?.textContent).toContain('3 Quarters Reported');
  });
});
