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
  templateUrl: './project-edit-dialog.component.html',
  styleUrl: './project-edit-dialog.component.scss',
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
