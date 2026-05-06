import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { ComponentSchemaEntry, PropertySchema } from '../models/protocol';
import { REST_BASE } from '../config';

@Injectable({ providedIn: 'root' })
export class SchemaService {
  private http = inject(HttpClient);
  private _schemas = signal<Map<string, PropertySchema[]>>(new Map());

  readonly schemas = this._schemas.asReadonly();

  load(): void {
    this.http.get<ComponentSchemaEntry[]>(`${REST_BASE}/api/sim/schemas`).subscribe({
      next: entries => {
        const map = new Map<string, PropertySchema[]>();
        for (const e of entries) map.set(e.kind, e.schema);
        this._schemas.set(map);
      },
      error: err => console.warn('[SchemaService] Failed to load component schemas:', err),
    });
  }

  getSchema(kind: string): PropertySchema[] {
    return this._schemas().get(kind) ?? [];
  }
}
