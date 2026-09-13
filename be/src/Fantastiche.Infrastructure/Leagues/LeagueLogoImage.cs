using System.Buffers.Binary;
using Fantastiche.Core.Exceptions;

namespace Fantastiche.Infrastructure.Leagues;

public static class LeagueLogoImage
{
    public const int MaxBytes = 2 * 1024 * 1024;

    public static (string ContentType, string Extension) Identify(byte[] content)
    {
        if (content.Length > MaxBytes)
            throw new DomainException("league.logo_too_large", "Il logo deve pesare al massimo 2 MB.");
        var bytes = content.AsSpan();
        // Il formato deriva dal contenuto, mai dal nome o dal MIME dichiarati dal browser.
        if (bytes.Length >= 45 && bytes[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })
            && bytes.Slice(12, 4).SequenceEqual("IHDR"u8)
            && bytes.Slice(bytes.Length - 8, 4).SequenceEqual("IEND"u8))
            return ("image/png", "png");
        if (bytes.Length >= 4 && bytes[0] == 0xff && bytes[1] == 0xd8 && bytes[2] == 0xff
            && bytes[^2] == 0xff && bytes[^1] == 0xd9)
            return ("image/jpeg", "jpg");
        if (bytes.Length >= 20 && bytes[..4].SequenceEqual("RIFF"u8)
            && bytes.Slice(8, 4).SequenceEqual("WEBP"u8)
            && BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(4, 4)) == bytes.Length - 8
            && (bytes.Slice(12, 4).SequenceEqual("VP8 "u8) || bytes.Slice(12, 4).SequenceEqual("VP8L"u8) || bytes.Slice(12, 4).SequenceEqual("VP8X"u8)))
            return ("image/webp", "webp");
        throw new DomainException("league.logo_invalid", "Scegli un’immagine PNG, JPEG o WebP valida.");
    }
}
