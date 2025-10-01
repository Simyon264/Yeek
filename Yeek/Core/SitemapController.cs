using System.Text;
using Microsoft.AspNetCore.Mvc;
using Yeek.FileHosting.Repositories;

namespace Yeek.Core;

[ApiController]
[Route("/")]
public class SitemapController : ControllerBase
{
    private readonly IFileRepository _fileRepository;
    private const int MaxUrlsPerSitemap = 50000; // Sitemap limit
    private readonly string[] _staticPages = ["/", "/privacy", "/about", "/faq"];

    public SitemapController(IFileRepository fileRepository)
    {
        _fileRepository = fileRepository;
    }

    [HttpGet("robots.txt")]
    public IActionResult Robots()
    {
        var sb = new StringBuilder();

        // Allow all
        sb.AppendLine("User-agent: *");
        sb.AppendLine("Disallow:");

        sb.AppendLine($"Sitemap: {Request.Scheme}://{Request.Host}/sitemap_index.xml");

        return Content(sb.ToString(), "text/plain");
    }

    [HttpGet("/sitemap.xml")]
    [HttpGet("/sitemap_index.xml")]
    public async Task<ActionResult> Sitemap()
    {
        var allIds = await _fileRepository.GetAllIdsAsync();
        var totalSitemaps = (int)Math.Ceiling(allIds.Length / (double)MaxUrlsPerSitemap);

        var sb = new StringBuilder();
        sb.AppendLine("""<?xml version="1.0" encoding="UTF-8"?>""");
        sb.AppendLine("""<sitemapindex xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">""");

        for (var i = 0; i < totalSitemaps; i++)
        {
            sb.AppendLine("  <sitemap>");
            sb.AppendLine($"    <loc>{Request.Scheme}://{Request.Host}/sitemap-{i + 1}.xml</loc>");
            sb.AppendLine($"    <lastmod>{DateTime.UtcNow:yyyy-MM-dd}</lastmod>");
            sb.AppendLine("  </sitemap>");
        }

        sb.AppendLine("</sitemapindex>");
        return Content(sb.ToString(), "application/xml", Encoding.UTF8);
    }

    [HttpGet("/sitemap-{index:int}.xml")]
    public async Task<IActionResult> SitemapChunk(int index)
    {
        if (index < 1)
            return BadRequest("Invalid sitemap index.");

        var allIds = await _fileRepository.GetAllIdsAsync();

        var skip = (index - 1) * MaxUrlsPerSitemap;
        if (skip >= allIds.Length)
            return NotFound("Sitemap index out of range.");

        var chunkIds = allIds.Skip(skip).Take(MaxUrlsPerSitemap).ToArray();

        var sb = new StringBuilder();
        sb.AppendLine("""<?xml version="1.0" encoding="UTF-8"?>""");
        sb.AppendLine("""<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">""");

        if (index == 1)
        {
            foreach (var page in _staticPages)
            {
                sb.AppendLine("  <url>");
                sb.AppendLine($"    <loc>{Request.Scheme}://{Request.Host}{page}</loc>");
                sb.AppendLine("    <priority>1.0</priority>");
                sb.AppendLine("    <changefreq>monthly</changefreq>");
                sb.AppendLine("  </url>");
            }
        }

        foreach (var fileId in chunkIds)
        {
            var file = await _fileRepository.GetUploadedFileAsync(fileId);

            sb.AppendLine("  <url>");
            sb.AppendLine($"    <loc>{Request.Scheme}://{Request.Host}/{file.Id}</loc>");
            sb.AppendLine($"    <lastmod>{file.MostRecentRevision.UpdatedOn:yyyy-MM-dd}</lastmod>");
            sb.AppendLine("    <changefreq>weekly</changefreq>");
            sb.AppendLine("    <priority>0.9</priority>");
            sb.AppendLine("  </url>");
        }

        sb.AppendLine("</urlset>");
        return Content(sb.ToString(), "application/xml", Encoding.UTF8);
    }
}