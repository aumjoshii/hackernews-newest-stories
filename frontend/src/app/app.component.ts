import { CommonModule } from '@angular/common';
import { Component, OnDestroy, OnInit } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { Subject, debounceTime, distinctUntilChanged, switchMap, takeUntil, catchError, of, finalize, startWith } from 'rxjs';
import { PagedResult, Story } from './models/story.model';
import { HackerNewsService } from './services/hacker-news.service';
import { StoryItemComponent } from './components/story-item/story-item.component';
import { PagerComponent } from './components/pager/pager.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, StoryItemComponent, PagerComponent],
  template: `
    <div class="container">
      <header class="header">
        <h1>Hacker News — Newest Stories</h1>
        <p class="subtitle">Browse and search the latest submissions.</p>
      </header>

      <div class="controls">
        <input
          type="search"
          class="search-input"
          [formControl]="searchControl"
          placeholder="Search stories by title…"
          aria-label="Search stories"
          data-testid="search-input" />

        <select
          class="page-size-select"
          (change)="onPageSizeChange($any($event.target).value)"
          aria-label="Page size"
          data-testid="page-size-select">
          <option [value]="10" [selected]="pageSize === 10">10 / page</option>
          <option [value]="20" [selected]="pageSize === 20">20 / page</option>
          <option [value]="50" [selected]="pageSize === 50">50 / page</option>
        </select>
      </div>

      <div *ngIf="loading" class="status" data-testid="loading">Loading…</div>
      <div *ngIf="error" class="status error" data-testid="error">{{ error }}</div>

      <ng-container *ngIf="!loading && !error">
        <div *ngIf="result && result.totalCount > 0; else empty">
          <p class="result-count" data-testid="result-count">
            Showing {{ rangeStart }}–{{ rangeEnd }} of {{ result.totalCount }} stories
            <span *ngIf="searchControl.value">matching "{{ searchControl.value }}"</span>
          </p>
          <ul class="story-list">
            <app-story-item *ngFor="let s of result.items; trackBy: trackById" [story]="s"></app-story-item>
          </ul>
          <app-pager
            [currentPage]="result.page"
            [totalPages]="result.totalPages"
            (pageChange)="onPageChange($event)">
          </app-pager>
        </div>

        <ng-template #empty>
          <p class="status" data-testid="empty">No stories found.</p>
        </ng-template>
      </ng-container>
    </div>
  `,
  styles: [`
    .header {
      padding: 1rem 0;
      border-bottom: 2px solid #ff6600;
      margin-bottom: 1rem;
    }
    .header h1 { margin: 0 0 0.25rem; font-size: 1.5rem; color: #ff6600; }
    .subtitle { margin: 0; color: #555; font-size: 0.9rem; }

    .controls {
      display: flex;
      gap: 0.5rem;
      margin-bottom: 1rem;
    }
    .search-input {
      flex: 1;
      padding: 0.5rem 0.75rem;
      font-size: 0.95rem;
      border: 1px solid #ccc;
      border-radius: 4px;
    }
    .search-input:focus { outline: 2px solid #ff6600; outline-offset: -1px; }

    .page-size-select {
      padding: 0.5rem;
      border: 1px solid #ccc;
      border-radius: 4px;
      background: white;
    }

    .status {
      padding: 1rem;
      text-align: center;
      color: #666;
    }
    .status.error { color: #c0392b; }

    .result-count {
      font-size: 0.85rem;
      color: #828282;
      margin: 0.5rem 0;
    }

    .story-list {
      padding: 0;
      margin: 0;
    }
  `]
})
export class AppComponent implements OnInit, OnDestroy {
  searchControl = new FormControl('', { nonNullable: true });
  pageSize = 10;
  page = 1;

  result: PagedResult<Story> | null = null;
  loading = false;
  error: string | null = null;

  private readonly destroy$ = new Subject<void>();
  // We funnel both search-text changes and pagination changes into the same
  // request stream so each new request cancels the in-flight one (via switchMap).
  private readonly request$ = new Subject<void>();

  constructor(private readonly hn: HackerNewsService) {}

  ngOnInit(): void {
    // Reset to page 1 when the search term changes — staying on page 5 of an old
    // search while typing a new one gives confusing empty results.
    this.searchControl.valueChanges
      .pipe(debounceTime(300), distinctUntilChanged(), takeUntil(this.destroy$))
      .subscribe(() => {
        this.page = 1;
        this.request$.next();
      });

    this.request$
      .pipe(
        startWith(null),
        switchMap(() => {
          this.loading = true;
          this.error = null;
          return this.hn
            .getNewestStories(this.page, this.pageSize, this.searchControl.value)
            .pipe(
              catchError(err => {
                console.error('Failed to load stories', err);
                this.error = 'Failed to load stories. Please try again.';
                return of(null);
              }),
              finalize(() => (this.loading = false))
            );
        }),
        takeUntil(this.destroy$)
      )
      .subscribe(res => {
        this.result = res;
      });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  onPageChange(page: number): void {
    this.page = page;
    this.request$.next();
  }

  onPageSizeChange(size: string | number): void {
    const parsed = typeof size === 'string' ? parseInt(size, 10) : size;
    if (Number.isFinite(parsed) && parsed > 0) {
      this.pageSize = parsed;
      this.page = 1;
      this.request$.next();
    }
  }

  trackById(_: number, story: Story): number {
    return story.id;
  }

  get rangeStart(): number {
    if (!this.result || this.result.totalCount === 0) return 0;
    return (this.result.page - 1) * this.result.pageSize + 1;
  }

  get rangeEnd(): number {
    if (!this.result) return 0;
    return Math.min(this.result.page * this.result.pageSize, this.result.totalCount);
  }
}
