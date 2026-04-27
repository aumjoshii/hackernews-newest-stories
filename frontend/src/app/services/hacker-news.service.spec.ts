import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { HackerNewsService } from './hacker-news.service';
import { PagedResult, Story } from '../models/story.model';
import { environment } from '../../environments/environment';

describe('HackerNewsService', () => {
  let service: HackerNewsService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [HackerNewsService]
    });
    service = TestBed.inject(HackerNewsService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  const mockResponse: PagedResult<Story> = {
    items: [
      { id: 1, title: 'Story 1', url: 'https://a.com', by: 'u1', score: 10, time: 1700000000 }
    ],
    totalCount: 1,
    page: 1,
    pageSize: 20,
    totalPages: 1
  };

  it('builds the request URL with page and pageSize', () => {
    service.getNewestStories(2, 25).subscribe();

    const req = httpMock.expectOne(r =>
      r.url === `${environment.apiBaseUrl}/stories/newest`
      && r.params.get('page') === '2'
      && r.params.get('pageSize') === '25'
    );
    expect(req.request.method).toBe('GET');
    expect(req.request.params.has('search')).toBe(false);
    req.flush(mockResponse);
  });

  it('includes the search parameter when provided', () => {
    service.getNewestStories(1, 20, 'angular').subscribe();

    const req = httpMock.expectOne(r => r.params.get('search') === 'angular');
    expect(req.request.params.get('search')).toBe('angular');
    req.flush(mockResponse);
  });

  it('omits empty/whitespace search terms', () => {
    service.getNewestStories(1, 20, '   ').subscribe();

    const req = httpMock.expectOne(r => r.url === `${environment.apiBaseUrl}/stories/newest`);
    expect(req.request.params.has('search')).toBe(false);
    req.flush(mockResponse);
  });

  it('returns the paged result from the API', (done) => {
    service.getNewestStories(1, 20).subscribe(result => {
      expect(result).toEqual(mockResponse);
      expect(result.items.length).toBe(1);
      done();
    });

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/stories/newest?page=1&pageSize=20`);
    req.flush(mockResponse);
  });
});
