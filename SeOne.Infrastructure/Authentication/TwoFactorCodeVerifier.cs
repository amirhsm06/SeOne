using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SeOne.Domain.Entities;

namespace SeOne.Infrastructure.Authentication;

public static class TwoFactorCodeVerifier
{
    private sealed class StoredTwoFactorData
    {
        public string? Secret { get; set; }
        public string[] Codes { get; set; } = Array.Empty<string>();
    }

    public static bool VerifyTotp(string code, string? secret)
    {
        if (string.IsNullOrWhiteSpace(secret) || code.Length != 6 || !code.All(char.IsDigit))
            return false;

        byte[] secretBytes;
        try { secretBytes = DecodeBase32(secret); }
        catch { return false; }

        var unixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        for (long offset = -1; offset <= 1; offset++)
        {
            var counterBytes = BitConverter.GetBytes((unixSeconds / 30) + offset);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(counterBytes);

            using var hmac = new HMACSHA1(secretBytes);
            var hash = hmac.ComputeHash(counterBytes);
            var offsetIndex = hash[^1] & 0x0f;
            var binary = ((hash[offsetIndex] & 0x7f) << 24)
                | (hash[offsetIndex + 1] << 16)
                | (hash[offsetIndex + 2] << 8)
                | hash[offsetIndex + 3];

            if ((binary % 1_000_000).ToString("D6") == code)
                return true;
        }

        return false;
    }

    public static string HashBackupCode(string code)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(code.Trim().ToUpperInvariant()));
        return "sha256:" + Convert.ToHexString(bytes);
    }

    public static bool TryConsumeBackupCode(User user, string suppliedCode)
    {
        if (string.IsNullOrWhiteSpace(user.TwoFactorBackupCodesJson) || string.IsNullOrWhiteSpace(suppliedCode))
            return false;

        StoredTwoFactorData data;
        try
        {
            var raw = user.TwoFactorBackupCodesJson.TrimStart();
            if (raw.StartsWith("[", StringComparison.Ordinal))
            {
                data = new StoredTwoFactorData
                {
                    Codes = JsonSerializer.Deserialize<string[]>(raw) ?? Array.Empty<string>()
                };
            }
            else
            {
                data = JsonSerializer.Deserialize<StoredTwoFactorData>(raw) ?? new StoredTwoFactorData();
            }
        }
        catch
        {
            return false;
        }

        var suppliedHash = HashBackupCode(suppliedCode);
        var matchIndex = -1;
        for (var index = 0; index < data.Codes.Length; index++)
        {
            var storedCode = data.Codes[index];
            var candidateHash = storedCode.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)
                ? storedCode
                : HashBackupCode(storedCode);

            var left = Encoding.ASCII.GetBytes(candidateHash.ToUpperInvariant());
            var right = Encoding.ASCII.GetBytes(suppliedHash.ToUpperInvariant());
            if (left.Length == right.Length && CryptographicOperations.FixedTimeEquals(left, right))
            {
                matchIndex = index;
                break;
            }
        }

        if (matchIndex < 0)
            return false;

        data.Codes = data.Codes.Where((_, index) => index != matchIndex).ToArray();
        user.TwoFactorBackupCodesJson = JsonSerializer.Serialize(data);
        return true;
    }

    private static byte[] DecodeBase32(string input)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var normalized = input.Trim().TrimEnd('=');
        var output = new List<byte>();
        var buffer = 0;
        var bits = 0;

        foreach (var character in normalized)
        {
            var value = alphabet.IndexOf(char.ToUpperInvariant(character));
            if (value < 0)
                throw new FormatException("Invalid Base32 secret.");

            buffer = (buffer << 5) | value;
            bits += 5;
            if (bits >= 8)
            {
                bits -= 8;
                output.Add((byte)((buffer >> bits) & 0xff));
            }
        }

        return output.ToArray();
    }
}
