import { CommonModule } from '@angular/common';
import { Component, Input } from '@angular/core';
import { Story } from '../../models/story.model';

@Component({
  selector: 'app-story-item',
  standalone: true,
  imports: [CommonModule],
  template: `
    <li class="story-item">
      <div class="story-title">
        <ng-container *ngIf="story.url; else noLink">
          <a [href]="story.url" target="_blank" rel="noopener noreferrer">
            {{ story.title || '(untitled)' }}
          </a>
          <span class="story-domain" *ngIf="domain"> ({{ domain }})</span>
        </ng-container>
        <ng-template #noLink>
          <span class="no-link" data-testid="no-link-title">
            {{ story.title || '(untitled)' }}
          </span>
        </ng-template>
      </div>
      <div class="story-meta">
        {{ story.score }} points
        <span *ngIf="story.by"> by {{ story.by }}</span>
        <span *ngIf="story.descendants !== undefined && story.descendants !== null">
          | {{ story.descendants }} comments
        </span>
      </div>
    </li>
  `,
  styles: [`
    .story-item {
      padding: 0.75rem 0;
      border-bottom: 1px solid #e6e6e0;
      list-style: none;
    }
    .story-title { font-size: 1rem; line-height: 1.4; }
    .story-domain { color: #828282; font-size: 0.85rem; }
    .no-link { color: #333; }
    .story-meta {
      color: #828282;
      font-size: 0.8rem;
      margin-top: 0.25rem;
    }
  `]
})
export class StoryItemComponent {
  @Input({ required: true }) story!: Story;

  get domain(): string | null {
    if (!this.story?.url) return null;
    try {
      return new URL(this.story.url).hostname.replace(/^www\./, '');
    } catch {
      return null;
    }
  }
}
