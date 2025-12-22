namespace RecordingsHelper.Core.Models;

public class TranscriptionProgress
{
    public double ProgressPercentage { get; set; }
    public int SegmentsRecognized { get; set; }
    public string Message { get; set; } = string.Empty;
}
