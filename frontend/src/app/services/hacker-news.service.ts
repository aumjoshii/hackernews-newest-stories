import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { PagedResult, Story } from '../models/story.model';

/**
 * Thin client over the backend REST API. Kept dumb on purpose — no caching here,
 * since the backend already caches and re-doing it client-side would just hide
 * stale data.
 */
@Injectable({ providedIn: 'root' })
export class HackerNewsService {
  private readonly endpoint = `${environment.apiBaseUrl}/stories/newest`;

  constructor(private readonly http: HttpClient) {}

  getNewestStories(page: number, pageSize: number, search?: string): Observable<PagedResult<Story>> {
    let params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());

    if (search && search.trim().length > 0) {
      params = params.set('search', search.trim());
    }

    return this.http.get<PagedResult<Story>>(this.endpoint, { params });
  }
}
