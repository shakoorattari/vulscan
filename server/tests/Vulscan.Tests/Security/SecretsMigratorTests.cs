using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using Vulscan.Application.Common;
using Vulscan.Domain.Entities;
using Vulscan.Domain.Enums;
using Vulscan.Infrastructure.Data;
using Vulscan.Infrastructure.Security;
using Vulscan.Tests.Support;

namespace Vulscan.Tests.Security;

public class SecretsMigratorTests
{
    private readonly DataProtectionSecretProtector _protector = new(new EphemeralDataProtectionProvider());
    private CredentialVault Vault => new(_protector);

    private static async Task<(Guid InstanceId, Guid ProjectId)> SeedLegacyAsync(VulscanDbContext db)
    {
        var instance = new AzureDevOpsInstance
        {
            Name = "ado", Url = "https://ado.local", Collection = "DefaultCollection", AuthMethod = AuthMethod.BasicAuth,
            CredentialReference = "{\"username\":\"inst-user\",\"password\":\"inst-pass\"}",
        };
        db.AzureDevOpsInstances.Add(instance);
        await db.SaveChangesAsync();

        var project = new Project
        {
            InstanceId = instance.Id, Name = "p", AzureProjectId = "p", Url = "https://ado.local/p",
            CredentialReference = "{\"username\":\"proj-user\",\"password\":\"proj-pass\"}",
        };
        db.Projects.Add(project);
        db.SmtpConfigurations.Add(new SmtpConfiguration { Host = "smtp", FromEmail = "a@b.c", Username = "smtp-user", Password = "smtp-pass" });
        await db.SaveChangesAsync();
        return (instance.Id, project.Id);
    }

    [Fact]
    public async Task Encrypts_every_legacy_plaintext_secret_and_keeps_it_usable()
    {
        using var test = new TestDb();
        await using var db = test.NewContext();
        var (instanceId, projectId) = await SeedLegacyAsync(db);

        var changed = await SecretsMigrator.EncryptLegacySecretsAsync(db, Vault, _protector, NullLogger.Instance);

        Assert.Equal(3, changed);
        await using var verify = test.NewContext();
        var instance = await verify.AzureDevOpsInstances.FindAsync(instanceId);
        var project = await verify.Projects.FindAsync(projectId);
        var smtp = verify.SmtpConfigurations.Single();

        Assert.True(_protector.IsProtected(instance!.CredentialReference));
        Assert.True(_protector.IsProtected(project!.CredentialReference));
        Assert.True(_protector.IsProtected(smtp.Password));
        Assert.DoesNotContain("inst-pass", instance.CredentialReference);
        Assert.DoesNotContain("proj-pass", project.CredentialReference!);
        Assert.DoesNotContain("smtp-pass", smtp.Password!);

        Assert.Equal(("inst-user", "inst-pass"), (Vault.Unpack(instance.CredentialReference).Username, Vault.Unpack(instance.CredentialReference).Password));
        Assert.Equal("smtp-pass", _protector.Unprotect(smtp.Password!));
    }

    [Fact]
    public async Task Is_idempotent()
    {
        using var test = new TestDb();
        await using var db = test.NewContext();
        await SeedLegacyAsync(db);

        await SecretsMigrator.EncryptLegacySecretsAsync(db, Vault, _protector, NullLogger.Instance);
        var before = db.Projects.Single().CredentialReference;
        var second = await SecretsMigrator.EncryptLegacySecretsAsync(db, Vault, _protector, NullLogger.Instance);

        Assert.Equal(0, second);
        Assert.Equal(before, db.Projects.Single().CredentialReference);   // not double-encrypted
    }

    [Fact]
    public async Task Leaves_unreadable_legacy_values_untouched_rather_than_destroying_them()
    {
        using var test = new TestDb();
        await using var db = test.NewContext();
        db.AzureDevOpsInstances.Add(new AzureDevOpsInstance
        {
            Name = "weird", Url = "https://x", Collection = "c", AuthMethod = AuthMethod.Pat, CredentialReference = "some-opaque-token",
        });
        await db.SaveChangesAsync();

        var changed = await SecretsMigrator.EncryptLegacySecretsAsync(db, Vault, _protector, NullLogger.Instance);

        Assert.Equal(0, changed);
        Assert.Equal("some-opaque-token", db.AzureDevOpsInstances.Single().CredentialReference);
    }
}
