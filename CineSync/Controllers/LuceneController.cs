using CineSync.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CineSync.Controllers;

public class LuceneController : Controller
{
    private readonly LuceneService _lucene;

    public LuceneController(LuceneService lucene)
    {
        _lucene = lucene;
    }

    public IActionResult Index(string? q, string sort = "desc")
    {
        ViewBag.Query = q;
        ViewBag.Sort = sort;
        ViewBag.IndexExists = _lucene.IndexExists();

        if (string.IsNullOrWhiteSpace(q))
        {
            return View(new List<LuceneSearchResult>());
        }

        var results = _lucene.Search(q, sort);
        return View(results);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RebuildIndex()
    {
        var indexedCount = await _lucene.BuildIndexAsync();
        TempData["IndexResult"] = $"Index rebuilt successfully. {indexedCount} documents indexed.";

        return RedirectToAction(nameof(Index));
    }
}
