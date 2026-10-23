import { Routes } from '@angular/router';
import { WorkspacePage } from './workspace-page';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'overview' },
  { path: 'overview', component: WorkspacePage, data: { page: 'overview' } },
  { path: 'compare', component: WorkspacePage, data: { page: 'compare' } },
  { path: 'tables', component: WorkspacePage, data: { page: 'tables' } },
  { path: 'history', component: WorkspacePage, data: { page: 'history' } },
  { path: 'settings', component: WorkspacePage, data: { page: 'settings' } },
  { path: '**', redirectTo: 'overview' }
];
