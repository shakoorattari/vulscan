using Vulscan.Application.Auth;

namespace Vulscan.Tests.Auth;

public class PasswordPolicyTests
{
    [Theory]
    [InlineData("Correct-Horse-9-Battery")]
    [InlineData("An0ther-Strong-Passphrase!")]
    [InlineData("zQ7#kLm2$vXw9pRt")]
    public void Strong_passwords_are_accepted(string password)
        => Assert.Empty(PasswordPolicy.Validate(password, "someone", "someone@sdd.shj.ae"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Missing_password_is_refused(string? password)
        => Assert.NotEmpty(PasswordPolicy.Validate(password));

    [Theory]
    [InlineData("Vulscan@2025")]            // the published default
    [InlineData("vulscan@2025")]            // case-insensitive
    [InlineData("Password123!")]
    [InlineData("Aaaa-Aaaa-Aaaa1")]         // technically mixed but trivially repetitive
    public void Common_or_trivially_simple_passwords_are_refused(string password)
        => Assert.Contains(PasswordPolicy.Validate(password), p => p.Contains("common", StringComparison.OrdinalIgnoreCase));

    [Fact]
    public void Personal_information_is_refused()
    {
        Assert.Contains(PasswordPolicy.Validate("Shakoor-Hussain-9!x", "shakoor.hussain", "shakoor.hussain@sdd.shj.ae"),
            p => p.Contains("username or email"));
        Assert.Contains(PasswordPolicy.Validate("xx-Hussain.Attari-77!", "other", "hussain.attari@sdd.shj.ae"),
            p => p.Contains("username or email"));
    }

    [Fact]
    public void Very_short_usernames_do_not_cause_false_positives()
        => Assert.Empty(PasswordPolicy.Validate("Correct-Horse-9-Battery", "ab", "ab@sdd.shj.ae"));

    [Fact]
    public void Every_broken_rule_is_reported_at_once()
    {
        var problems = PasswordPolicy.Validate("abc");
        Assert.True(problems.Count >= 4);
    }
}
