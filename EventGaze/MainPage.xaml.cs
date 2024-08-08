using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using VersOne.Epub;
using UglyToad.PdfPig;

namespace EventGaze
{
    public partial class MainPage : ContentPage
    {
        private string _text;
        private int _index;
        private CancellationTokenSource _cancellationTokenSource;
        private bool _isPaused;
        private string[] _words;

        public MainPage()
        {
            InitializeComponent();
        }

        private async void OnUploadClicked(object sender, EventArgs e)
        {
            var result = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Pick a PDF or EPUB file",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.iOS, new[] { "public.pdf", "org.idpf.epub-container" } },
                    { DevicePlatform.Android, new[] { "application/pdf", "application/epub+zip" } }
                })
            });

            if (result != null)
            {
                var fileExtension = Path.GetExtension(result.FileName).ToLower();
                if (fileExtension == ".pdf")
                {
                    _text = await ExtractTextFromPdf(result.FullPath);
                }
                else if (fileExtension == ".epub")
                {
                    _text = ExtractTextFromEpub(result.FullPath);
                }

                lblStatus.Text = "File loaded. Ready to start.";
                _words = _text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            }
        }

        private async Task<string> ExtractTextFromPdf(string filepath)
        {
            using (var document = PdfDocument.Open(filepath))
            {
                return string.Join(" ", document.GetPages().SelectMany(page => page.GetWords()));
            }
        }

        private string ExtractTextFromEpub(string filepath)
        {
            EpubBook epubBook = EpubReader.ReadBook(filepath);
            return string.Join(" ", epubBook.ReadingOrder.Select(contentFile => contentFile.ReadContentAsText()));
        }

        private async void OnStartClicked(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(_text))
            {
                _isPaused = false;
                _cancellationTokenSource = new CancellationTokenSource();
                int wpm = (int)sliderWpm.Value;
                await RsvpReaderFunc(_words, wpm, _cancellationTokenSource.Token);
            }
        }

        private void OnPauseClicked(object sender, EventArgs e)
        {
            _isPaused = true;
        }

        private void OnResumeClicked(object sender, EventArgs e)
        {
            _isPaused = false;
        }

        private void OnStopClicked(object sender, EventArgs e)
        {
            _cancellationTokenSource?.Cancel();
        }

        private async void OnWpmChanged(object sender, ValueChangedEventArgs e)
        {
            lblWpm.Text = $"Words per minute: {(int)e.NewValue}";
        }

        private async Task RsvpReaderFunc(string[] words, int wpm, CancellationToken token)
        {
            var delay = 60000 / wpm;

            while (_index < words.Length)
            {
                if (token.IsCancellationRequested)
                {
                    break;
                }

                if (_isPaused)
                {
                    await Task.Delay(100); // Check every 100ms if it's still paused
                    continue;
                }

                lblRsvp.Text = words[_index];
                _index++;
                await Task.Delay(delay);
            }
        }
    }

    public static class EpubContentFileExtensions
    {
        public static string ReadContentAsText(this EpubLocalTextContentFile contentFile)
        {
            return contentFile.Content;
        }
    }
}
