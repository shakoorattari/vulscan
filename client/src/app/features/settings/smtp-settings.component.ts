import { CommonModule } from '@angular/common';
import { Component, OnInit, signal } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { SmtpConfigurationDto, SmtpConfigurationRequest, TestEmailRequest } from '../../core/models/smtp.models';
import { ApiService } from '../../core/services/api.service';

@Component({
  selector: 'app-smtp-settings',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatCheckboxModule,
    MatSlideToggleModule,
    MatProgressSpinnerModule,
    MatTooltipModule,
    MatSnackBarModule,
    MatTableModule,
    MatDialogModule,
  ],
  templateUrl: './smtp-settings.component.html',
  styleUrls: ['./smtp-settings.component.scss'],
})
export class SmtpSettingsComponent implements OnInit {
  loading = signal(false);
  saving = signal(false);
  testing = signal(false);
  configurations = signal<SmtpConfigurationDto[]>([]);
  activeConfig = signal<SmtpConfigurationDto | null>(null);
  editMode = signal(false);
  editingId = signal<string | null>(null);

  smtpForm!: FormGroup;
  testEmailForm!: FormGroup;

  displayedColumns: string[] = ['host', 'port', 'fromEmail', 'isActive', 'isEnabled', 'lastTested', 'actions'];

  constructor(
    private readonly api: ApiService,
    private readonly fb: FormBuilder,
    private readonly snackBar: MatSnackBar,
    private readonly dialog: MatDialog
  ) {
    this.initializeForms();
  }

  ngOnInit(): void {
    this.loadConfigurations();
  }

  private initializeForms(): void {
    this.smtpForm = this.fb.group({
      host: ['', [Validators.required]],
      port: [587, [Validators.required, Validators.min(1), Validators.max(65535)]],
      useSsl: [false],
      useStartTls: [true],
      username: [''],
      password: [''],
      fromEmail: ['', [Validators.required, Validators.email]],
      fromName: ['', [Validators.required]],
      replyToEmail: ['', [Validators.email]],
      timeoutSeconds: [30, [Validators.required, Validators.min(5)]],
      isEnabled: [true],
    });

    this.testEmailForm = this.fb.group({
      toEmail: ['', [Validators.required, Validators.email]],
      subject: ['Test Email from Vulscan', [Validators.required]],
      body: ['This is a test email from Vulscan vulnerability scanner.', [Validators.required]],
    });
  }

  loadConfigurations(): void {
    this.loading.set(true);
    this.api.getAllSmtpConfigurations().subscribe({
      next: (response) => {
        if (response.success && response.data) {
          this.configurations.set(response.data);
          const active = response.data.find(c => c.isActive);
          this.activeConfig.set(active || null);
        }
        this.loading.set(false);
      },
      error: (err) => {
        this.showError('Failed to load SMTP configurations');
        this.loading.set(false);
      },
    });
  }

  startCreate(): void {
    this.editMode.set(true);
    this.editingId.set(null);
    this.smtpForm.reset({
      port: 587,
      useSsl: false,
      useStartTls: true,
      timeoutSeconds: 30,
      isEnabled: true,
    });
  }

  startEdit(config: SmtpConfigurationDto): void {
    this.editMode.set(true);
    this.editingId.set(config.id);
    this.smtpForm.patchValue({
      host: config.host,
      port: config.port,
      useSsl: config.useSsl,
      useStartTls: config.useStartTls,
      username: config.username,
      password: '', // Don't populate password
      fromEmail: config.fromEmail,
      fromName: config.fromName,
      replyToEmail: config.replyToEmail,
      timeoutSeconds: config.timeoutSeconds,
      isEnabled: config.isEnabled,
    });
  }

  cancelEdit(): void {
    this.editMode.set(false);
    this.editingId.set(null);
    this.smtpForm.reset();
  }

  saveConfiguration(): void {
    if (this.smtpForm.invalid) {
      this.smtpForm.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    const request: SmtpConfigurationRequest = this.smtpForm.value;
    
    // Only include password if it's been entered
    if (!request.password) {
      delete request.password;
    }

    const operation = this.editingId()
      ? this.api.updateSmtpConfiguration(this.editingId()!, request)
      : this.api.createSmtpConfiguration(request);

    operation.subscribe({
      next: (response) => {
        if (response.success) {
          this.showSuccess(this.editingId() ? 'Configuration updated successfully' : 'Configuration created successfully');
          this.cancelEdit();
          this.loadConfigurations();
        }
        this.saving.set(false);
      },
      error: (err) => {
        this.showError(err.error?.message || 'Failed to save configuration');
        this.saving.set(false);
      },
    });
  }

  deleteConfiguration(id: string): void {
    if (!confirm('Are you sure you want to delete this SMTP configuration?')) {
      return;
    }

    this.api.deleteSmtpConfiguration(id).subscribe({
      next: (response) => {
        if (response.success) {
          this.showSuccess('Configuration deleted successfully');
          this.loadConfigurations();
        }
      },
      error: (err) => {
        this.showError(err.error?.message || 'Failed to delete configuration');
      },
    });
  }

  setActive(id: string): void {
    this.api.setActiveSmtpConfiguration(id).subscribe({
      next: (response) => {
        if (response.success) {
          this.showSuccess('Configuration activated successfully');
          this.loadConfigurations();
        }
      },
      error: (err) => {
        this.showError(err.error?.message || 'Failed to activate configuration');
      },
    });
  }

  testConfiguration(config: SmtpConfigurationDto): void {
    if (this.testEmailForm.invalid) {
      this.testEmailForm.markAllAsTouched();
      return;
    }

    this.testing.set(true);
    const request: TestEmailRequest = this.testEmailForm.value;

    this.api.testSmtpConfiguration(config.id, request).subscribe({
      next: (response) => {
        if (response.success) {
          this.showSuccess('Test email sent successfully');
          this.loadConfigurations();
        }
        this.testing.set(false);
      },
      error: (err) => {
        this.showError(err.error?.message || 'Failed to send test email');
        this.testing.set(false);
      },
    });
  }

  private showSuccess(message: string): void {
    this.snackBar.open(message, 'Close', { duration: 3000 });
  }

  private showError(message: string): void {
    this.snackBar.open(message, 'Close', { duration: 5000, panelClass: ['error-snackbar'] });
  }
}
