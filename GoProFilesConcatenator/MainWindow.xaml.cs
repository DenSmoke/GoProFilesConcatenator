using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.WindowsAPICodePack.Dialogs;

namespace GoProFilesConcatenator;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly Regex _goProFileRegex = MyRegex();
    private CancellationTokenSource _cts = new();

    public MainWindow()
    {
        InitializeComponent();
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            MessageBox.Show(e.ExceptionObject.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    [GeneratedRegex("G[H,X](\\d{2})(\\d{4})\\.MP4", RegexOptions.Compiled)]
    private static partial Regex MyRegex();

    /// <summary>
    ///     Concat GoPro video files
    /// </summary>
    /// <param name="inputDir">Input directory</param>
    /// <param name="outputDir">Output directory</param>
    public async Task ConcatGoProFilesAsync(string inputDir, string outputDir, bool rotate, CancellationToken ct)
    {
        Dictionary<int, List<GoProFile>> dict = new();
        string[] files = Directory.GetFiles(inputDir);

        for (int i = 0; i < files.Length; i++)
        {
            string path = files[i];
            Match match = _goProFileRegex.Match(path);

            if (match.Success)
            {
                byte number = byte.Parse(match.Groups[1].Value);
                ushort groupNumber = ushort.Parse(match.Groups[2].Value);
                GoProFile goProFile = new(number, groupNumber, path);

                if (dict.TryGetValue(goProFile.GroupNumber, out List<GoProFile> value))
                    value.Add(goProFile);
                else
                    dict[goProFile.GroupNumber] = new List<GoProFile> { goProFile };
            }
        }

        IOrderedEnumerable<int> groups = dict.Keys.OrderBy(x => x);
        int n = 1;

        foreach (int group in groups)
        {
            string outputFile = Path.Combine(outputDir, $"video{n++}.mp4");
            List<GoProFile> groupFiles = dict[group];

            if (groupFiles.Count == 1)
            {
                string oldPath = groupFiles[0].Path;
                LogFileConvert(oldPath, outputFile);
                File.Copy(oldPath, outputFile);
            }
            else
            {
                groupFiles.Sort((x, y) => x.Number.CompareTo(y.Number));
                List<string> paths = groupFiles.ConvertAll(x => x.Path);
                string input = string.Join(Environment.NewLine, paths);
                LogFileConvert(input, outputFile);
                await Ffmpeg.ConcatFilesAsync(paths, outputFile, rotate, ct).ConfigureAwait(false);
            }
        }
    }

    private void Log(string message) => Dispatcher.Invoke(() => Output.AppendText(message));

    private void LogFileConvert(string input, string output) => Log($"{Environment.NewLine}{input} => {output}{Environment.NewLine}");

    private async void OnStartClick(object sender, RoutedEventArgs e)
    {
        using CommonOpenFileDialog inputFolderDialog = new()
        {
            Title = "Input folder",
            IsFolderPicker = true
        };

        if (inputFolderDialog.ShowDialog() == CommonFileDialogResult.Ok)
        {
            using CommonOpenFileDialog outputFolderDialog = new()
            {
                Title = "Output folder",
                IsFolderPicker = true
            };

            if (outputFolderDialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                try
                {
                    Log("Start");
                    await ConcatGoProFilesAsync(inputFolderDialog.FileName, outputFolderDialog.FileName, RotateCheckBox.IsChecked ?? false, _cts.Token).ConfigureAwait(false);
                    Log("End");
                }
                catch (Exception ex)
                {
                    Log($"{Environment.NewLine}ex.ToString()");
                }
            }
        }
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        _cts.Cancel();
        _cts.Dispose();
        _cts = new CancellationTokenSource();
    }
}
