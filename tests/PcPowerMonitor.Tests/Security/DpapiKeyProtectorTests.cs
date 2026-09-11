using PcPowerMonitor.Core.Security;

namespace PcPowerMonitor.Tests.Security;

public sealed class DpapiKeyProtectorTests
{
    [Fact]
    public void Encrypt_returns_non_empty_base64()
    {
        var plainText = "test-fake-key-123";
        var encrypted = DpapiKeyProtector.Encrypt(plainText);

        Assert.NotEmpty(encrypted);
        // Verify it looks like base64 (rough check)
        try
        {
            var bytes = Convert.FromBase64String(encrypted);
            Assert.NotEmpty(bytes);
        }
        catch
        {
            Assert.Fail("Encrypted result is not valid base64");
        }
    }

    [Fact]
    public void Encrypt_then_try_decrypt_round_trips()
    {
        var original = "test-fake-key-123";
        var encrypted = DpapiKeyProtector.Encrypt(original);

        var success = DpapiKeyProtector.TryDecrypt(encrypted, out var decrypted);

        Assert.True(success);
        Assert.Equal(original, decrypted);
    }

    [Fact]
    public void Different_plaintexts_produce_different_ciphertexts()
    {
        var key1 = "test-key-1";
        var key2 = "test-key-2";

        var encrypted1 = DpapiKeyProtector.Encrypt(key1);
        var encrypted2 = DpapiKeyProtector.Encrypt(key2);

        Assert.NotEqual(encrypted1, encrypted2);
    }

    [Fact]
    public void Same_plaintext_encrypts_differently_each_time()
    {
        var plainText = "test-fake-key-123";

        var encrypted1 = DpapiKeyProtector.Encrypt(plainText);
        var encrypted2 = DpapiKeyProtector.Encrypt(plainText);

        // Due to DPAPI randomization, even same plaintext produces different ciphertexts
        Assert.NotEqual(encrypted1, encrypted2);

        // But both should decrypt to the same value
        Assert.True(DpapiKeyProtector.TryDecrypt(encrypted1, out var dec1));
        Assert.True(DpapiKeyProtector.TryDecrypt(encrypted2, out var dec2));
        Assert.Equal(dec1, dec2);
        Assert.Equal(plainText, dec1);
    }

    [Fact]
    public void Decrypt_garbage_base64_returns_false()
    {
        var success = DpapiKeyProtector.TryDecrypt("not-valid-base64!!!", out var plainText);

        Assert.False(success);
        Assert.Empty(plainText);
    }

    [Fact]
    public void Decrypt_valid_base64_but_not_dpapi_returns_false()
    {
        // Valid base64 string that is NOT a DPAPI blob
        var validBase64 = Convert.ToBase64String("just-random-bytes"u8.ToArray());

        var success = DpapiKeyProtector.TryDecrypt(validBase64, out var plainText);

        Assert.False(success);
        Assert.Empty(plainText);
    }

    [Fact]
    public void Decrypt_empty_string_returns_false()
    {
        var success = DpapiKeyProtector.TryDecrypt(string.Empty, out var plainText);

        Assert.False(success);
        Assert.Empty(plainText);
    }

    [Fact]
    public void Decrypt_null_or_whitespace_returns_false()
    {
        Assert.False(DpapiKeyProtector.TryDecrypt(null!, out _));
        Assert.False(DpapiKeyProtector.TryDecrypt("   ", out _));
    }

    [Fact]
    public void Encrypt_null_throws()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => DpapiKeyProtector.Encrypt(null!));
        Assert.Equal("plainText", ex.ParamName);
    }

    [Fact]
    public void Encrypt_empty_string_succeeds()
    {
        // Edge case: empty plaintext is valid (just encrypts empty bytes)
        var encrypted = DpapiKeyProtector.Encrypt(string.Empty);
        Assert.NotEmpty(encrypted);

        var success = DpapiKeyProtector.TryDecrypt(encrypted, out var decrypted);
        Assert.True(success);
        Assert.Empty(decrypted);
    }

    [Fact]
    public void Encrypt_unicode_roundtrips()
    {
        var plainText = "test-key-with-unicode-🔐-symbols";
        var encrypted = DpapiKeyProtector.Encrypt(plainText);

        var success = DpapiKeyProtector.TryDecrypt(encrypted, out var decrypted);

        Assert.True(success);
        Assert.Equal(plainText, decrypted);
    }

    [Fact]
    public void Encrypt_long_string_roundtrips()
    {
        var plainText = new string('x', 1000);
        var encrypted = DpapiKeyProtector.Encrypt(plainText);

        var success = DpapiKeyProtector.TryDecrypt(encrypted, out var decrypted);

        Assert.True(success);
        Assert.Equal(plainText, decrypted);
    }

    [Fact]
    public void Tampered_base64_blob_returns_false()
    {
        var original = "test-fake-key-123";
        var encrypted = DpapiKeyProtector.Encrypt(original);

        // Tamper: flip a character in the middle
        var chars = encrypted.ToCharArray();
        chars[encrypted.Length / 2] = chars[encrypted.Length / 2] == 'A' ? 'B' : 'A';
        var tampered = new string(chars);

        var success = DpapiKeyProtector.TryDecrypt(tampered, out var plainText);

        Assert.False(success);
        Assert.Empty(plainText);
    }
}
