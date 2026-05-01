/**
 * SMTP Configuration models for email notifications
 */

export interface SmtpConfigurationDto {
  id: string;
  host: string;
  port: number;
  useSsl: boolean;
  useStartTls: boolean;
  username?: string;
  fromEmail: string;
  fromName: string;
  replyToEmail?: string;
  timeoutSeconds: number;
  isActive: boolean;
  isEnabled: boolean;
  lastTestedAt?: Date;
  lastTestResult?: string;
  createdAt: Date;
  updatedAt?: Date;
}

export interface SmtpConfigurationRequest {
  host: string;
  port: number;
  useSsl: boolean;
  useStartTls: boolean;
  username?: string;
  password?: string;
  fromEmail: string;
  fromName: string;
  replyToEmail?: string;
  timeoutSeconds: number;
  isEnabled: boolean;
}

export interface TestEmailRequest {
  toEmail: string;
  subject: string;
  body: string;
}

export interface EmailStatusResponse {
  enabled: boolean;
  configured: boolean;
  fromEmail?: string;
  fromName?: string;
}
