using Microsoft.AspNetCore.DataProtection;
using Vulscan.Application.Common;
using Vulscan.Infrastructure.Security;

namespace Vulscan.Tests.Security;

public class CredentialVaultTests
{
    private static CredentialVault NewVault(IDataProtectionProvider? provider = null) =>
        new(new DataProtectionSecretProtector(provider ?? new EphemeralDataProtectionProvider()));

    [Fact]
    public void Pack_encrypts_and_never_leaks_the_secret()
    {
        var packed = NewVault().Pack("svc-scan", "p@ss;w\"ord,1");

        Assert.StartsWith(DataProtectionSecretProtector.Prefix, packed);
        Assert.DoesNotContain("p@ss", packed);
        Assert.DoesNotContain("svc-scan", packed);
    }

    [Fact]
    public void Unpack_round_trips_awkward_characters()
    {
        var vault = NewVault();
        var creds = vault.Unpack(vault.Pack("user@corp.local", "p@ss;w\"ord,1 üñí"));

        Assert.Equal("user@corp.local", creds.Username);
        Assert.Equal("p@ss;w\"ord,1 üñí", creds.Password);
        Assert.False(creds.IsEmpty);
    }

    [Fact]
    public void Unpack_still_reads_legacy_plaintext_json_and_flags_it_for_encryption()
    {
        var vault = NewVault();
        const string legacy = "{\"username\":\"old\",\"password\":\"plain\"}";

        Assert.True(vault.NeedsEncryption(legacy));
        var creds = vault.Unpack(legacy);
        Assert.Equal(("old", "plain"), (creds.Username, creds.Password));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_values_are_empty_and_need_nothing(string? stored)
    {
        var vault = NewVault();
        Assert.True(vault.Unpack(stored).IsEmpty);
        Assert.False(vault.NeedsEncryption(stored));
    }

    [Fact]
    public void Garbage_plaintext_unpacks_to_empty_instead_of_throwing()
        => Assert.True(NewVault().Unpack("not json at all").IsEmpty);

    [Fact]
    public void Packed_values_do_not_need_encryption()
    {
        var vault = NewVault();
        Assert.False(vault.NeedsEncryption(vault.Pack("u", "p")));
    }

    [Fact]
    public void Decrypting_with_a_different_key_ring_fails_with_an_actionable_error()
    {
        var packed = NewVault(new EphemeralDataProtectionProvider()).Pack("u", "p");

        var ex = Assert.Throws<InvalidOperationException>(() => NewVault(new EphemeralDataProtectionProvider()).Unpack(packed));
        Assert.Contains("DataProtection:KeysPath", ex.Message);
    }

    [Fact]
    public void Tampered_ciphertext_is_rejected()
    {
        var vault = NewVault();
        var packed = vault.Pack("u", "p");
        var tampered = packed[..^4] + (packed.EndsWith("AAAA") ? "BBBB" : "AAAA");

        Assert.Throws<InvalidOperationException>(() => vault.Unpack(tampered));
    }
}
