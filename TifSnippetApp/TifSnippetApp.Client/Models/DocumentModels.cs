using System.Collections.Generic;

namespace TifSnippetApp.Client.Models
{
    public class OCRDocument
    {
        public string ServiceVersion { get; set; }
        public string ModelId { get; set; }
        public string Content { get; set; }
        public List<OCRPage> Pages { get; set; }
    }

    public class OCRPage
    {
        public int PageNumber { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
        public List<OCRLine> Lines { get; set; }
    }

    public class OCRLine
    {
        public string Content { get; set; }
        public List<OCRPoint> BoundingPolygon { get; set; }
    }

    public class OCRPoint
    {
        public float X { get; set; }
        public float Y { get; set; }
    }

    public class SnippetInfo
    {
        public int PageIndex { get; set; }
        public int LineIndex { get; set; }
        public string FieldName { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public float Confidence { get; set; }
        public string ImageBase64 { get; set; } = string.Empty;
        public int TotalLines { get; set; }
    }

    public enum SnippetStatus
    {
        None,
        Accepted,
        Edited,
        RejectedBlank,
        RejectedIllegible,
        RejectedOther
    }
    public class SnippetSubmission
    {
        public int LineIndex { get; set; }
        public string CapturedContent { get; set; } = string.Empty;
        public SnippetStatus Status { get; set; }
        public string Username { get; set; } = "macdgeo";
    }
}
