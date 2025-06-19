using Ookii.Dialogs.Wpf;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace CareLoader
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            var downloadsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            PathBox.Text = downloadsPath;
        }

        private string? ExtractYtDlpExe()
        {
            var tempPath = Path.Combine(Path.GetTempPath(), "yt-dlp.exe");

            if (!File.Exists(tempPath))
            {
                using (var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("CareLoader.yt-dlp.exe"))
                {
                    if (resource == null)
                    {
                        MessageBox.Show("Embedded Resource 'yt-dlp.exe' nicht gefunden!");
                        return null;
                    }

                    using (var file = new FileStream(tempPath, FileMode.Create, FileAccess.Write))
                    {
                        resource.CopyTo(file);
                    }
                }
            }

            return tempPath;
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new VistaFolderBrowserDialog();
            if (dialog.ShowDialog() == true)
            {
                PathBox.Text = dialog.SelectedPath;
            }
        }

        private void ClearPlaceholder(object sender, RoutedEventArgs e)
        {
            if (UrlBox.Text == "https://www.youtube.com/watch?v=...")
                UrlBox.Clear();
        }

        private void LoadThumbnail(string url)
        {
            try
            {
                var match = Regex.Match(url, @"(?:youtu\.be/|v=)([a-zA-Z0-9_-]{11})");
                if (!match.Success) return;

                string videoId = match.Groups[1].Value;
                string thumbUrl = $"https://img.youtube.com/vi/{videoId}/maxresdefault.jpg";

                BitmapImage bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(thumbUrl);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();

            }
            catch { /* ignoriere Fehler */ }
        }

        private void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            var url = UrlBox.Text;
            var format = ((ComboBoxItem)FormatBox.SelectedItem)?.Content.ToString() ?? "mp4";
            var quality = ((ComboBoxItem)QualityBox.SelectedItem)?.Content.ToString() ?? "best";
            var savePath = PathBox.Text;

            if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(savePath))
            {
                MessageBox.Show("Bitte Link und Speicherort angeben!");
                return;
            }

            var ytDlpPath = ExtractYtDlpExe();
            if (ytDlpPath == null)
                return;

            string outputTemplate = Path.Combine(savePath, "%(title)s.%(ext)s");
            string args;

            if (format == "mp3")
            {
                args = $"--extract-audio --audio-format mp3 -o \"{outputTemplate}\" \"{url}\"";
            }
            else
            {
                string ytQuality = quality switch
                {
                    "1080p" => "bestvideo[height<=1080]+bestaudio/best[height<=1080]",
                    "720p" => "bestvideo[height<=720]+bestaudio/best[height<=720]",
                    "worst" => "worst",
                    _ => "best"
                };

                args = $"-f \"{ytQuality}\" -o \"{outputTemplate}\" \"{url}\"";
            }

            OutputBox.Clear();
            DownloadProgressBar.Value = 0;

            LoadThumbnail(url);

            var psi = new ProcessStartInfo
            {
                FileName = ytDlpPath,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            var process = new Process { StartInfo = psi };

            process.OutputDataReceived += (s, ev) =>
            {
                if (!string.IsNullOrWhiteSpace(ev.Data))
                {
                    Dispatcher.Invoke(() =>
                    {
                        OutputBox.AppendText(ev.Data + Environment.NewLine);
                        OutputBox.ScrollToEnd();

                        var match = Regex.Match(ev.Data, @"\b(\d{1,3}\.\d)%");
                        if (match.Success && double.TryParse(match.Groups[1].Value, out var percent))
                        {
                            DownloadProgressBar.Value = percent;
                        }
                    });
                }
            };

            process.ErrorDataReceived += (s, ev) =>
            {
                if (!string.IsNullOrWhiteSpace(ev.Data))
                {
                    Dispatcher.Invoke(() =>
                    {
                        OutputBox.AppendText("Fehler: " + ev.Data + Environment.NewLine);
                        OutputBox.ScrollToEnd();
                    });
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }
    }
}
