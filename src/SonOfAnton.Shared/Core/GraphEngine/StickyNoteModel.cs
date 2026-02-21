namespace SonOfAnton.Shared.Core.GraphEngine;

public class StickyNoteModel
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Content { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }
    public string Color { get; set; } = "#f9e2af";
}
