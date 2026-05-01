import { CommonModule } from '@angular/common';
import { Component, Inject, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { ProjectDto } from '../../core/models/api.models';

export interface ProjectEditDialogData {
  project: ProjectDto;
}

@Component({
  selector: 'app-project-edit-dialog',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatCheckboxModule,
    MatSlideToggleModule,
  ],
  template: `
    <h2 mat-dialog-title>
      <mat-icon>edit</mat-icon>
      Edit Project: {{ data.project.name }}
    </h2>

    <mat-dialog-content>
      <form [formGroup]="form" class="edit-form">
        <!-- Basic Settings -->
        <h3 class="section-title">Basic Settings</h3>

        <mat-form-field appearance="outline">
          <mat-label>Project Name</mat-label>
          <input matInput formControlName="name" />
          <mat-icon matSuffix>label</mat-icon>
          @if (form.get('name')?.hasError('required') && form.get('name')?.touched) {
            <mat-error>Name is required</mat-error>
          }
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Default Branch</mat-label>
          <input matInput formControlName="defaultBranch" placeholder="e.g., main, develop" />
          <mat-icon matSuffix>branch</mat-icon>
          <mat-hint>Default branch for scanning repositories in this project</mat-hint>
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Cron Schedule (optional)</mat-label>
          <input matInput formControlName="cronExpression" placeholder="0 2 * * *" />
          <mat-icon matSuffix matTooltip="5-field cron: minute hour day-of-month month day-of-week">schedule</mat-icon>
          <mat-hint>Leave blank to use global schedule</mat-hint>
        </mat-form-field>

        <mat-slide-toggle formControlName="isEnabled" class="toggle">
          Project Enabled
        </mat-slide-toggle>

        <!-- Credentials -->
        <h3 class="section-title">Credentials (optional)</h3>

        <mat-form-field appearance="outline">
          <mat-label>Username</mat-label>
          <input matInput formControlName="username" autocomplete="username" />
          <mat-icon matSuffix>person</mat-icon>
          <mat-hint>Override instance-level credentials</mat-hint>
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Password</mat-label>
          <input matInput type="password" formControlName="password" autocomplete="new-password" />
          <mat-icon matSuffix>lock</mat-icon>
          <mat-hint>Leave blank to keep existing password</mat-hint>
        </mat-form-field>

        <!-- Email Notifications -->
        <h3 class="section-title">Email Notifications</h3>

        <mat-form-field appearance="outline">
          <mat-label>Owner Name</mat-label>
          <input matInput formControlName="ownerName" placeholder="John Doe" />
          <mat-icon matSuffix>badge</mat-icon>
          <mat-hint>Project owner or responsible person</mat-hint>
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Owner Email</mat-label>
          <input matInput type="email" formControlName="ownerEmail" placeholder="owner@example.com" />
          <mat-icon matSuffix>email</mat-icon>
          @if (form.get('ownerEmail')?.hasError('email')) {
            <mat-error>Invalid email format</mat-error>
          }
          <mat-hint>Primary recipient for scan notifications</mat-hint>
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>CC Emails</mat-label>
          <input matInput formControlName="ccEmails" placeholder="user1@example.com, user2@example.com" />
          <mat-icon matSuffix>group</mat-icon>
          <mat-hint>Comma-separated list of additional recipients</mat-hint>
        </mat-form-field>

        <mat-slide-toggle formControlName="sendEmailNotifications" class="toggle">
          Send Email Notifications
        </mat-slide-toggle>
      </form>
    </mat-dialog-content>

    <mat-dialog-actions align="end">
      <button mat-button [mat-dialog-close]="null">Cancel</button>
      <button mat-raised-button color="primary" (click)="save()" [disabled]="form.invalid">
        <mat-icon>save</mat-icon> Save Changes
      </button>
    </mat-dialog-actions>
  `,
  styles: [`
    .edit-form {
      display: flex;
      flex-direction: column;
      gap: 1rem;
      min-width: 500px;
      padding: 1rem 0;

      .section-title {
        margin: 1rem 0 0.5rem 0;
        font-size: 1rem;
        font-weight: 500;
        color: rgba(0, 0, 0, 0.87);
        border-bottom: 1px solid rgba(0, 0, 0, 0.12);
        padding-bottom: 0.5rem;

        &:first-child {
          margin-top: 0;
        }
      }

      mat-form-field {
        width: 100%;
      }

      .toggle {
        margin: 0.5rem 0;
      }
    }

    h2[mat-dialog-title] {
      display: flex;
      align-items: center;
      gap: 0.5rem;

      mat-icon {
        color: var(--primary-color, #1976d2);
      }
    }

    mat-dialog-content {
      max-height: 70vh;
      overflow-y: auto;
    }

    @media (max-width: 600px) {
      .edit-form {
        min-width: unset;
      }
    }
  `],
})
export class ProjectEditDialogComponent implements OnInit {
  form!: FormGroup;

  constructor(
    public dialogRef: MatDialogRef<ProjectEditDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: ProjectEditDialogData,
    private fb: FormBuilder
  ) {}

  ngOnInit(): void {
    this.form = this.fb.group({
      name: [this.data.project.name, [Validators.required]],
      defaultBranch: [this.data.project.defaultBranch || ''],
      cronExpression: [this.data.project.cronExpression || ''],
      isEnabled: [this.data.project.isEnabled],
      username: [''],
      password: [''],
      ownerName: [this.data.project.ownerName || ''],
      ownerEmail: [this.data.project.ownerEmail || '', [Validators.email]],
      ccEmails: [this.data.project.ccEmails || ''],
      sendEmailNotifications: [this.data.project.sendEmailNotifications ?? true],
    });
  }

  save(): void {
    if (this.form.valid) {
      const formValue = this.form.value;
      
      // Remove password if not changed
      if (!formValue.password) {
        delete formValue.password;
      }
      
      // Remove username if empty
      if (!formValue.username) {
        delete formValue.username;
      }

      this.dialogRef.close(formValue);
    }
  }
}
