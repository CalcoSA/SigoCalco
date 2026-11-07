using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SigoCalco.Authentication.Domain.Validators;
using SigoCalco.Authentication.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.Configuration;

namespace SigoCalco.Authentication.Test;

public class WordpressEmailTests
{
    [Theory]
    [InlineData("person@example.com")]
    [InlineData(null)]
    public void QueryAndReaderIncludeEmailWithoutExposingPasswordHash(string? email)
    {
        // Exercises the exact mapper used by GetUser; no WordPress connection is opened.
        var table = new DataTable();
        table.Columns.Add("wordpressUserId", typeof(ulong));
        foreach (var column in new[] { "wordpressUserLogin", "wordpressDisplayName", "wordpressUserPass", "wordpressUserEmail" })
            table.Columns.Add(column, typeof(string));
        table.Rows.Add(123UL, "person", "Persona", "test-only-hash", email is null ? DBNull.Value : email);
        using var reader = table.CreateDataReader();
        Assert.True(reader.Read());
        var user = WordpressUserRepository.MapUser(reader);
        Assert.Equal(123UL, user.WordpressUserId);
        Assert.Equal("person", user.WordpressUserLogin);
        Assert.Equal("Persona", user.WordpressDisplayName);
        Assert.Equal(email, user.WordpressUserEmail);
        Assert.Equal("test-only-hash", user.WordpressUserPass);
        var json = JsonSerializer.Serialize(user, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Contains("wordpressUserEmail", json);
        Assert.DoesNotContain("test-only-hash", json);
        Assert.DoesNotContain("wordpressUserPass", json);
        Assert.Contains("user_email AS wordpressUserEmail", WordpressUserRepository.UserByLoginQuery);
        Assert.Contains("WHERE user_login = @username", WordpressUserRepository.UserByLoginQuery);
        Assert.Contains("LIMIT 1", WordpressUserRepository.UserByLoginQuery);
    }

    [Theory]
    [InlineData("bcrypt")]
    [InlineData("wordpress-bcrypt")]
    [InlineData("legacy-md5")]
    public void OriginalWordpressPasswordVerificationStillAcceptsOnlyTheCorrectPassword(string algorithm)
    {
        const string password = "synthetic-test-password";
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["ConnectionStrings:WordPressDatabase"] = "Server=localhost;Database=unused;" }).Build();
        var repository = new WordpressUserRepository(configuration);
        var hash = algorithm switch
        {
            "legacy-md5" => Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(password))).ToLowerInvariant(),
            "wordpress-bcrypt" => WordpressHash(password),
            _ => BCrypt.Net.BCrypt.HashPassword(password, workFactor: 4)
        };
        Assert.True(repository.Verify(password, hash));
        Assert.False(repository.Verify("incorrect-password", hash));
        Assert.False(repository.Verify("", hash));

        static string WordpressHash(string value)
        {
            using var hmac = new HMACSHA384(Encoding.UTF8.GetBytes("wp-sha384"));
            var prehash = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(value)));
            return "$wp" + BCrypt.Net.BCrypt.HashPassword(prehash, workFactor: 4).Replace("$2a$", "$2y$");
        }
    }

    [Theory]
    [InlineData("a@example.com", true)]
    [InlineData("  User+tag@example.com  ", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("missing-domain@", false)]
    [InlineData("@example.com", false)]
    [InlineData("a@b@example.com", false)]
    [InlineData("Person <a@example.com>", false)]
    [InlineData("a b@example.com", false)]
    [InlineData("a\r\n@example.com", false)]
    public void EmailValidationAndNormalizationAreConsistent(string? value, bool valid)
    {
        Assert.Equal(valid, UserEmail.IsValid(value));
        if (valid) Assert.Equal(value!.Trim(), UserEmail.Normalize(value));
    }

    [Fact]
    public void EmailLongerThanTheDatabaseColumnIsRejected()
        => Assert.False(UserEmail.IsValid(new string('a', 243) + "@example.com"));
}
