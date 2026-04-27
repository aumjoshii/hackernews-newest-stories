import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { PagerComponent } from './pager.component';

describe('PagerComponent', () => {
  let fixture: ComponentFixture<PagerComponent>;
  let component: PagerComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [PagerComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(PagerComponent);
    component = fixture.componentInstance;
  });

  it('hides itself when there is only one page', () => {
    component.currentPage = 1;
    component.totalPages = 1;
    fixture.detectChanges();

    expect(fixture.debugElement.query(By.css('nav.pager'))).toBeNull();
  });

  it('disables Prev on the first page', () => {
    component.currentPage = 1;
    component.totalPages = 5;
    fixture.detectChanges();

    const buttons = fixture.debugElement.queryAll(By.css('button'));
    const prev = buttons[0].nativeElement as HTMLButtonElement;
    const next = buttons[1].nativeElement as HTMLButtonElement;
    expect(prev.disabled).toBe(true);
    expect(next.disabled).toBe(false);
  });

  it('disables Next on the last page', () => {
    component.currentPage = 5;
    component.totalPages = 5;
    fixture.detectChanges();

    const buttons = fixture.debugElement.queryAll(By.css('button'));
    expect((buttons[0].nativeElement as HTMLButtonElement).disabled).toBe(false);
    expect((buttons[1].nativeElement as HTMLButtonElement).disabled).toBe(true);
  });

  it('emits pageChange with the next page number when Next is clicked', () => {
    component.currentPage = 2;
    component.totalPages = 5;
    fixture.detectChanges();

    const emitted: number[] = [];
    component.pageChange.subscribe(n => emitted.push(n));

    const buttons = fixture.debugElement.queryAll(By.css('button'));
    buttons[1].nativeElement.click();

    expect(emitted).toEqual([3]);
  });

  it('does not emit when goTo is called with out-of-range page', () => {
    component.currentPage = 1;
    component.totalPages = 3;

    const emitted: number[] = [];
    component.pageChange.subscribe(n => emitted.push(n));

    component.goTo(0);
    component.goTo(4);
    component.goTo(1); // current page

    expect(emitted).toEqual([]);
  });

  it('shows the current page info text', () => {
    component.currentPage = 3;
    component.totalPages = 10;
    fixture.detectChanges();

    const info = fixture.debugElement.query(By.css('[data-testid="pager-info"]'));
    expect(info.nativeElement.textContent.trim()).toBe('Page 3 of 10');
  });
});
