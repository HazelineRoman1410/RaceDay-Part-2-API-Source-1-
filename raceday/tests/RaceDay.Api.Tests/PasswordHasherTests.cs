using RaceDay.Api.Services;

namespace RaceDay.Api.Tests;

public class PasswordHasherTests
{
    private readonly Pbkdf2PasswordHasher _hasher = new();

    [Fact]
    public void Hash_NeverContainsThePlainTextPassword()
    {
        var hash = _hasher.Hash("StrongPass#1");

        Assert.DoesNotContain("StrongPass#1", hash);
        Assert.StartsWith("PBKDF2$", hash);
    }

    [Fact]
    public void Hash_SamePasswordTwice_ProducesDifferentHashesBecauseOfRandomSalt()
    {
        Assert.NotEqual(_hasher.Hash("StrongPass#1"), _hasher.Hash("StrongPass#1"));
    }

    [Fact]
    public void Verify_CorrectPassword_ReturnsTrue()
    {
        var hash = _hasher.Hash("StrongPass#1");
        Assert.True(_hasher.Verify("StrongPass#1", hash));
    }

    [Theory]
    [InlineData("strongpass#1")]
    [InlineData("StrongPass#2")]
    [InlineData("")]
    public void Verify_WrongPassword_ReturnsFalse(string attempt)
    {
        var hash = _hasher.Hash("StrongPass#1");
        Assert.False(_hasher.Verify(attempt, hash));
    }

    [Theory]
    [InlineData("REPLACE_WITH_API_HASH")]
    [InlineData("PBKDF2$abc$salt$key")]
    [InlineData("")]
    public void Verify_MalformedStoredHash_ReturnsFalseInsteadOfThrowing(string stored)
    {
        Assert.False(_hasher.Verify("StrongPass#1", stored));
    }
}
