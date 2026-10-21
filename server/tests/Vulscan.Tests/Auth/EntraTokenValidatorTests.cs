using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Vulscan.Application.Auth;
using Vulscan.Infrastructure.Security;

namespace Vulscan.Tests.Auth;

public class EntraTokenValidatorTests : IDisposable
{
    private const string Tenant = "11111111-1111-1111-1111-111111111111";
    private const string Client = "22222222-2222-2222-2222-222222222222";
    private const string Oid = "aaaaaaaa-0000-0000-0000-000000000001";
    private static readonly string Issuer = $"https://login.microsoftonline.com/{Tenant}/v2.0";

    private readonly RSA _rsa = RSA.Create(2048);
    private readonly RSA _attacker = RSA.Create(2048);

    public void Dispose() { _rsa.Dispose(); _attacker.Dispose(); }

    private EntraTokenValidator Validator(bool enabled = true)
    {
        var options = new AuthOptions { Entra = { Enabled = enabled, TenantId = Tenant, ClientId = Client } };
        var config = new OpenIdConnectConfiguration { Issuer = Issuer };
        config.SigningKeys.Add(new RsaSecurityKey(_rsa) { KeyId = "kid-1" });
        return new EntraTokenValidator(options, NullLogger<EntraTokenValidator>.Instance,
            configurationManager: new StaticConfigurationManager<OpenIdConnectConfiguration>(config));
    }

    private string Token(
        RSA? signWith = null, string? issuer = null, string? audience = null, DateTime? expires = null,
        string tid = Tenant, string? oid = Oid, string? upn = "analyst@sdd.shj.ae", string[]? roles = null,
        bool unsigned = false, string kid = "kid-1")
    {
        var claims = new Dictionary<string, object> { ["tid"] = tid, ["name"] = "Test Analyst", ["ver"] = "2.0" };
        if (oid is not null) claims["oid"] = oid;
        if (upn is not null) { claims["preferred_username"] = upn; claims["email"] = upn; }
        claims["roles"] = roles ?? ["Vulscan.SecurityAnalyst"];

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer ?? Issuer,
            Audience = audience ?? Client,
            IssuedAt = DateTime.UtcNow.AddMinutes(-1),
            NotBefore = DateTime.UtcNow.AddMinutes(-1),
            Expires = expires ?? DateTime.UtcNow.AddMinutes(30),
            Claims = claims,
            SigningCredentials = unsigned ? null : new SigningCredentials(new RsaSecurityKey(signWith ?? _rsa) { KeyId = kid }, SecurityAlgorithms.RsaSha256),
        };
        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    private Task<Vulscan.Application.Interfaces.EntraIdentity> Run(string token) => Validator().ValidateAsync(token);

    [Fact]
    public async Task A_correctly_signed_token_for_this_app_and_tenant_is_accepted_and_mapped()
    {
        var id = await Run(Token(roles: ["Vulscan.Admin", "Vulscan.Viewer"]));

        Assert.Equal(Oid, id.ObjectId);
        Assert.Equal("analyst@sdd.shj.ae", id.Username);
        Assert.Equal("analyst@sdd.shj.ae", id.Email);
        Assert.Equal("Test Analyst", id.DisplayName);
        Assert.Equal(["Vulscan.Admin", "Vulscan.Viewer"], id.Roles.OrderBy(r => r));
    }

    [Fact]
    public async Task Object_id_is_normalised_to_lower_case_so_it_always_matches_the_stored_key()
    {
        var id = await Run(Token(oid: "AAAAAAAA-0000-0000-0000-00000000ABCD"));
        Assert.Equal("aaaaaaaa-0000-0000-0000-00000000abcd", id.ObjectId);
    }

    [Fact]
    public async Task A_token_signed_by_someone_elses_key_is_rejected_even_when_it_claims_the_right_key_id()
        => await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Run(Token(signWith: _attacker)));

    [Fact]
    public async Task A_token_for_a_different_audience_is_rejected()
        => await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Run(Token(audience: "33333333-3333-3333-3333-333333333333")));

    [Fact]
    public async Task A_token_from_another_tenant_issuer_is_rejected()
        => await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            Run(Token(issuer: "https://login.microsoftonline.com/99999999-9999-9999-9999-999999999999/v2.0")));

    [Fact]
    public async Task A_forged_tid_claim_under_the_right_issuer_is_still_rejected()
    {
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Run(Token(tid: "99999999-9999-9999-9999-999999999999")));
        Assert.Contains("different tenant", ex.Message);
    }

    [Fact]
    public async Task An_expired_token_is_rejected()
        => await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Run(Token(expires: DateTime.UtcNow.AddMinutes(-10))));

    [Fact]
    public async Task An_unsigned_token_is_rejected()
        => await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Run(Token(unsigned: true)));

    [Fact]
    public async Task Algorithm_confusion_with_an_hmac_token_keyed_by_the_public_key_is_rejected()
    {
        var publicKeyMaterial = Convert.ToBase64String(_rsa.ExportSubjectPublicKeyInfo());
        var hmac = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(publicKeyMaterial.PadRight(64, 'x'))) { KeyId = "kid-1" };
        var forged = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Issuer, Audience = Client, Expires = DateTime.UtcNow.AddMinutes(30),
            Claims = new Dictionary<string, object> { ["tid"] = Tenant, ["oid"] = Oid, ["preferred_username"] = "evil@sdd.shj.ae", ["roles"] = new[] { "Vulscan.Admin" } },
            SigningCredentials = new SigningCredentials(hmac, SecurityAlgorithms.HmacSha256),
        });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Run(forged));
    }

    [Theory]
    [InlineData(null, "analyst@sdd.shj.ae")]       // no object id
    [InlineData("not-a-guid", "analyst@sdd.shj.ae")]
    [InlineData(Oid, null)]                        // no username-like claim
    public async Task Tokens_missing_the_identity_claims_are_rejected(string? oid, string? upn)
        => await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Run(Token(oid: oid, upn: upn)));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not.a.jwt")]
    public async Task Garbage_input_is_rejected(string token)
        => await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Run(token));

    [Fact]
    public async Task An_oversized_token_is_rejected_before_any_parsing()
        => await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Run(new string('a', 20_000)));

    [Fact]
    public async Task Nothing_is_validated_when_entra_is_not_enabled()
        => await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Validator(enabled: false).ValidateAsync(Token()));

    [Fact]
    public async Task A_token_without_roles_is_accepted_here_and_refused_later_by_role_mapping()
    {
        var id = await Run(Token(roles: []));
        Assert.Empty(id.Roles);
    }
}
