using System.Text.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using TifSnippetApp.Client.Models;

namespace TifSnippetApp.Services
{
    public class SnippetService
    {
        private readonly string _datasetPath;
        private readonly string _csvPath;
        private readonly string _resultCsvPath;
        private List<CsvRecord>? _records;
        private readonly HashSet<int> _capturedLineIndices = new();
        private readonly object _csvLock = new object();

        public class CsvRecord
        {
            public string Filename { get; set; } = "";
            public int PageNumber { get; set; }
            public string FieldName { get; set; } = "";
            public string Content { get; set; } = "";
            public float Confidence { get; set; }
            public string BoundingBoxPolygon { get; set; } = "";
        }

        public SnippetService(IConfiguration configuration)
        {
            //_datasetPath = configuration["DatasetPath"] ?? @"D:\datasets\Attestations Cleaned up\";
            _datasetPath = configuration["DatasetPath"] ?? @"D:\datasets\WWI Data\Attestations_CleanedUp\";
            _csvPath = Path.Combine(_datasetPath, "AnalysisResults.csv");
            _resultCsvPath = Path.Combine(_datasetPath, "CaptureResults.csv");
        }

        private async Task LoadRecordsAsync()
        {
            if (_records != null) return;

            var content = await File.ReadAllTextAsync(_csvPath);
            _records = new List<CsvRecord>();

            var lines = ParseCsvContent(content);
            if (lines.Count == 0) return;

            // Filename,PageNumber,FieldName,Content,Confidence,BoundingBoxPolygon
            for (int i = 1; i < lines.Count; i++)
            {
                var parts = lines[i];
                if (parts.Count < 6) continue;

                _records.Add(new CsvRecord
                {
                    Filename = parts[0],
                    PageNumber = (int)Math.Round(float.TryParse(parts[1], out var p) ? p : 0),
                    FieldName = parts[2],
                    Content = parts[3],
                    Confidence = float.TryParse(parts[4], out var c) ? c : 0,
                    BoundingBoxPolygon = parts[5]
                });
            }

            // Load existing results
            if (File.Exists(_resultCsvPath))
            {
                var resultContent = await File.ReadAllTextAsync(_resultCsvPath);
                var resultLines = ParseCsvContent(resultContent);
                
                for (int i = 1; i < resultLines.Count; i++) // Skip header
                {
                    var parts = resultLines[i];
                    if (parts.Count >= 9 && int.TryParse(parts[8], out var index))
                    {
                        _capturedLineIndices.Add(index);
                    }
                    else if (parts.Count == 8 && int.TryParse(parts[7], out var oldIndex))
                    {
                        // Fallback for old 8-column format
                        _capturedLineIndices.Add(oldIndex);
                    }
                }
            }
        }

        private List<List<string>> ParseCsvContent(string content)
        {
            var rows = new List<List<string>>();
            var currentRow = new List<string>();
            var currentField = new System.Text.StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < content.Length; i++)
            {
                char c = content[i];

                if (c == '\"')
                {
                    if (inQuotes && i + 1 < content.Length && content[i + 1] == '\"')
                    {
                        currentField.Append('\"'); // Escaped quote
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    currentRow.Add(currentField.ToString());
                    currentField.Clear();
                }
                else if ((c == '\n' || c == '\r') && !inQuotes)
                {
                    if (c == '\r' && i + 1 < content.Length && content[i + 1] == '\n') i++;
                    
                    if (currentField.Length > 0 || currentRow.Count > 0)
                    {
                        currentRow.Add(currentField.ToString());
                        rows.Add(new List<string>(currentRow));
                        currentRow.Clear();
                        currentField.Clear();
                    }
                }
                else
                {
                    currentField.Append(c);
                }
            }

            if (currentField.Length > 0 || currentRow.Count > 0)
            {
                currentRow.Add(currentField.ToString());
                rows.Add(currentRow);
            }

            return rows;
        }

