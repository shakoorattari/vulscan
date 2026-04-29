# 🛡️ Vulscan Security Guide

This document outlines the security best practices and setup procedures for the Vulscan vulnerability scanning platform.

## 🚨 **IMMEDIATE ACTIONS REQUIRED**

### 1. **Environment Variables Setup**

Copy the example environment file and configure secure values:

```bash
# Copy the example file
cp .env.example .env

# Generate secure passwords
openssl rand -base64 32  # For SQL_SERVER_PASSWORD
openssl rand -base64 64  # For JWT_SECRET_KEY
```

Edit `.env` with your secure values:

```bash
nano .env
```

### 2. **Default Admin Password Change**

⚠️ **CRITICAL**: Change the default admin password immediately after first login!

1. Start the application
2. Login with username: `admin` and password: `Vulscan@2025`  
   ⚠️ **DEVELOPMENT ONLY** — **DO NOT USE THIS PASSWORD IN PRODUCTION!**
3. Navigate to user settings
4. Change password to a strong, unique password

### 3. **Application Configuration**

Update your `appsettings.json` to use environment variables:

```json
{
  "ConnectionStrings": {
    "VulscanDb": "Server=localhost,1433;Database=VulscanDb;User Id=sa;Password=${SQL_SERVER_PASSWORD};TrustServerCertificate=true;MultipleActiveResultSets=true"
  },
  "Jwt": {
    "SecretKey": "${JWT_SECRET_KEY}",
    "Issuer": "VulscanApi",
    "Audience": "VulscanDashboard",
    "ExpirationHours": 8
  }
}
```

## 🔒 **Security Best Practices**

### **Password Policy**

- **Minimum 12 characters**
- **Mix of uppercase, lowercase, numbers, and symbols**
- **No dictionary words or personal information**
- **Unique for each service**

### **Secret Management**

- ✅ Use environment variables for all secrets
- ✅ Never commit credentials to version control
- ✅ Use secure credential storage (Azure Key Vault, HashiCorp Vault, etc.)
- ✅ Rotate credentials every 30-90 days
- ❌ Never hardcode secrets in configuration files
- ❌ Never share credentials via email or chat

### **Development Environment**

- Use SQLite for local development (already configured in `appsettings.Development.json`)
- Use different passwords for development vs. production
- Keep development data separate from production

### **Production Environment**

- Use Azure Key Vault or equivalent for secret management
- Enable HTTPS with valid SSL certificates
- Configure proper firewall rules
- Enable audit logging
- Regular security scans and updates

## 🔧 **Docker Security Configuration**

### **Using Environment Variables with Docker Compose**

Create a `.env` file in the root directory:

```env
SQL_SERVER_PASSWORD=YourSecureProductionPassword2026!
JWT_SECRET_KEY=YourSecure64CharacterJWTSecretKeyForProductionEnvironment2026!
```

The `docker-compose.yml` will automatically load these variables.

### **Docker Security Best Practices**

- Don't run containers as root
- Use specific version tags, not `latest`
- Scan images for vulnerabilities
- Limit container resources
- Use Docker secrets for production

## 📧 **SMTP Configuration (Optional)**

For email notifications, add to your `.env` file:

```env
SMTP_SERVER=smtp.yourcompany.com
SMTP_PORT=587
SMTP_USERNAME=vulscan@yourcompany.com
SMTP_PASSWORD=your_secure_smtp_password
SMTP_USE_TLS=true
```

## 🔍 **Security Monitoring**

### **What to Monitor**

- Failed login attempts
- Unusual database access patterns
- Large data exports
- Configuration changes
- Network connections from unexpected IPs

### **Audit Logging**

All security-sensitive operations are logged:

- Authentication attempts
- Configuration changes
- Data access and exports
- Administrative actions

Logs are stored in:

- Application logs: `server/src/Vulscan.Api/logs/`
- Database audit trail: `AuditLog` table

## 🚀 **Deployment Checklist**

### **Before Production Deployment**

- [ ] Change all default passwords
- [ ] Configure strong JWT secret key
- [ ] Setup environment variable management
- [ ] Configure HTTPS/TLS
- [ ] Setup database backups
- [ ] Configure firewall rules
- [ ] Enable audit logging
- [ ] Test credential rotation procedure
- [ ] Setup monitoring and alerting
- [ ] Remove development/test data
- [ ] Validate security configurations

### **Regular Security Tasks**

- [ ] Rotate credentials every 30-90 days
- [ ] Review access logs monthly
- [ ] Update dependencies regularly
- [ ] Scan for new vulnerabilities
- [ ] Test backup and recovery procedures
- [ ] Review firewall and network access rules

## 🆘 **Incident Response**

### **If Credentials Are Compromised**

1. **Immediately rotate all affected credentials**
2. **Review audit logs for unauthorized access**
3. **Check for data exfiltration**
4. **Update all systems using the compromised credentials**
5. **Notify relevant stakeholders**
6. **Document the incident and lessons learned**

### **Security Contact**

For security issues or questions:

- 📧 Email: <security@yourCompany.com>
- 🔐 Report vulnerabilities responsibly
- 📞 Emergency Security Hotline: [Your Phone Number]

## 📚 **Additional Resources**

- [OWASP Top 10](https://owasp.org/www-project-top-ten/)
- [Microsoft Security Documentation](https://docs.microsoft.com/en-us/security/)
- [Docker Security Best Practices](https://docs.docker.com/engine/security/)
- [.NET Core Security Guidelines](https://docs.microsoft.com/en-us/aspnet/core/security/)

---

## 🏷️ **Version Control Security**

### **What NOT to Commit**

```bash
# Files that should NEVER be in version control
.env*
appsettings.local.json
secrets.json
*.pem
*.p12
*.pfx
*.key
database-backups/
logs/
```

### **Before Each Commit**

```bash
# Check for accidentally staged secrets
git diff --staged | grep -i "password\|secret\|key"

# Use git-secrets tool (recommended)
git secrets --scan

# GitGuardian CLI scan
ggshield secret scan pre-commit
```

---
*Last Updated: February 18, 2026*
