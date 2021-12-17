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
    private readonly Regex _goProFileRegex = new(@"GX(\d{2})(\d{4})\.MP4", RegexOptions.Compiled);
    private CancellationTokenSource _cts = new();

    public MainWindow()
    {
        InitializeComponent();
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            MessageBox.Show(e.ExceptionObject.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        Closed += OnWindowClosed;
    }

    private void OnWindowClosed(object sender, EventArgs e)
    {
        _cts.Cancel();
        _cts.Dispose();
    }

    /// <summary>
    ///     Concat GoPro video files
    /// </summary>
    /// <param name="inputDir">Input directory</param>
    /// <param name="outputDir">Output directory</param>
    public async Task ConcatGoProFilesAsync(string inputDir, string outputDir, CancellationToken ct)
    {
        string[] files = Directory.GetFiles(inputDir);
        Dictionary<int, List<GoProFile>> dict = new();
        for (int i = 0; i < files.Length; i++)
        {
            string path = files[i];
            Match match = _goProFileRegex.Match(path);
            if (match.Success)
            {
                byte number = byte.Parse(match.Groups[1].Value);
                ushort groupNumber = ushort.Parse(match.Groups[2].Value);
                GoProFile goProFile = new(number, groupNumber, path);
                if (dict.ContainsKey(goProFile.GroupNumber))
                    dict[goProFile.GroupNumber].Add(goProFile);
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
                await Ffmpeg.ConcatFilesAsync(paths, outputFile, ct).ConfigureAwait(false);
            }
        }
    }

    private void LogFileConvert(string input, string output) => Dispatcher.Invoke(() =>
        Output.AppendText($"{Environment.NewLine}{input} => {output}{Environment.NewLine}"));

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
                    await ConcatGoProFilesAsync(inputFolderDialog.FileName, outputFolderDialog.FileName, _cts.Token).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() =>
                    {
                        Output.AppendText(Environment.NewLine);
                        Output.AppendText(ex.ToString());
                    });
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