        public async Task<List<SnippetInfo>> GetSnippetsAsync(int startIndex, int count)
        {
            await LoadRecordsAsync();
            if (_records == null) return new List<SnippetInfo>();

            var results = new List<SnippetInfo>();
            int foundCount = 0;

            // Search forward from the absolute startIndex until we have 'count' uncaptured snippets
            for (int i = startIndex; i < _records.Count; i++)
            {
                if (_capturedLineIndices.Contains(i)) continue;

                var record = _records[i];
                var imagePath = Path.Combine(_datasetPath, record.Filename);

                if (!File.Exists(imagePath)) continue;

                try
                {
                    using var image = await Image.LoadAsync(imagePath);

                    var coordMatches = System.Text.RegularExpressions.Regex.Matches(record.BoundingBoxPolygon, @"-?\d+(\.\d+)?");
                    var coords = coordMatches.Cast<System.Text.RegularExpressions.Match>().Select(m => float.Parse(m.Value)).ToList();
                    if (coords.Count < 8) continue;

                    var xs = new List<float> { coords[0], coords[2], coords[4], coords[6] };
                    var ys = new List<float> { coords[1], coords[3], coords[5], coords[7] };

                    var minX = xs.Min();
                    var minY = ys.Min();
                    var maxX = xs.Max();
                    var maxY = ys.Max();

                    var width = maxX - minX;
                    var height = maxY - minY;

                    var rect = new Rectangle((int)minX, (int)minY, (int)Math.Min(width, image.Width - minX), (int)Math.Min(height, image.Height - minY));
                    using var cropped = image.Clone(x => x.Crop(rect));

                    using var ms = new MemoryStream();
                    await cropped.SaveAsPngAsync(ms);
                    var base64 = Convert.ToBase64String(ms.ToArray());

                    results.Add(new SnippetInfo
                    {
                        PageIndex = record.PageNumber,
                        LineIndex = i,
                        FieldName = record.FieldName,
                        Content = record.Content,
                        Confidence = record.Confidence,
                        ImageBase64 = $"data:image/png;base64,{base64}",
                        TotalLines = _records.Count - _capturedLineIndices.Count
                    });

                    foundCount++;
                    if (foundCount >= count) break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing record {i}: {ex.Message}");
                    continue;
                }
            }

            return results;
        }

        public async Task SaveResultAsync(SnippetSubmission submission)
        {
            await LoadRecordsAsync();
            if (_records == null || submission.LineIndex < 0 || submission.LineIndex >= _records.Count) return;

            var record = _records[submission.LineIndex];
            var folderName = Path.GetFileName(_datasetPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            // Folder,Filename,FieldName,OriginalContent,CapturedContent,Status,User,Timestamp,LineIndex
            var line = $"\"{folderName}\",\"{record.Filename}\",\"{record.FieldName}\",\"{record.Content.Replace("\"", "\"\"")}\",\"{submission.CapturedContent.Replace("\"", "\"\"")}\",\"{submission.Status}\",\"{submission.Username}\",\"{timestamp}\",{submission.LineIndex}";

            lock (_csvLock)
            {
                if (!File.Exists(_resultCsvPath))
                {
                    File.WriteAllText(_resultCsvPath, "Folder,Filename,FieldName,OriginalContent,CapturedContent,Status,User,Timestamp,LineIndex\n");
                }
                File.AppendAllLines(_resultCsvPath, new[] { line });
                _capturedLineIndices.Add(submission.LineIndex);
            }
        }

        public async Task<SnippetInfo?> GetSnippetAsync(int index)
        {
            var results = await GetSnippetsAsync(index, 1);
            return results.FirstOrDefault();
        }

        public async Task<string?> GetSnippetImageAsync(int index, bool expanded)
        {
            await LoadRecordsAsync();
            if (_records == null || index < 0 || index >= _records.Count) return null;

            var record = _records[index];
            var imagePath = Path.Combine(_datasetPath, record.Filename);

            if (!File.Exists(imagePath)) return null;

            using var image = await Image.LoadAsync(imagePath);

            var coordMatches = System.Text.RegularExpressions.Regex.Matches(record.BoundingBoxPolygon, @"-?\d+(\.\d+)?");
            var coords = coordMatches.Cast<System.Text.RegularExpressions.Match>().Select(m => float.Parse(m.Value)).ToList();
            if (coords.Count < 8) return null;

            var xs = new List<float> { coords[0], coords[2], coords[4], coords[6] };
            var ys = new List<float> { coords[1], coords[3], coords[5], coords[7] };

            var minX = xs.Min();
            var minY = ys.Min();
            var maxX = xs.Max();
            var maxY = ys.Max();

            var width = maxX - minX;
            var height = maxY - minY;

            if (expanded)
            {
                // Expand by 5x in each dimension (25x area), centered on the original snippet
                // New half-size = 2.5 * original dimension, so offset = 2.5 - 0.5 = 2.0 of original
                minX -= width * 2f;
                minY -= height * 2f;
                width *= 5;
                height *= 5;
            }
            else
            {
                // Use exact CSV polygon extents — no buffer added
            }

            // Clamp to image bounds
            var rectX = (int)Math.Max(0, minX);
            var rectY = (int)Math.Max(0, minY);
            var rectWidth = (int)Math.Min(image.Width - rectX, (int)width);
            var rectHeight = (int)Math.Min(image.Height - rectY, (int)height);

            if (rectWidth <= 0 || rectHeight <= 0) return null;

            using var cropped = image.Clone(x => x.Crop(new Rectangle(rectX, rectY, rectWidth, rectHeight)));

            using var ms = new MemoryStream();
            await cropped.SaveAsPngAsync(ms);
            return Convert.ToBase64String(ms.ToArray());
        }
    }
}
