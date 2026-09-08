import { Component } from '@angular/core';
import { StockDashboardComponent } from './components/stock-dashboard/stock-dashboard.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [StockDashboardComponent],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App {}
