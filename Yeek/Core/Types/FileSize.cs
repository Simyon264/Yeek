using System.ComponentModel;
using System.Globalization;

namespace Yeek.Core.Types;

[TypeConverter(typeof(FileSizeTypeConverter))]
public sealed record FileSize(long Bytes = 0)
{
    public static FileSize Zero => new(0);

    public static implicit operator long(FileSize size) => size.Bytes;

    public override string ToString()
    {
        return Bytes switch
        {
            0 => "0",
            > 1024L*1024L*1024L*1024L => $"{Bytes/1024/1024/1024}PB",
            > 1024*1024*1024 => $"{Bytes/1024/1024/1024}GB",
            > 1024*1024 => $"{Bytes/1024/1024}MB",
            > 1024 => $"{Bytes/1024}KB",
            _ => $"{Bytes}B"
        };
    }

    public static FileSize Parse(string str)
    {
        if (string.IsNullOrWhiteSpace(str))
            return Zero;

        var span = str.AsSpan();
        var index = 0;

        while (span.Length > index && char.IsDigit(span[index]))
            index++;

        var number = span[..index];
        var unit = span[index..].Trim();

        if (number.Length == 0)
            return Zero;

        var bytes = unit switch
        {
            "PiB" or "PB" => long.Parse(number) * 1024 * 1024 * 1024 * 1024 * 1024,
            "TiB" or "TB" => long.Parse(number) * 1024 * 1024 * 1024 * 1024,
            "GiB" or "GB" => long.Parse(number) * 1024 * 1024 * 1024,
            "MiB" or "MB" => long.Parse(number) * 1024 * 1024,
            "KiB" or "KB" => long.Parse(number) * 1024,
            _ => long.Parse(number)
        };

        return new FileSize(bytes);
    }
}


public sealed class FileSizeTypeConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
    {
        return sourceType == typeof(string);
    }

    public override object ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is not string str)
            throw new InvalidOperationException();

        return FileSize.Parse(str);
    }
}