export interface Story {
  id: number;
  title: string | null;
  url: string | null;
  by: string | null;
  score: number;
  time: number;
  descendants?: number | null;
  type?: string | null;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}
