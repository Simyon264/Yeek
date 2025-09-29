namespace Yeek.FileHosting;

public static class StringExtensions
{
    /// <summary>
    /// Gets a content type for a given extension. The extension should not have the .
    /// </summary>
    public static string GetContentTypeForExtension(this string extension)
    {
        return extension switch
        {
            "webm" => "audio/webm; codecs=opus",
            "m4a" => "audio/mp4; codecs=aac",
            "mp3" => "audio/mpeg",
            "ogg" => "audio/ogg; codecs=vorbis",
            _ => throw new InvalidOperationException("Extension doesn't have a content type set.")
        };
    }
}