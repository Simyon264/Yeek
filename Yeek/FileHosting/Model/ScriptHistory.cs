namespace Yeek.FileHosting.Model;

public class ScriptHistory
{
    public Guid Id { get; set; }
    public bool WasApplied { get; set; }
    public Guid RunBy { get; set; }
    public DateTime ExecutedOn { get; set; }
    public int Affected { get; set; }
}