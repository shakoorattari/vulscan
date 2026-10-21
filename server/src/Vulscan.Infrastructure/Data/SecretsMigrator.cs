using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Vulscan.Application.Common;
using Vulscan.Application.Interfaces;

namespace Vulscan.Infrastructure.Data;

/// <summary>
/// One-off, idempotent encryption of secrets that earlier versions stored in plaintext
/// (SCM credentials on instances/projects, SMTP passwords). Safe to run on every start.
/// </summary>
public static class SecretsMigrator
{
    public static async Task<int> EncryptLegacySecretsAsync(
        VulscanDbContext db, CredentialVault vault, ISecretProtector protector, ILogger logger, CancellationToken ct = default)
    {
        var changed = 0;

        foreach (var instance in await db.AzureDevOpsInstances.Where(i => i.CredentialReference != string.Empty).ToListAsync(ct))
        {
            if (!vault.NeedsEncryption(instance.CredentialReference)) continue;
            var c = vault.Unpack(instance.CredentialReference);
            if (c.IsEmpty) continue;                     // unreadable legacy value: leave untouched rather than destroy it
            instance.CredentialReference = vault.Pack(c.Username, c.Password);
            changed++;
        }

        foreach (var project in await db.Projects.Where(p => p.CredentialReference != null && p.CredentialReference != string.Empty).ToListAsync(ct))
        {
            if (!vault.NeedsEncryption(project.CredentialReference)) continue;
            var c = vault.Unpack(project.CredentialReference);
            if (c.IsEmpty) continue;
            project.CredentialReference = vault.Pack(c.Username, c.Password);
            changed++;
        }

        foreach (var smtp in await db.SmtpConfigurations.Where(s => s.Password != null && s.Password != string.Empty).ToListAsync(ct))
        {
            if (protector.IsProtected(smtp.Password)) continue;
            smtp.Password = protector.Protect(smtp.Password!);
            changed++;
        }

        if (changed > 0)
        {
            await db.SaveChangesAsync(ct);
            logger.LogWarning("Encrypted {Count} previously plaintext stored secret(s).", changed);
        }

        return changed;
    }
}
