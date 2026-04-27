import { ComponentFixture, TestBed } from '@angular/core/testing';
import { StoryItemComponent } from './story-item.component';
import { Story } from '../../models/story.model';
import { By } from '@angular/platform-browser';

describe('StoryItemComponent', () => {
  let fixture: ComponentFixture<StoryItemComponent>;
  let component: StoryItemComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [StoryItemComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(StoryItemComponent);
    component = fixture.componentInstance;
  });

  it('renders a hyperlink when url is present', () => {
    component.story = {
      id: 1, title: 'Hello', url: 'https://example.com/page',
      by: 'tester', score: 42, time: 1700000000
    };
    fixture.detectChanges();

    const anchor = fixture.debugElement.query(By.css('a'));
    expect(anchor).toBeTruthy();
    expect(anchor.nativeElement.getAttribute('href')).toBe('https://example.com/page');
    expect(anchor.nativeElement.textContent.trim()).toBe('Hello');
  });

  it('renders plain text when url is missing (Ask HN style stories)', () => {
    component.story = {
      id: 2, title: 'Ask HN: Anyone using Angular 17?', url: null,
      by: 'asker', score: 5, time: 1700000000
    };
    fixture.detectChanges();

    expect(fixture.debugElement.query(By.css('a'))).toBeNull();
    const noLink = fixture.debugElement.query(By.css('[data-testid="no-link-title"]'));
    expect(noLink.nativeElement.textContent.trim()).toBe('Ask HN: Anyone using Angular 17?');
  });

  it('extracts and displays the domain from the url', () => {
    component.story = {
      id: 3, title: 'X', url: 'https://www.example.com/path',
      by: null, score: 1, time: 1700000000
    };
    fixture.detectChanges();

    expect(component.domain).toBe('example.com');
    const domainEl = fixture.debugElement.query(By.css('.story-domain'));
    expect(domainEl.nativeElement.textContent).toContain('example.com');
  });

  it('falls back to "(untitled)" when title is missing', () => {
    component.story = {
      id: 4, title: null, url: 'https://x.com',
      by: null, score: 0, time: 0
    };
    fixture.detectChanges();

    expect(fixture.debugElement.nativeElement.textContent).toContain('(untitled)');
  });

  it('renders comment count when descendants is provided', () => {
    component.story = {
      id: 5, title: 'A', url: null, by: 'me',
      score: 1, time: 0, descendants: 7
    };
    fixture.detectChanges();

    expect(fixture.debugElement.nativeElement.textContent).toContain('7 comments');
  });
});
