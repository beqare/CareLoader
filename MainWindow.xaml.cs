using Ookii.Dialogs.Wpf;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace CareLoader
{
    public partial class MainWindow : Window
    {
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

        public MainWindow()
        {
            InitializeComponent();
            var downloadsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            PathBox.Text = downloadsPath;
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new VistaFolderBrowserDialog();
            if (dialog.ShowDialog() == true)
            {
                PathBox.Text = dialog.SelectedPath;
            }
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
            string args = $"-f \"{quality}\" " +
                          $"{(format == "mp3" ? "--extract-audio --audio-format mp3" : "")} " +
                          $"-o \"{outputTemplate}\" \"{url}\"";

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
                    Dispatcher.Invoke(() => OutputBox.AppendText(ev.Data + Environment.NewLine));
            };

            process.ErrorDataReceived += (s, ev) =>
            {
                if (!string.IsNullOrWhiteSpace(ev.Data))
                    Dispatcher.Invoke(() => OutputBox.AppendText("Fehler: " + ev.Data + Environment.NewLine));
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }
    }
}
