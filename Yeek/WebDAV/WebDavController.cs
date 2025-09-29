using System.Net;
using System.Text;
using System.Web;
using System.Xml.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using Yeek.Configuration;
using Yeek.FileHosting.Model;

namespace Yeek.WebDAV;

[ApiController]
[Route("webdav")]
public class WebDavController : ControllerBase
{
    private readonly WebDavManager _webDavManager;
    private readonly FileConfiguration _fileConfiguration = new();
    private readonly ILogger<WebDavController> _logger;

    public WebDavController(WebDavManager webDavManager, IConfiguration configuration, ILogger<WebDavController> logger)
    {
        _webDavManager = webDavManager;
        configuration.Bind(FileConfiguration.Name, _fileConfiguration);
        _logger = logger;
    }

    [HttpOptions("{*path}")]
    public IActionResult Options(string? path)
    {
        Response.Headers.Allow = "OPTIONS, PROPFIND, GET, HEAD";
        Response.Headers["DAV"] = "1,2";
        Response.Headers["MS-Author-Via"] = "DAV";

        return Ok(); // 200
    }

    // Ok so RT tries to open the file with write access, windows translates this into a LOCK request.
    // If said request fails the file wont be opened. So:
    // Let's just tell the client the request worked! In practise, this doesn't mean anything.
    [HttpLock("{*path}")]
    public IActionResult MockLock(string? path)
    {
        path = path?.Trim('/') ?? string.Empty;

        var lockToken = $"opaquelocktoken:{Guid.NewGuid()}";

        XNamespace d = "DAV:";
        var lockDiscovery = new XElement(d + "prop",
            new XElement(d + "lockdiscovery",
                new XElement(d + "activelock",
                    new XElement(d + "locktype", new XElement(d + "write")),
                    new XElement(d + "lockscope", new XElement(d + "exclusive")),
                    new XElement(d + "depth", "infinity"),
                    new XElement(d + "owner", "Mock WebDAV Lock"),
                    new XElement(d + "timeout", "Second-3600"),
                    new XElement(d + "locktoken",
                        new XElement(d + "href", lockToken)
                    )
                )
            )
        );

        Response.Headers["Lock-Token"] = $"<{lockToken}>";

        return new ContentResult
        {
            Content = lockDiscovery.ToString(SaveOptions.DisableFormatting),
            ContentType = "application/xml; charset=utf-8",
            StatusCode = (int)HttpStatusCode.OK
        };
    }

    [HttpGet("{*path}")]
    [EnableRateLimiting("DownloadPolicy")]
    public async Task<IActionResult> GetFile(string path)
    {
        path = path?.Trim('/') ?? string.Empty;

        if (string.IsNullOrEmpty(path))
            return BadRequest("Path cannot be empty.");

        if (!_webDavManager.Ready)
        {
            Request.Headers.RetryAfter = new StringValues("10"); // try again in 10 seconds nerd.
            return StatusCode(503);
        }

        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var dir = _webDavManager.RootDirectory;
        UploadedFile? uploadedFile = null;

        for (var i = 0; i < segments.Length; i++)
        {
            var segment = segments[i];
            var isLast = (i == segments.Length - 1);

            if (!isLast)
            {
                // Traverse directories
                dir = dir?.Children.FirstOrDefault(d =>
                    string.Equals(d.Name, segment, StringComparison.OrdinalIgnoreCase));

                if (dir == null)
                    return NotFound();
            }
            else
            {
                // Last segment: try directory first
                var childDir = dir?.Children.FirstOrDefault(d =>
                    string.Equals(d.Name, segment, StringComparison.OrdinalIgnoreCase));

                if (childDir != null)
                {
                    return BadRequest("Requested path is a directory, not a file.");
                }

                uploadedFile = dir?.Files.FirstOrDefault(f =>
                    string.Equals(f.GetDownloadName(), segment, StringComparison.OrdinalIgnoreCase));

                if (uploadedFile == null)
                    return NotFound();
            }
        }

        if (uploadedFile is null)
            return NotFound();

        var trueFilePath = Path.Combine(_fileConfiguration.UserContentDirectory, uploadedFile.RelativePath);
        if (!System.IO.File.Exists(trueFilePath))
        {
            return StatusCode(500, "File does not exist in file system, but exists in DB.");
        }

        var etag = new EntityTagHeaderValue($"\"{uploadedFile.Hash}\"");

        return PhysicalFile(
            Path.GetFullPath(trueFilePath),
            "audio/midi",
            uploadedFile.GetDownloadName(),
            null,
            etag,
            true);
    }

