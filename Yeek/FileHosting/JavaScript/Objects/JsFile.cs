using System.ComponentModel.DataAnnotations;
using JetBrains.Annotations;
using Yeek.FileHosting.Model;

namespace Yeek.FileHosting.JavaScript.Objects;

/// <summary>
/// Represents an uploaded file.
/// </summary>
public class JsFile
{
    private ScriptContext _scriptContext;

    public Guid Id { get; private set; }
    public long FileSize { get; private set;}
    public DateTime UploadedOn { get; private set;}
    public JsUser UploadedBy { get; private set;}
    public object Revisions { get; internal set; }

    internal JsFile(UploadedFile file, JsUser uploadedBy, ScriptContext scriptContext)
    {
        Id = file.Id;
        FileSize = file.FileSize;
        UploadedOn = file.UploadedOn;
        UploadedBy = uploadedBy;

        _scriptContext = scriptContext;
    }

    /// <summary>
    /// Set's the info for a given file. This will only process once the script finishes execution.
    /// </summary>
    [UsedImplicitly]
    public void SetInfo(string name, string? albumName, object artistNames, string? description, string changeSummary, bool allowOverwrite = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));
        ArgumentException.ThrowIfNullOrWhiteSpace(changeSummary, nameof(changeSummary));

        if (_scriptContext.Updates.ContainsKey(Id))
        {
            if (!allowOverwrite)
                throw new InvalidOperationException("An existing change for this file is already pending!");
            else
                _scriptContext.Updates.Remove(Id);
        }

        // Validate it.
        var form = new MidiUploadForm()
        {
            Id = Id,
            Albumname = albumName,
            Authornames = artistNames.ToHostArray<string>(),
            Description = description,
            ChangeSummary = changeSummary,
            Trackname = name
        };

        var context = new ValidationContext(form);
        var results = form.Validate(context);
        foreach (var validationResult in results)
        {
            if (validationResult.ErrorMessage != null)
            {
                throw new ValidationException(validationResult.ErrorMessage);
            }
        }

        _scriptContext.Updates.Add(Id, new QueuedUpdate(name, albumName, artistNames.ToHostArray<string>(), description, changeSummary));
    }
}