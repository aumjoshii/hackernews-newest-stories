import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';

/**
 * Reusable paging control. Pure presentational — emits page numbers, owns no state.
 */
@Component({
  selector: 'app-pager',
  standalone: true,
  imports: [CommonModule],
  template: `
    <nav class="pager" aria-label="Pagination" *ngIf="totalPages > 1">
      <button
        type="button"
        class="pager-btn"
        (click)="goTo(currentPage - 1)"
        [disabled]="currentPage <= 1"
        aria-label="Previous page">
        ← Prev
      </button>

      <span class="pager-info" data-testid="pager-info">
        Page {{ currentPage }} of {{ totalPages }}
      </span>

      <button
        type="button"
        class="pager-btn"
        (click)="goTo(currentPage + 1)"
        [disabled]="currentPage >= totalPages"
        aria-label="Next page">
        Next →
      </button>
    </nav>
  `,
  styles: [`
    .pager {
      display: flex;
      align-items: center;
      gap: 1rem;
      justify-content: center;
      padding: 1rem 0;
    }
    .pager-btn {
      padding: 0.5rem 1rem;
      border: 1px solid #ccc;
      background: white;
      cursor: pointer;
      border-radius: 4px;
      font-size: 0.9rem;
    }
    .pager-btn:hover:not(:disabled) { background: #f0f0f0; }
    .pager-btn:disabled { opacity: 0.5; cursor: not-allowed; }
    .pager-info { font-size: 0.9rem; color: #555; }
  `]
})
export class PagerComponent {
  @Input({ required: true }) currentPage = 1;
  @Input({ required: true }) totalPages = 1;
  @Output() pageChange = new EventEmitter<number>();

  goTo(page: number): void {
    if (page < 1 || page > this.totalPages || page === this.currentPage) {
      return;
    }
    this.pageChange.emit(page);
  }
}
