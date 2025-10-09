using System.Text;
using Microsoft.ClearScript;
using Microsoft.ClearScript.JavaScript;
using Yeek.FileHosting.Model;

namespace Yeek.FileHosting.JavaScript.Objects;

/// <summary>
/// Represents a revision for a given file.
/// </summary>
public class JsRevision
{
    public int RevisionNumber { get; private set; }
    public JsUser User { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public string TrackName { get; private set; }
    public string? AlbumName { get; private set; }
    public object ArtistNames { get; private set; }
    public string? Description { get; private set; }
    public string ChangeSummary { get; private set; }

    internal JsRevision(FileRevision revision, JsUser uploadedUser, ScriptEngine engine)
    {
        RevisionNumber = revision.RevisionId;
        User = uploadedUser;
        UpdatedAt = revision.UpdatedOn;
        TrackName = revision.TrackName;
        AlbumName = revision.AlbumName;
        ArtistNames = revision.ArtistNames.ToScriptArray(engine);
        Description = revision.Description;
        ChangeSummary = revision.ChangeSummary;
    }

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.Append($"{RevisionNumber}: ");
        if (ArtistNames is ScriptObject scriptArray)
        {
            if (scriptArray.ToHostArray<string>().Length > 0)
                sb.Append($"{string.Join(", ", scriptArray.ToHostArray<string>())} ");
        }


        if (AlbumName is not null)
            sb.Append($"{AlbumName} ");

        sb.Append(TrackName);

        return sb.ToString();
    }
}