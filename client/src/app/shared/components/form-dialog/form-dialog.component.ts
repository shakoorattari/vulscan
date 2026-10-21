import { Component, Inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';

export interface FormField {
  key: string;
  label: string;
  type?: 'text' | 'password' | 'email' | 'select' | 'textarea' | 'date';
  required?: boolean;
  value?: string;
  hint?: string;
  options?: { value: string; label: string }[];
  minLength?: number;
  maxLength?: number;
  autocomplete?: string;
}

export interface FormDialogData {
  title: string;
  message?: string;
  confirmLabel?: string;
  /** Styles the confirm button as destructive. */
  danger?: boolean;
  fields: FormField[];
}

/** A small generic "fill in these fields" dialog. Closes with a key→value map, or undefined when cancelled. */
@Component({
  selector: 'app-form-dialog',
  standalone: true,
  imports: [FormsModule, MatButtonModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatSelectModule],
  template: `
    <h2 mat-dialog-title>{{ data.title }}</h2>
    <mat-dialog-content>
      @if (data.message) {
        <p class="dialog-message">{{ data.message }}</p>
      }
      <form id="dialog-form" (ngSubmit)="submit()" class="dialog-form">
        @for (f of data.fields; track f.key) {
          <mat-form-field appearance="outline" class="full-width">
            <mat-label>{{ f.label }}</mat-label>
            @switch (f.type) {
              @case ('select') {
                <mat-select [(ngModel)]="values[f.key]" [name]="f.key" [required]="!!f.required">
                  @for (o of f.options ?? []; track o.value) {
                    <mat-option [value]="o.value">{{ o.label }}</mat-option>
                  }
                </mat-select>
              }
              @case ('textarea') {
                <textarea matInput rows="3" [(ngModel)]="values[f.key]" [name]="f.key" [required]="!!f.required"
                          [attr.minlength]="f.minLength" [attr.maxlength]="f.maxLength"></textarea>
              }
              @default {
                <input matInput [type]="f.type ?? 'text'" [(ngModel)]="values[f.key]" [name]="f.key" [required]="!!f.required"
                       [attr.minlength]="f.minLength" [attr.maxlength]="f.maxLength" [attr.autocomplete]="f.autocomplete ?? 'off'" />
              }
            }
            @if (f.hint) {
              <mat-hint>{{ f.hint }}</mat-hint>
            }
          </mat-form-field>
        }
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button type="button" (click)="ref.close()">Cancel</button>
      <button mat-flat-button [color]="data.danger ? 'warn' : 'primary'" type="submit" form="dialog-form" [disabled]="!valid()">
        {{ data.confirmLabel ?? 'Confirm' }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .dialog-form { display: flex; flex-direction: column; gap: 4px; min-width: 360px; padding-top: 8px; }
    .full-width { width: 100%; }
    .dialog-message { margin: 0 0 8px; color: var(--neutral-700); }
  `,
})
export class FormDialogComponent {
  values: Record<string, string> = {};

  constructor(
    public readonly ref: MatDialogRef<FormDialogComponent, Record<string, string>>,
    @Inject(MAT_DIALOG_DATA) public readonly data: FormDialogData,
  ) {
    for (const f of data.fields) this.values[f.key] = f.value ?? '';
  }

  valid(): boolean {
    return this.data.fields.every((f) => {
      const v = (this.values[f.key] ?? '').trim();
      if (f.required && !v) return false;
      if (f.minLength && v.length > 0 && v.length < f.minLength) return false;
      return true;
    });
  }

  submit(): void {
    if (this.valid()) this.ref.close({ ...this.values });
  }
}