    [HttpPropFind("{*path}")]
    public IActionResult PropFind(string? path = null)
    {
        // Normalize path
        path = path?.Trim('/') ?? string.Empty;

        if (!_webDavManager.Ready)
        {
            Request.Headers.RetryAfter = new StringValues("10"); // try again in 10 seconds nerd.
            return StatusCode(503);
        }

        // Parse Depth header
        var depthHeader = Request.Headers["Depth"].FirstOrDefault() ?? "1";
        var depth = depthHeader.Equals("infinity", StringComparison.OrdinalIgnoreCase)
            ? int.MaxValue
            : int.TryParse(depthHeader, out var d) ? d : 1;

        object? current = _webDavManager.RootDirectory;

        if (!string.IsNullOrEmpty(path))
        {
            var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var dir = _webDavManager.RootDirectory;

            for (var i = 0; i < segments.Length; i++)
            {
                var segment = segments[i];
                var isLast = (i == segments.Length - 1);

                if (!isLast)
                {
                    // Traverse directories
                    dir = dir?.Children.FirstOrDefault(d =>
                        string.Equals(d.Name, segment, StringComparison.OrdinalIgnoreCase));

                    if (dir == null)
                        return NotFound();

                    current = dir;
                }
                else
                {
                    // Last segment -> could be directory or file
                    var childDir = dir?.Children.FirstOrDefault(d =>
                        string.Equals(d.Name, segment, StringComparison.OrdinalIgnoreCase));

                    if (childDir != null)
                    {
                        current = childDir;
                    }
                    else
                    {
                        var file = dir?.Files.FirstOrDefault(f =>
                            string.Equals(f.GetDownloadName(), segment, StringComparison.OrdinalIgnoreCase));

                        if (file != null)
                        {
                            current = file;
                        }
                        else
                        {
                            return NotFound();
                        }
                    }
                }
            }
        }

        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        sb.AppendLine("<d:multistatus xmlns:d=\"DAV:\">");

        AppendResponseRecursive(sb, path, current, depth);

        sb.AppendLine("</d:multistatus>");

        return StatusCode(207, sb.ToString()); // 207 Multi-Status
    }

    /// <summary>
    /// Recursively appends WebDAV responses respecting the Depth header.
    /// </summary>
    private void AppendResponseRecursive(StringBuilder sb, string href, object resource, int depth)
    {
        if (resource is Directory dir)
        {
            // Check for valid cache
            if (dir.XmlCacheByDepth.TryGetValue(depth, out var cached))
            {
                sb.Append(cached);
                return;
            }

            var dirSb = new StringBuilder();
            AppendResponse(dirSb, href, dir);

            if (depth > 0)
            {
                foreach (var childDir in dir.Children)
                {
                    AppendResponseRecursive(dirSb, CombinePath(href, childDir.Name), childDir, depth - 1);
                }

                foreach (var file in dir.Files)
                {
                    AppendResponseRecursive(dirSb, CombinePath(href, file.GetDownloadName()), file, depth - 1);
                }
            }

            var xml = dirSb.ToString();
            dir.XmlCacheByDepth[depth] = xml;
            sb.Append(xml);
            return;
        }

        AppendResponse(sb, href, resource);
    }

    /// <summary>
    /// Appends a WebDAV response block for either a directory or file.
    /// </summary>
    private void AppendResponse(StringBuilder sb, string href, object resource)
    {
        sb.AppendLine("  <d:response>");

        var encodedHref = WebUtility.HtmlEncode("/webdav/" + HttpUtility.UrlPathEncode(href.TrimStart('/')));
        sb.AppendLine($"    <d:href>{encodedHref}</d:href>");

        sb.AppendLine("    <d:propstat>");
        sb.AppendLine("      <d:prop>");

        if (resource is Directory dir)
        {
            sb.AppendLine("        <d:resourcetype><d:collection/></d:resourcetype>");
            sb.AppendLine($"        <d:displayname>{WebUtility.HtmlEncode(dir.Name ?? "webdav")}</d:displayname>");
        }
        else if (resource is UploadedFile file)
        {
            sb.AppendLine("        <d:resourcetype/>");
            sb.AppendLine($"        <d:displayname>{WebUtility.HtmlEncode(file.GetDownloadName())}</d:displayname>");
            sb.AppendLine($"        <d:getlastmodified>{file.UploadedOn:R}</d:getlastmodified>");
            sb.AppendLine($"        <d:getcontentlength>{file.FileSize}</d:getcontentlength>");
            sb.AppendLine("        <d:getcontenttype>audio/midi</d:getcontenttype>");
            sb.AppendLine($"        <d:getetag>\"{file.Hash}\"</d:getetag>");
        }

        sb.AppendLine("      </d:prop>");
        sb.AppendLine("      <d:status>HTTP/1.1 200 OK</d:status>");
        sb.AppendLine("    </d:propstat>");
        sb.AppendLine("  </d:response>");
    }

    /// <summary>
    /// Utility for safe path joining in WebDAV context.
    /// </summary>
    private string CombinePath(string basePath, string? name)
    {
        if (string.IsNullOrEmpty(basePath)) return name ?? string.Empty;
        return $"{basePath.TrimEnd('/')}/{name}";
    }

    [HttpPut("{*path}")]
    [HttpDelete("{*path}")]
    [HttpMkCol("{*path}")]
    [HttpMove("{*path}")]
    [HttpCopy("{*path}")]
    public IActionResult Forbidden(string? path = null)
    {
        const string errorMessage = "The network share is read-only. Use the website to modify contents.";

        XNamespace d = "DAV:";
        var errorXml = new XElement(d + "error",
            new XElement(d + "forbidden"),
            new XElement(d + "responsedescription", errorMessage)
        );

        Response.Headers.Append("X-MSDAVEXT_ERROR", $"589838; {HttpUtility.UrlPathEncode(errorMessage)}");

        var xmlString = errorXml.ToString(SaveOptions.DisableFormatting);

        return new ContentResult
        {
            Content = xmlString,
            ContentType = "application/xml; charset=\"utf-8\"",
            StatusCode = (int)HttpStatusCode.Forbidden
        };
    }
}