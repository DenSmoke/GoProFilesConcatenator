using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace GoProFilesConcatenator;

/// <summary>
///     Class for operations with ffmpeg usage
/// </summary>
public static class Ffmpeg
{
    private const string VIDEOLIST_FILENAME = "vidlist.txt";
    private const string FFMPEG_EXE = "ffmpeg.exe";

    /// <summary>
    ///     Concatenate <paramref name="files"/> and save to <paramref name="output"/> directory
    /// </summary>
    /// <param name="files">Collection of file paths</param>
    /// <param name="output">Output directory</param>
    public static async Task ConcatFilesAsync(IEnumerable<string> files, string output, CancellationToken ct = default)
    {
        StreamWriter file = File.CreateText(VIDEOLIST_FILENAME);

        await using (file.ConfigureAwait(false))
        {
            foreach (string path in files)
                await file.WriteLineAsync($"file '{path}'".AsMemory(), ct).ConfigureAwait(false);
        }

        string args = $"-f concat -safe 0 -i {VIDEOLIST_FILENAME} -c copy -metadata:s:v:0 rotate=180 \"{output}\"";

        using Process process = new()
        {
            StartInfo = new ProcessStartInfo(FFMPEG_EXE)
            {
                UseShellExecute = true,
                Arguments = args
            }
        };
        process.Start();

        try
        {
            await process.WaitForExitAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            process.Kill();
        }
    }
}
