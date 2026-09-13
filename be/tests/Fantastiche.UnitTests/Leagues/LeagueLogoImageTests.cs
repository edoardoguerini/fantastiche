using Fantastiche.Core.Exceptions;
using Fantastiche.Infrastructure.Leagues;

namespace Fantastiche.UnitTests.Leagues;

public sealed class LeagueLogoImageTests
{
    [Theory]
    [InlineData("iVBORw0KGgoAAAANSUhEUgAAAAIAAAACCAIAAAD91JpzAAAAEklEQVR4nGOsjZrGwMDAxAAGABEtAXGyBJ9AAAAAAElFTkSuQmCC", "image/png", "png")]
    [InlineData("/9j/4AAQSkZJRgABAQAAAQABAAD/2wBDAAgGBgcGBQgHBwcJCQgKDBQNDAsLDBkSEw8UHRofHh0aHBwgJC4nICIsIxwcKDcpLDAxNDQ0Hyc5PTgyPC4zNDL/2wBDAQkJCQwLDBgNDRgyIRwhMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjL/wAARCAACAAIDASIAAhEBAxEB/8QAHwAAAQUBAQEBAQEAAAAAAAAAAAECAwQFBgcICQoL/8QAtRAAAgEDAwIEAwUFBAQAAAF9AQIDAAQRBRIhMUEGE1FhByJxFDKBkaEII0KxwRVS0fAkM2JyggkKFhcYGRolJicoKSo0NTY3ODk6Q0RFRkdISUpTVFVWV1hZWmNkZWZnaGlqc3R1dnd4eXqDhIWGh4iJipKTlJWWl5iZmqKjpKWmp6ipqrKztLW2t7i5usLDxMXGx8jJytLT1NXW19jZ2uHi4+Tl5ufo6erx8vP09fb3+Pn6/8QAHwEAAwEBAQEBAQEBAQAAAAAAAAECAwQFBgcICQoL/8QAtREAAgECBAQDBAcFBAQAAQJ3AAECAxEEBSExBhJBUQdhcRMiMoEIFEKRobHBCSMzUvAVYnLRChYkNOEl8RcYGRomJygpKjU2Nzg5OkNERUZHSElKU1RVVldYWVpjZGVmZ2hpanN0dXZ3eHl6goOEhYaHiImKkpOUlZaXmJmaoqOkpaanqKmqsrO0tba3uLm6wsPExcbHyMnK0tPU1dbX2Nna4uPk5ebn6Onq8vP09fb3+Pn6/9oADAMBAAIRAxEAPwDKooor1TsP/9k=", "image/jpeg", "jpg")]
    [InlineData("UklGRjIAAABXRUJQVlA4ICYAAABwAQCdASoCAAIAAUAmJYgCdAFAAAD+645f1d+dv1PO7yOd6VGAAA==", "image/webp", "webp")]
    public void RecognizesRasterContentWithoutTrustingTheFileName(string base64, string mime, string extension)
    {
        var result = LeagueLogoImage.Identify(Convert.FromBase64String(base64));
        Assert.Equal(mime, result.ContentType);
        Assert.Equal(extension, result.Extension);
    }

    [Fact]
    public void RejectsOversizedImages()
    {
        var error = Assert.Throws<DomainException>(() => LeagueLogoImage.Identify(new byte[2 * 1024 * 1024 + 1]));
        Assert.Equal("league.logo_too_large", error.Code);
    }

    [Theory]
    [InlineData("PHN2Zz48L3N2Zz4=")]
    [InlineData("iVBORw0KGgo=")]
    [InlineData("/9j/")]
    [InlineData("")]
    public void RejectsSvgAndTruncatedContent(string base64)
    {
        var error = Assert.Throws<DomainException>(() => LeagueLogoImage.Identify(Convert.FromBase64String(base64)));
        Assert.Equal("league.logo_invalid", error.Code);
    }
}
