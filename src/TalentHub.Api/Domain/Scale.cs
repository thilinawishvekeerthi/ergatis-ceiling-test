namespace TalentHub.Api.Domain;

public class Scale
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ICollection<ScaleLevel> Levels { get; set; } = new List<ScaleLevel>();
}

public class ScaleLevel
{
    public int Id { get; set; }
    public int ScaleId { get; set; }
    public Scale Scale { get; set; } = null!;
    public string Label { get; set; } = string.Empty;
    public int Value { get; set; }
}
