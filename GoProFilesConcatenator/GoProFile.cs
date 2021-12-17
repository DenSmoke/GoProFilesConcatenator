using System;

namespace GoProFilesConcatenator;

/// <summary>
///     Represents GoPro video file
/// </summary>
public readonly record struct GoProFile
{
    public GoProFile(byte number, ushort groupNumber, string path)
    {
        Number = number;
        GroupNumber = groupNumber;
        Path = path ?? throw new ArgumentNullException(nameof(path));
    }

    /// <summary>
    ///     Number of the file in the group
    /// </summary>
    public byte Number { get; init; }

    /// <summary>
    ///     Number of the group of files
    /// </summary>
    public ushort GroupNumber { get; init; }

    /// <summary>
    ///     Path to the file on storage
    /// </summary>
    public string Path { get; init; }
}
