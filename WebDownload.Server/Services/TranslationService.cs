using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace WebDownload.Server.Services
{
    public interface ITranslationService
    {
        /// <summary>
        /// Reads an .srt file, translates each cue's text into targetLangCode,
        /// and writes a sibling file "<name>.<targetLangCode>.srt".
        /// Returns the full path of the file that was written.
        /// </summary>
        Task<string> TranslateSrtFileAsync(string srtPath, string targetLangCode, string? sourceLangCode = null);
    }

    // NOTE: This uses Google's public/unofficial translate endpoint so the
    // project works out of the box with no API key. It is fine for personal
    // use but is rate-limited and unsupported by Google. For production use,
    // swap the body of TranslateTextAsync to call the official Cloud
    // Translation API (Google.Cloud.Translation.V2 NuGet package) with a
    // service-account key or API key stored in configuration/user-secrets
    // -- never commit it to the repo.
    public class TranslationService : ITranslationService
    {
        private static readonly Regex CueBlockSplitter = new(@"\r?\n\r?\n", RegexOptions.Compiled);
        private static readonly Regex TimestampLine = new(@"-->", RegexOptions.Compiled);

        private readonly HttpClient _httpClient;

        public TranslationService(IHttpClientFactory httpClientFactory)
        {
            _httpClient = httpClientFactory.CreateClient(nameof(TranslationService));
        }

        public async Task<string> TranslateSrtFileAsync(string srtPath, string targetLangCode, string? sourceLangCode = null)
        {
            if (!File.Exists(srtPath))
            {
                throw new FileNotFoundException("Subtitle file not found.", srtPath);
            }

            var content = await File.ReadAllTextAsync(srtPath, Encoding.UTF8);
            var blocks = CueBlockSplitter.Split(content.Trim());
            var output = new StringBuilder();

            foreach (var block in blocks)
            {
                var lines = block.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
                if (lines.Count == 0)
                {
                    continue;
                }

                // A cue block looks like:
                //   12
                //   00:00:03,120 --> 00:00:05,400
                //   Hello there
                //   how are you?
                var indexLine = lines.Count > 0 ? lines[0] : string.Empty;
                var timeLineIndex = lines.FindIndex(l => TimestampLine.IsMatch(l));
                if (timeLineIndex < 0)
                {
                    // Not a well-formed cue block, pass it through untouched.
                    output.Append(block).Append("\n\n");
                    continue;
                }

                var timeLine = lines[timeLineIndex];
                var textLines = lines.Skip(timeLineIndex + 1).ToList();
                var originalText = string.Join("\n", textLines);

                var translatedText = string.IsNullOrWhiteSpace(originalText)
                    ? originalText
                    : await TranslateTextAsync(originalText, targetLangCode, sourceLangCode);

                output.Append(indexLine).Append('\n');
                output.Append(timeLine).Append('\n');
                output.Append(translatedText).Append('\n');
                output.Append('\n');

                // Be polite to the free/unofficial endpoint.
                await Task.Delay(120);
            }

            var directory = Path.GetDirectoryName(srtPath) ?? ".";
            var fileNameNoExt = Path.GetFileNameWithoutExtension(srtPath);
            // Strip a trailing ".en" / ".th" etc. language suffix if present so we
            // don't end up with "video.en.km.srt" style names.
            fileNameNoExt = Regex.Replace(fileNameNoExt, @"\.[a-zA-Z-]{2,8}$", string.Empty);
            var outputPath = Path.Combine(directory, $"{fileNameNoExt}.{targetLangCode}.srt");

            await File.WriteAllTextAsync(outputPath, output.ToString(), new UTF8Encoding(false));
            return outputPath;
        }

        private async Task<string> TranslateTextAsync(string text, string targetLangCode, string? sourceLangCode)
        {
            var src = string.IsNullOrWhiteSpace(sourceLangCode) ? "auto" : sourceLangCode;
            var url = "https://translate.googleapis.com/translate_a/single" +
                      $"?client=gtx&sl={Uri.EscapeDataString(src)}&tl={Uri.EscapeDataString(targetLangCode)}" +
                      $"&dt=t&q={Uri.EscapeDataString(text)}";

            try
            {
                using var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                await using var stream = await response.Content.ReadAsStreamAsync();
                using var doc = await JsonDocument.ParseAsync(stream);

                // Response shape: [[["translated","original",null,null,1], ...], ...]
                var sb = new StringBuilder();
                foreach (var sentence in doc.RootElement[0].EnumerateArray())
                {
                    sb.Append(sentence[0].GetString());
                }
                return sb.ToString();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Translation failed for a cue, leaving original text. Error: {ex.Message}");
                return text;
            }
        }
    }
}
