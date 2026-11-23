namespace Mp3MetadataCorrector.Models;

public class DownloadProgressReport
{
    public double Percentage { get; set; }
    public string Speed { get; set; } = string.Empty;
    public string Eta { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string RawLine { get; set; } = string.Empty;
    public bool IsComplete { get; set; }
    public string? DownloadedFilePath { get; set; }
}
