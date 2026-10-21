import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { YurtApiService } from 'shared-api';
import { TranslatePipe } from '../../core/translate.pipe';
import { environment } from '../../../environments/environment';

@Component({
  selector: 'app-group-order-join',
  standalone: true,
  imports: [CommonModule, FormsModule, TranslatePipe],
  templateUrl: './group-order-join.component.html',
})
export class GroupOrderJoinComponent implements OnInit {
  private api = inject(YurtApiService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);

  code = '';
  loading = signal(false);
  error = signal('');

  ngOnInit(): void {
    this.api.configure(environment.apiUrl);
    const codeParam = this.route.snapshot.queryParamMap.get('code');
    if (codeParam) this.code = codeParam;
  }

  join(): void {
    const trimmed = this.code.trim().toUpperCase();
    if (trimmed.length !== 6) { this.error.set('group.codeLength'); return; }
    this.error.set('');
    this.loading.set(true);
    this.api.joinGroupOrder(trimmed).subscribe({
      next: (cart) => this.router.navigate(['/group-order', cart.id]),
      error: () => {
        this.error.set('group.invalidCode');
        this.loading.set(false);
      },
    });
  }
}
