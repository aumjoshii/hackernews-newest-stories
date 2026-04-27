import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { By } from '@angular/platform-browser';
import { AppComponent } from './app.component';
import { HackerNewsService } from './services/hacker-news.service';
import { PagedResult, Story } from './models/story.model';
import { environment } from '../environments/environment';

describe('AppComponent', () => {
  let fixture: ComponentFixture<AppComponent>;
  let component: AppComponent;
  let httpMock: HttpTestingController;

  const buildResponse = (overrides: Partial<PagedResult<Story>> = {}): PagedResult<Story> => ({
    items: [
      { id: 1, title: 'First story', url: 'https://a.com', by: 'a', score: 10, time: 0 },
      { id: 2, title: 'Second story', url: null, by: 'b', score: 5, time: 0 }
    ],
    totalCount: 40,
    page: 1,
    pageSize: 10,
    totalPages: 4,
    ...overrides
  });

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AppComponent, HttpClientTestingModule],
      providers: [HackerNewsService]
    }).compileComponents();

    fixture = TestBed.createComponent(AppComponent);
    component = fixture.componentInstance;
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  function flushInitialRequest(response = buildResponse()) {
    fixture.detectChanges(); // triggers ngOnInit which kicks off the initial request
    const req = httpMock.expectOne(r => r.url === `${environment.apiBaseUrl}/stories/newest`);
    req.flush(response);
    fixture.detectChanges();
    return req;
  }

  it('loads the first page of newest stories on init', () => {
    const req = flushInitialRequest();

    expect(req.request.params.get('page')).toBe('1');
    expect(req.request.params.get('pageSize')).toBe('10');
    expect(component.result?.items.length).toBe(2);
  });

  it('displays the rendered stories in the DOM', () => {
    flushInitialRequest();

    const items = fixture.debugElement.queryAll(By.css('app-story-item'));
    expect(items.length).toBe(2);
    expect(fixture.nativeElement.textContent).toContain('First story');
    expect(fixture.nativeElement.textContent).toContain('Second story');
  });

  it('shows an empty-state message when no results are returned', () => {
    flushInitialRequest(buildResponse({ items: [], totalCount: 0, totalPages: 0 }));

    const empty = fixture.debugElement.query(By.css('[data-testid="empty"]'));
    expect(empty).toBeTruthy();
    expect(empty.nativeElement.textContent).toContain('No stories found');
  });

  it('shows an error message when the API call fails', () => {
    fixture.detectChanges();
    const req = httpMock.expectOne(r => r.url === `${environment.apiBaseUrl}/stories/newest`);
    req.error(new ProgressEvent('Network error'), { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();

    const err = fixture.debugElement.query(By.css('[data-testid="error"]'));
    expect(err).toBeTruthy();
    expect(err.nativeElement.textContent).toContain('Failed to load');
  });

  it('debounces the search input and includes the search param', fakeAsync(() => {
    flushInitialRequest();

    component.searchControl.setValue('ang');
    component.searchControl.setValue('angu');
    component.searchControl.setValue('angular');
    tick(299); // not yet past the 300ms debounce
    httpMock.expectNone(r => r.params.get('search') === 'angular');

    tick(1); // cross the debounce threshold
    const req = httpMock.expectOne(r => r.params.get('search') === 'angular');
    expect(req.request.params.get('page')).toBe('1');
    req.flush(buildResponse({ totalCount: 1, totalPages: 1, items: [
      { id: 99, title: 'Angular release', url: 'https://x', by: 'q', score: 1, time: 0 }
    ] }));
  }));

  it('resets to page 1 when the search term changes', fakeAsync(() => {
    flushInitialRequest();

    // navigate to page 2
    component.onPageChange(2);
    const page2Req = httpMock.expectOne(r => r.params.get('page') === '2');
    page2Req.flush(buildResponse({ page: 2 }));
    fixture.detectChanges();
    expect(component.page).toBe(2);

    // type a search — should snap back to page 1
    component.searchControl.setValue('foo');
    tick(300);

    const searchReq = httpMock.expectOne(r => r.params.get('search') === 'foo');
    expect(searchReq.request.params.get('page')).toBe('1');
    searchReq.flush(buildResponse());
  }));

  it('refetches with the new page when onPageChange is called', () => {
    flushInitialRequest();

    component.onPageChange(2);

    const req = httpMock.expectOne(r => r.params.get('page') === '2');
    req.flush(buildResponse({ page: 2 }));
    expect(component.page).toBe(2);
  });

  it('changes page size and resets to page 1', () => {
    flushInitialRequest();
    component.page = 3; // pretend we were deep in the list

    component.onPageSizeChange('50');

    const req = httpMock.expectOne(r =>
      r.params.get('pageSize') === '50' && r.params.get('page') === '1');
    req.flush(buildResponse({ pageSize: 50 }));
    expect(component.pageSize).toBe(50);
    expect(component.page).toBe(1);
  });

  it('computes the displayed range correctly', () => {
    flushInitialRequest(buildResponse({
      page: 2, pageSize: 20, totalCount: 45, totalPages: 3
    }));
    expect(component.rangeStart).toBe(21);
    expect(component.rangeEnd).toBe(40);
  });

  it('caps rangeEnd at totalCount on the last page', () => {
    flushInitialRequest(buildResponse({
      page: 3, pageSize: 20, totalCount: 45, totalPages: 3
    }));
    expect(component.rangeStart).toBe(41);
    expect(component.rangeEnd).toBe(45);
  });
});
