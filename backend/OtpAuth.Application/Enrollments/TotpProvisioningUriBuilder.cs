using System.Text;

namespace OtpAuth.Application.Enrollments;

internal static class TotpProvisioningUriBuilder
{
    public static string Build(
        string issuer,
        string label,
        byte[] secret,
        int digits,
        int periodSeconds,
        string algorithm)
    {
        var encodedIssuer = Uri.EscapeDataString(issuer);
        var encodedLabel = Uri.EscapeDataString(label);
        var encodedSecret = Base32Encode(secret);
        var encodedAlgorithm = Uri.EscapeDataString(algorithm.ToUpperInvariant());

        return $"otpauth://totp/{encodedIssuer}:{encodedLabel}?secret={encodedSecret}&issuer={encodedIssuer}&digits={digits}&period={periodSeconds}&algorithm={encodedAlgorithm}";
    }

    private static string Base32Encode(byte[] data)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

        if (data.Length == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder((data.Length * 8 + 4) / 5);
        var buffer = (int)data[0];
        var next = 1;
        var bitsLeft = 8;

        while (bitsLeft > 0 || next < data.Length)
        {
            if (bitsLeft < 5)
            {
                if (next < data.Length)
                {
                    buffer <<= 8;
                    buffer |= data[next++] & 0xFF;
                    bitsLeft += 8;
                }
                else
                {
                    var pad = 5 - bitsLeft;
                    buffer <<= pad;
                    bitsLeft += pad;
                }
            }

            var index = 0x1F & (buffer >> (bitsLeft - 5));
            bitsLeft -= 5;
            builder.Append(alphabet[index]);
        }

        return builder.ToString();
    }
}
