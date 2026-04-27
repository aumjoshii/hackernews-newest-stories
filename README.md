# Hacker News Newest Stories

A full-stack application that displays the newest stories from the
[Hacker News API](https://github.com/HackerNews/API) with search and paging.
The backend is a **C# / .NET 9** Web API that caches and serves story data; the
frontend is an **Angular 17** standalone-component application.

```
hackernews-app/
├── backend/          # .NET 9 Web API
│   ├── HackerNews.Api/        # The REST API
│   └── HackerNews.Api.Tests/  # xUnit unit + integration tests
└── frontend/         # Angular 17 application + Karma/Jasmine tests
```

---

## Prerequisites

| Tool       | Version |
|------------|---------|
| .NET SDK   | 9.0+    |
| Node.js    | 18.13+ or 20.9+ |
| npm        | 9+      |
| Angular CLI (optional, can use `npx`) | 17 |
| Google Chrome | latest — required by Karma to run frontend tests (`npm test` / `npm run test:ci`) |

---

## Running locally

### Backend

```bash
cd backend
dotnet restore
dotnet run --project HackerNews.Api
```

The API listens on `http://localhost:5000`. Swagger UI is available at
`http://localhost:5000/swagger` in Development.

Smoke-test:
```bash
curl "http://localhost:5000/api/stories/newest?page=1&pageSize=5"
```

### Frontend

```bash
cd frontend
npm install   # or: npm ci   (a package-lock.json is committed)
npm start
```

The dev server runs on `http://localhost:4200`. CORS is preconfigured on the
backend to allow this origin.

### Building

```bash
npm run build       # development build (uses environment.ts → http://localhost:5000/api)
npm run build:prod  # production build (uses environment.prod.ts → /api relative path)
```

The default `ng build` target is the **development** configuration, which keeps
`apiBaseUrl` pointed at the local backend. Use `build:prod` only when the
frontend will be served from the same origin as the API.

---

## Running the tests

### Backend (xUnit + Moq + WebApplicationFactory)

```bash
cd backend
dotnet test
```

This runs three suites:

* `Services/HackerNewsServiceTests` — service logic, paging, search, caching, error handling
* `Controllers/StoriesControllerTests` — input validation and routing
* `Integration/StoriesEndpointTests` — full HTTP pipeline via `WebApplicationFactory<Program>`

### Frontend (Karma + Jasmine)

```bash
cd frontend
npm test            # interactive
npm run test:ci     # headless, single run
```

Covered specs:

* `services/hacker-news.service.spec.ts` — HTTP contract, search/paging params
* `components/story-item/story-item.component.spec.ts` — link vs. no-link rendering, domain extraction
* `components/pager/pager.component.spec.ts` — disabled states, emit semantics
* `app.component.spec.ts` — end-to-end component behavior, search debouncing, error handling, page-reset on search

---

## Architecture overview

### Backend

```
Controllers/StoriesController  ──▶  IHackerNewsService  ──▶  HackerNewsService
                                         (DI)                       │
                                                                    ▼
                                                          IMemoryCache + HttpClient
                                                                    │
                                                                    ▼
                                                         hacker-news.firebaseio.com
```

Key design choices:

1. **Two-layer caching.** The list of newest IDs is cached for 60s (it changes
   constantly); individual story payloads are cached for 10 minutes (they rarely
   change once published). This minimizes upstream calls while keeping the
   "newest" feed fresh.
2. **Bounded parallelism.** Story details are fetched in parallel using a
   `SemaphoreSlim` to cap concurrent HTTP calls, avoiding rate-limiting pressure
   on the upstream API.
3. **Server-side filtering and paging.** The frontend never sees the full 500-item
   ID list — the API filters by title and pages the result before sending it.
4. **Typed `HttpClient` via `IHttpClientFactory`** with options bound from
   `appsettings.json` so cache TTLs, base URL, and concurrency are configurable
   without recompiling.
5. **`IHackerNewsService` interface** so the controller depends on a contract,
   making controller tests trivial and integration tests hermetic.

### Frontend

* **Standalone components** — no `NgModule`, using Angular 17's modern bootstrap.
* **Reactive search** with `debounceTime(300)` + `distinctUntilChanged` to avoid
  spamming the API as the user types.
* **`switchMap`** so a new request cancels any in-flight request — the UI never
  shows stale results from a previous query.
* **Page reset on search** — typing a new search term snaps back to page 1.
* **Defensive rendering** — stories with no URL render as plain text instead
  of broken hyperlinks (per the requirements).

---

## API reference

`GET /api/stories/newest`

| Query param | Type    | Default | Notes                               |
|-------------|---------|---------|-------------------------------------|
| `page`      | int     | 1       | 1-based                              |
| `pageSize`  | int     | 20      | Clamped to [1, 100]                  |
| `search`    | string  | (none)  | Case-insensitive title contains      |

The working set of newest IDs is capped (default 200, configurable via
`HackerNews:MaxNewestIds`) so that cold-cache search calls never fan out into
500 upstream fetches. Each individual upstream story call is bounded by
`HackerNews:PerStoryTimeoutSeconds` (default 10s) so a single hung response
can't dominate a page request.

**200 OK** response shape:
```json
{
  "items": [
    { "id": 1, "title": "...", "url": "https://...", "by": "user",
      "score": 42, "time": 1700000000, "type": "story" }
  ],
  "totalCount": 487,
  "page": 1,
  "pageSize": 20,
  "totalPages": 25
}
```

`GET /health` — returns `{ "status": "healthy" }`.

---

## AI Tool Usage

This challenge required the use of AI-assisted development tools. The entire
solution was built in collaboration with **Claude (Anthropic)**, used as a
pair-programmer for planning, code generation, refactoring, and writing tests.

### How AI was used at each stage

#### 1. Planning & design

**Prompt 1 (initial decomposition):**
> Read the challenge spec. Before writing any code, propose an architecture:
> what are the components on each side, where does caching live, how do we
> handle search and paging given that the HN API returns up to 500 newest IDs
> with no built-in filtering, and what tests do we want?

**Outcome:** This produced a written plan that I evaluated and **modified** in
two important ways:
- The first draft suggested caching the *full hydrated list* of 500 stories.
  I pushed back: that wastes memory and forces a 500-item refresh every time
  the ID list expires. **Rejected** in favor of two-layer caching: short TTL
  on the ID list, longer TTL on individual stories keyed by ID.
- The first draft suggested doing search *client-side*. **Rejected** — that
  requires the API to ship all 500 stories to the browser per request.
  Server-side filtering is cheaper and lets the API control the data envelope.

**Prompt 2 (HN API quirks):**
> What edge cases of the Hacker News API should we handle? Specifically: do
> all items in `newstories` have URLs? What happens if `item/{id}.json` returns
> null? Are deleted items in the list?

**Outcome:** Confirmed that Ask HN posts have `url: null`, that some items can
be `null` if deleted, and that fields like `descendants` may be absent. **Accepted**
and translated into the nullable properties on the `Story` model and the
`continue past 404` behavior in `GetStoryAsync`.

**Prompt 2a (HttpClient lifecycle & connection pooling):**
> For the typed `HttpClient` registered via `AddHttpClient<I, T>`, walk me
> through who owns its lifetime, what `SocketsHttpHandler`'s default pooling
> behavior gives us, and whether fanning out ~200 stories with parallelism 20
> introduces any handler-rotation or DNS-pinning concern in a long-running
> deployment.

**Outcome:** Confirmed the typed-client registration produces a transient
`HackerNewsService` over a pooled `HttpMessageHandler` rotated every two
minutes (the `IHttpClientFactory` default), which prevents both the classic
"socket exhaustion via `new HttpClient()`" and the lesser-known "stale DNS
because handlers live forever" problems. **Accepted** as-is — no extra
configuration needed at this scale, but the answer is documented here so the
choice isn't accidentally regressed in a refactor.

#### 2. Implementation

**Prompt 3 (caching strategy):**
> Generate a `HackerNewsService` that caches the newest IDs for a short TTL
> and individual story payloads for a longer TTL, fetches story details with
> bounded parallelism, and supports server-side title search.

**Outcome:** The first draft used `Task.WhenAll` over the entire ID list with no
concurrency cap. I **modified** this to add a `SemaphoreSlim` — fetching 500
stories in parallel against a public API is impolite at best and rate-limited
at worst. The `MaxParallelStoryFetches` option is the result.

A second issue with the first draft: it didn't preserve the original ID order
after the parallel fetches resolved (Task.WhenAll returns results in input
order, but a story could be `null` and skip). I **modified** the code to
re-order results to match the original ID list, which is what "newest" semantically
requires.

**Prompt 3a (cancellation & timeout semantics):**
> Two cancellation tokens combine in `GetStoryAsync` — the request-aborted
> token from the controller and the per-story timeout linked CTS. Describe
> the propagation rules: when the client disconnects mid-page, when one
> story times out while others succeed, and when upstream returns 503.
> Should any of these surface as non-200 to the frontend, or always 200
> with partial results?

**Outcome:** The AI distinguished between an `OperationCanceledException`
whose token *is* the caller's (caller cancelled — the whole request should
unwind) versus one whose token is the per-story linked CTS (upstream timeout
— log and skip, return partial results). **Accepted with modification.** The
explicit `catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) throw;`
clause encodes that distinction; everything else is swallowed and logged so a
single bad story can't poison a page.

