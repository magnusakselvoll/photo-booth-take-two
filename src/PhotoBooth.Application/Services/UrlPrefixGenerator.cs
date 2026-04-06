using System.Security.Cryptography;
using System.Text;

namespace PhotoBooth.Application.Services;

public static class UrlPrefixGenerator
{
    private const string Alphabet = "abcdefghijklmnopqrstuvwxyz0123456789";

    public static string Generate(string eventName, string salt, int length = 10)
    {
        // Hash the salt text to get a fixed-size binary salt
        var saltBytes = SHA256.HashData(Encoding.UTF8.GetBytes(salt ?? ""));

        // Concatenate event name bytes + binary salt, then hash
        var eventBytes = Encoding.UTF8.GetBytes(eventName ?? "");
        var combined = new byte[eventBytes.Length + saltBytes.Length];
        eventBytes.CopyTo(combined, 0);
        saltBytes.CopyTo(combined, eventBytes.Length);

        var hash = SHA256.HashData(combined);

        // Map hash bytes to alphabet characters
        var sb = new StringBuilder(length);
        for (var i = 0; i < length; i++)
        {
            sb.Append(Alphabet[hash[i] % Alphabet.Length]);
        }

        return sb.ToString();
    }
}
