import { ComponentFixture, TestBed } from '@angular/core/testing';

import { SharedApi } from './shared-api';

describe('SharedApi', () => {
  let component: SharedApi;
  let fixture: ComponentFixture<SharedApi>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SharedApi],
    }).compileComponents();

    fixture = TestBed.createComponent(SharedApi);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