**Prompt 3b (JSON deserialization correctness):**
> A `Story.url` is `string?` and the upstream JSON sometimes omits the field
> entirely (text posts) or sets it to `null`. Confirm `System.Text.Json`'s
> default behavior produces `null` in both cases without any opt-in, and
> that no `JsonSerializerOptions.DefaultIgnoreCondition` tuning is needed.

**Outcome:** Confirmed: with `[JsonPropertyName]` attributes and a nullable
reference type, both "missing key" and "explicit null" deserialize to `null`
on the property. No round-trip is performed (we don't serialize back), so
`DefaultIgnoreCondition` is irrelevant here. **Accepted** — saved a YAGNI
configuration knob.

**Prompt 4 (Angular search debouncing):**
> Wire the search input to a backend call with proper debouncing and
> cancellation of in-flight requests when the user types another character.

**Outcome:** The AI-generated code used a single `searchControl.valueChanges`
pipeline with `switchMap`. **Modified** to split this into two streams: one
for search-control changes (which resets the page), and a separate `request$`
Subject that paging events also push into. This way both code paths use the
same `switchMap` (so paging clicks also cancel in-flight requests), and the
"reset page on search" behavior is in one obvious place.

**Prompt 5 (component structure):**
> Should `StoryItemComponent` and `PagerComponent` be separate or just inline
> in the AppComponent template?

**Outcome:** I argued for separation; the AI agreed. **Accepted.** It makes
unit-testing `StoryItemComponent`'s no-URL branch and `PagerComponent`'s
disabled-state logic far cleaner than testing them through the parent.

**Prompt 5a (UI accessibility audit):**
> Audit the Angular UI for accessibility. Specifically: keyboard navigation
> on the pager, focus management when the result list re-renders after a
> search, screen-reader announcement of "showing X–Y of N" when results
> change, and color contrast on the orange `#ff6600` header against the
> `#f6f6ef` page background.

**Outcome:** Four findings: (1) prev/next buttons need `aria-label` —
**Accepted**, already in place. (2) the result-count line should be an
`aria-live="polite"` region so screen readers announce updates without
hijacking focus — **Accepted** for a follow-up. (3) auto-moving focus to the
first story after a search would be hostile to screen-reader users mid-read;
better to leave focus and rely on the live region — **Rejected** the
auto-focus suggestion. (4) `#ff6600` on `#f6f6ef` passes WCAG AA at the
header's 24px size but would fail at body text — **Accepted** as a known
constraint of the Hacker News brand palette, not a regression.

#### 3. Debugging & iteration

**Prompt 6 (test reasoning):**
> In my AppComponent test, after `onPageChange(2)` then `searchControl.setValue('foo')`,
> what's the request order, and does `tick(300)` flush both?

**Outcome:** Walked through the RxJS marble-diagram: `onPageChange` fires
synchronously, `setValue` enters the debounced pipeline. The first request
goes out immediately; only the second waits for `tick(300)`. **Accepted** — the
test was rewritten to flush the page-2 request first, then advance the clock,
then flush the search request.

**Prompt 7 (test isolation):**
> Suggest a way to test the controller without depending on real HTTP calls
> to Hacker News, and a way to integration-test the API end-to-end without
> ever hitting the upstream.

**Outcome:** Two-layer test strategy: `Moq<IHackerNewsService>` for controller
unit tests, `WebApplicationFactory<Program>` with the same mock injected for
integration tests. **Accepted** — this is the canonical pattern for ASP.NET
Core 6+, and ensures CI runs are deterministic and offline-safe.

**Prompt 7a (test determinism around debounce boundaries):**
> The fakeAsync test for search debouncing uses `tick(299)` followed by
> `tick(1)`. Why split it instead of `tick(300)` directly? Is there a
> non-flaky way to express "advance to just before the boundary, then
> cross it"?

**Outcome:** Confirmed the split is load-bearing: it pins down both sides
of the contract — *no request fires before 300ms* and *exactly one fires
at 300ms*. Collapsing to `tick(300)` would let a regression that fires at
250ms slip through. **Accepted as-is.** The test isn't flaky; it's
asserting a precise temporal boundary, which is exactly what fakeAsync is
for.

**Prompt 7b (concurrency invariant under parallel hydration):**
> Prove (or disprove) that `HydrateStoriesAsync` preserves the order of
> the input ID list in its output, given that fetches resolve in arbitrary
> order. If it doesn't, what's the smallest test that would catch a
> regression?

**Outcome:** Walked through `Task.WhenAll`'s contract: it returns results
positionally aligned with the input task array, not with completion order —
so order *is* preserved through `WhenAll`. The risk lives in the subsequent
`Where(s => s is not null)` filter, which can shift positions if any story
is null. The caller in `GetNewestStoriesAsync` re-projects against the
original `pageIds` list to lock the order, which is the canonical fix.
**Accepted** — and the test
`GetNewestStoriesAsync_returns_first_page_in_id_order` asserts
`ContainInOrder(1, 2, 3, 4, 5)` precisely to catch a future regression.

#### 4. Suggestions I rejected

* **Using `IDistributedCache` (Redis) from the start.** Overkill for this
  exercise — `IMemoryCache` is per-instance but for a single-app deployment it's
  sufficient and ships with the framework. I'd revisit if scaling out.
* **`HttpClientFactory` named clients vs typed clients.** The AI initially
  generated a named client. **Modified** to a typed client (`AddHttpClient<I, T>`)
  because it gives strongly-typed registration and requires less wiring.
* **Caching at the controller level via `[ResponseCache]`.** This would have
  cached entire HTTP responses including the search query, which works but
  bloats the cache and ignores the ID/story split. Service-level caching is
  more reusable.
* **A dedicated `SearchPipe` in Angular** to filter on the client. Same reason
  as in planning — the server is the right place to filter.

### Engineering judgment summary

AI handled most of the boilerplate well — `HttpClient` setup, RxJS pipelines,
xUnit fixtures, Karma config. The places where it needed correction were
mostly architectural decisions (where caching lives, where filtering happens,
how to bound concurrency) and subtle correctness issues (preserving order
across parallel calls, the page-reset semantics on search). I treated every
generated piece as a draft to evaluate, not a finished answer.

---

### Iteration log: review & hardening pass

After the first build was complete, I asked the AI to act as a reviewer and
identify how the project would fail when handed to a fresh evaluator.

**Prompt 8 (failure-mode review):**
> Review the project as if you were a reviewer cloning it on a fresh machine.
> Where will it fail? Be concrete.

**Outcome:** The review surfaced eight concrete issues, ranked by likelihood
of biting a reviewer. I worked through them one by one:

* **No `package-lock.json` committed.** **Accepted.** Generated via
  `npm install --package-lock-only` and committed so `npm ci` works.
* **Chrome required for Karma but not in Prerequisites.** **Accepted.** Added
  to the Prerequisites table.
* **`ng build` defaulted to `production`,** which swaps in the relative `/api`
  path and breaks against `localhost:5000`. **Accepted.** Switched
  `defaultConfiguration` to `development` and added a `build:prod` script for
  the explicit production build.
* **No `https` launch profile.** **Accepted.** Added an `https` profile so
  Visual Studio on Windows users get a working default.
* **CORS env-var requirement was buried in deploy notes.** **Accepted.**
  Promoted to a "Required" callout with a concrete `az` CLI example.
* **Search hydrates all 500 newest IDs on a cold cache,** risking the 30-second
  HttpClient timeout. **Accepted with modification.** Added two new options:
  `MaxNewestIds` (default 200) caps the working set, and `PerStoryTimeoutSeconds`
  (default 10) bounds each upstream call via a linked `CancellationTokenSource`.
  Added `GetNewestStoriesAsync_caps_working_set_to_MaxNewestIds` to lock the
  behavior in.
* **Caller cancellation propagation in `GetStoryAsync`.** Originally a single
  catch swallowed `OperationCanceledException`. **Modified** to propagate
  caller-initiated cancellation while still treating per-call timeouts as
  "skip this one and move on."
* **`.DS_Store` committed.** **Accepted.** Removed.

**Prompt 9 (target framework mismatch):**
> Reviewer's machine has .NET 6 and .NET 9 SDKs but not .NET 8. Project targets
> net8.0 and won't run.

**Outcome:** The AI offered two paths — install .NET 8 vs retarget. **Accepted**
the retarget option. Bumped `TargetFramework` to `net9.0` on both projects and
aligned package versions (`Microsoft.AspNetCore.Mvc.Testing` 9.0.0,
`Microsoft.Extensions.Caching.Memory` 9.0.0, etc.).

**Prompt 10 (page-size dropdown bug):**
> The page-size dropdown shows "10 / page" visually but the API request uses
> pageSize=20 — what's wrong?

**Outcome:** Diagnosed correctly: `<select [value]="pageSize">` doesn't bind
through Angular's select-control directive without `ngModel` or a reactive
form, so the visual default falls back to the first `<option>` while the model
keeps its `20`. **Accepted with modification.** Switched to per-option
`[selected]="opt === pageSize"` (explicit, no extra directive) and changed the
default `pageSize` to `10` to match the user's intent. Updated
`app.component.spec.ts` accordingly.

### What I rejected in this pass

* **Adding `@angular/forms` `FormControl` for `pageSize`.** Tempting for
  symmetry with `searchControl`, but the dropdown has 3 hard-coded options and
  no validation — `[selected]` is simpler and the test still asserts the
  emitted request param.
* **Switching to `IDistributedCache` to "fix" the cold-cache fan-out.** The
  cap + per-call timeout solves the actual symptom (request times out under
  load) without introducing Redis as a deployment dependency. I'd revisit if
  scaling beyond a single instance.
* **Adding retry-with-backoff inside `GetStoryAsync`.** Each upstream call now
  has a 10-second deadline and a single failure already returns null without
  killing the page. Retries would multiply tail latency for marginal benefit
  on a public read API; not worth it here.

