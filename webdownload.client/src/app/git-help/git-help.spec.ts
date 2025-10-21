import { ComponentFixture, TestBed } from '@angular/core/testing';

import { GitHelp } from './git-help';

describe('GitHelp', () => {
  let component: GitHelp;
  let fixture: ComponentFixture<GitHelp>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [GitHelp]
    })
    .compileComponents();

    fixture = TestBed.createComponent(GitHelp);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
