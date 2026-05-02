using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Circle.Models;


namespace Circle.Services;

public class ContentManager
{
    private readonly HttpClient http;
    private readonly List<Models.Circle> circles = [];
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public ContentManager(HttpClient http)
    {
        this.http = http;
    }

    public IReadOnlyList<Models.Circle> Circles => circles;

    /// <summary>
    /// Convenience: flattened packages across all circles, in declared order.
    /// </summary>
    public IEnumerable<Package> Packages => circles.SelectMany(c => c.Packages);

    // Flat ordered list of all navigable pages across all circles/packages (depth-first),
    // built once when content loads. Hierarchy is kept untouched; only navigation order is cached.
    private readonly List<Page> flatPages = [];
    private readonly Dictionary<string, int> flatIndex = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Package> pageToPackage = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Models.Circle> pageToCircle = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IReadOnlyList<ContentNode>> pageBreadcrumb = new(StringComparer.Ordinal);

    public IReadOnlyList<Page> FlatPages => flatPages;

    /// <summary>
    /// Returns the breadcrumb ancestor chain (Circle, Package, Folder...) for a given page,
    /// excluding the page itself.
    /// </summary>
    public IReadOnlyList<ContentNode> GetBreadcrumb(Page page) =>
        pageBreadcrumb.TryGetValue(page.Id, out var b) ? b : Array.Empty<ContentNode>();

    public Models.Circle? CurrentCircle { get; private set; }
    public Package? CurrentPackage { get; private set; }
    public Page? CurrentPage { get; private set; }

    private const string CirclesBasePath = "circles";

    public event Action? OnStateChanged;

    public async Task LoadContentAsync()
    {
        var circleIds = await http.GetFromJsonAsync<List<string>>($"{CirclesBasePath}/circles.json", JsonOptions) ?? [];

        circles.Clear();
        foreach (var id in circleIds)
        {
            var circle = await LoadCircleAsync(id);
            if (circle is not null)
                circles.Add(circle);
        }
        RebuildFlatIndex();
        OnStateChanged?.Invoke();
    }

    private async Task<Models.Circle?> LoadCircleAsync(string id)
    {
        var circle = await http.GetFromJsonAsync<Models.Circle>($"{CirclesBasePath}/{id}/circle.json", JsonOptions);
        if (circle is null) return null;
        if (string.IsNullOrWhiteSpace(circle.Id)) circle.Id = id;

        var circleBase = $"{CirclesBasePath}/{circle.EffectiveFolderName}";

        // Curtain SVG lives at the circle root; default file name is "curtain.svg".
        var curtainName = string.IsNullOrWhiteSpace(circle.CurtainFileName) ? "curtain.svg" : circle.CurtainFileName!;
        circle.CurtainPath = $"{circleBase}/{curtainName}";

        // Optional default page shown when the circle itself is selected.
        // Default file name is "page.svg" (not the Models.Circle Id) to keep the on-disk layout simple.
        ResolvePageContent(circle.Content, circleBase, defaultFileName: "page.svg");

        // Load packages declared by this circle.
        var packageIds = await http.GetFromJsonAsync<List<string>>($"{circleBase}/packages/packages.json", JsonOptions) ?? [];
        foreach (var pkgId in packageIds)
        {
            var pkg = await LoadPackageAsync(circle, circleBase, pkgId);
            if (pkg is not null)
                circle.Packages.Add(pkg);
        }
        return circle;
    }

    private async Task<Package?> LoadPackageAsync(Models.Circle circle, string circleBase, string id)
    {
        var package = await http.GetFromJsonAsync<Package>($"{circleBase}/packages/{id}/package.json", JsonOptions);
        if (package is null) return null;
        package.Circle = circle;
        ResolveContentPaths(package, circleBase);
        return package;
    }

    private static void ResolveContentPaths(Package package, string circleBase)
    {
        var packageBase = $"{circleBase}/packages/{package.EffectiveFolderName}";
        // Package's own page lives at the package root. Its filename defaults to "<Id>.svg".
        ResolvePageContent(package.Content, packageBase, defaultFileName: package.Id + ".svg");
        foreach (var item in package.Items)
            ResolveItem(item, packageBase);
    }

    private static void ResolveItem(ContentItem item, string parentBase)
    {
        switch (item)
        {
            case Folder folder:
                var folderBase = $"{parentBase}/{folder.EffectiveFolderName}";
                ResolvePageContent(folder.Content, folderBase, defaultFileName: folder.Id + ".svg");
                foreach (var child in folder.Items)
                    ResolveItem(child, folderBase);
                break;
            case Page page:
                ResolvePageContent(page.Content, parentBase, defaultFileName: page.Id + ".svg");
                break;
        }
    }

    private static void ResolvePageContent(PageContent? content, string baseFolder, string defaultFileName)
    {
        if (content is null) return;
        var fileName = string.IsNullOrWhiteSpace(content.FileName) ? defaultFileName : content.FileName!;
        content.ResolvedPath = $"{baseFolder}/{fileName}";
    }

    private void RebuildFlatIndex()
    {
        flatPages.Clear();
        flatIndex.Clear();
        pageToPackage.Clear();
        pageToCircle.Clear();
        pageBreadcrumb.Clear();
        foreach (var circle in circles)
        {
            var stack = new List<ContentNode> { circle };
            VisitCircle(circle, stack);
        }
    }

    private void VisitCircle(Models.Circle circle, List<ContentNode> ancestors)
    {
        if (circle.Content is not null)
        {
            var p = new Page
            {
                Id = $"crc::{circle.Id}",
                Name = circle.Name,
                Description = circle.Description,
                Icon = circle.Icon,
                Content = circle.Content
            };
            // Circle's content page IS the circle node — its breadcrumb excludes itself.
            var snapshot = ancestors.Take(ancestors.Count - 1).ToList();
            AddFlatPage(p, circle, pkg: null, snapshot);
        }
        foreach (var package in circle.Packages)
        {
            ancestors.Add(package);
            VisitPackage(circle, package, ancestors);
            ancestors.RemoveAt(ancestors.Count - 1);
        }
    }

    private void VisitPackage(Models.Circle circle, Package pkg, List<ContentNode> ancestors)
    {
        if (pkg.Content is not null)
        {
            var p = new Page
            {
                Id = $"pkg::{pkg.Id}",
                Name = pkg.Name,
                Description = pkg.Description,
                Icon = pkg.Icon,
                Content = pkg.Content
            };
            var snapshot = ancestors.Take(ancestors.Count - 1).ToList();
            AddFlatPage(p, circle, pkg, snapshot);
        }
        foreach (var item in pkg.Items)
            VisitItem(circle, pkg, item, ancestors);
    }

    private void VisitItem(Models.Circle circle, Package pkg, ContentItem item, List<ContentNode> ancestors)
    {
        switch (item)
        {
            case Folder folder:
                ancestors.Add(folder);
                if (folder.Content is not null)
                {
                    var p = new Page
                    {
                        Id = $"fld::{folder.Id}",
                        Name = folder.Name,
                        Description = folder.Description,
                        Icon = folder.Icon,
                        Content = folder.Content
                    };
                    var snapshot = ancestors.Take(ancestors.Count - 1).ToList();
                    AddFlatPage(p, circle, pkg, snapshot);
                }
                foreach (var child in folder.Items)
                    VisitItem(circle, pkg, child, ancestors);
                ancestors.RemoveAt(ancestors.Count - 1);
                break;
            case Page page:
                AddFlatPage(page, circle, pkg, ancestors);
                break;
        }
    }

    private void AddFlatPage(Page page, Models.Circle circle, Package? pkg, List<ContentNode> ancestors)
    {
        flatIndex[page.Id] = flatPages.Count;
        flatPages.Add(page);
        pageToCircle[page.Id] = circle;
        if (pkg is not null)
            pageToPackage[page.Id] = pkg;
        pageBreadcrumb[page.Id] = ancestors.ToArray();
    }

    public void SelectCircle(Models.Circle? circle)
    {
        CurrentCircle = circle;
        CurrentPackage = null;
        CurrentPage = null;
        OnStateChanged?.Invoke();
    }

    public void SelectPackage(Package? package)
    {
        CurrentPackage = package;
        if (package?.Circle is not null)
            CurrentCircle = package.Circle;
        CurrentPage = null;
        OnStateChanged?.Invoke();
    }

    public void SelectPage(Page? page)
    {
        if (page is null)
        {
            CurrentPage = null;
            OnStateChanged?.Invoke();
            return;
        }
        var canonical = flatIndex.TryGetValue(page.Id, out var i) ? flatPages[i] : page;
        CurrentPage = canonical;
        CurrentPackage = pageToPackage.TryGetValue(canonical.Id, out var pkg) ? pkg : null;
        if (pageToCircle.TryGetValue(canonical.Id, out var crc))
            CurrentCircle = crc;
        OnStateChanged?.Invoke();
    }

    public Page? PreviousPage
    {
        get
        {
            var idx = CurrentIndex;
            return idx > 0 ? flatPages[idx - 1] : null;
        }
    }

    public Page? NextPage
    {
        get
        {
            var idx = CurrentIndex;
            if (flatPages.Count == 0) return null;
            if (idx < 0) return flatPages[0];
            return idx < flatPages.Count - 1 ? flatPages[idx + 1] : null;
        }
    }

    public bool HasPreviousPage => CurrentPage is not null;
    public bool HasNextPage => NextPage is not null;

    public void NavigatePrevious()
    {
        if (CurrentPage is null) return;
        var idx = CurrentIndex;
        if (idx > 0)
            SelectPageInternal(flatPages[idx - 1]);
        else
            NavigateHome();
    }

    public void NavigateNext()
    {
        var idx = CurrentIndex;
        if (flatPages.Count == 0) return;
        if (idx < 0)
        {
            SelectPageInternal(flatPages[0]);
            return;
        }
        if (idx < flatPages.Count - 1)
            SelectPageInternal(flatPages[idx + 1]);
    }

    public void NavigateFirst()
    {
        if (flatPages.Count == 0) return;
        SelectPageInternal(flatPages[0]);
    }

    public void NavigateLast()
    {
        if (flatPages.Count == 0) return;
        SelectPageInternal(flatPages[^1]);
    }

    /// <summary>
    /// "Home" now means: jump to the first available circle (its content page or its first inner page).
    /// If no circles are loaded, clears the current selection.
    /// </summary>
    public void NavigateHome()
    {
        var first = Circles.FirstOrDefault();
        if (first is null)
        {
            CurrentCircle = null;
            CurrentPackage = null;
            CurrentPage = null;
            OnStateChanged?.Invoke();
            return;
        }

        if (!SelectCircleNode(first))
            NavigateToFirstPageOf(first);
    }

    public void NavigateChapterFirst()
    {
        var span = GetChapterSpan(CurrentPage);
        if (span is { } s)
            SelectPageInternal(flatPages[s.start]);
    }

    public void NavigateChapterLast()
    {
        var span = GetChapterSpan(CurrentPage);
        if (span is { } s)
            SelectPageInternal(flatPages[s.end]);
    }

    private (int start, int end)? GetChapterSpan(Page? page)
    {
        if (page is null) return null;
        var crumbs = GetBreadcrumb(page);
        if (crumbs.Count == 0) return null;
        var chapter = crumbs[^1];

        int start = -1, end = -1;
        for (int i = 0; i < flatPages.Count; i++)
        {
            var pageCrumbs = pageBreadcrumb[flatPages[i].Id];
            if (BelongsTo(pageCrumbs, chapter))
            {
                if (start < 0) start = i;
                end = i;
            }
            else if (start >= 0)
            {
                break;
            }
        }
        return start >= 0 ? (start, end) : null;
    }

    private static bool BelongsTo(IReadOnlyList<ContentNode> ancestors, ContentNode chapter)
    {
        for (int i = 0; i < ancestors.Count; i++)
            if (ReferenceEquals(ancestors[i], chapter))
                return true;
        return false;
    }

    public bool SelectCircleNode(Models.Circle circle)
    {
        if (flatIndex.TryGetValue($"crc::{circle.Id}", out var i))
        {
            SelectPageInternal(flatPages[i]);
            return true;
        }
        return false;
    }

    public bool SelectPackageNode(Package package)
    {
        if (flatIndex.TryGetValue($"pkg::{package.Id}", out var i))
        {
            SelectPageInternal(flatPages[i]);
            return true;
        }
        return false;
    }

    public bool SelectFolderNode(Folder folder)
    {
        if (flatIndex.TryGetValue($"fld::{folder.Id}", out var i))
        {
            SelectPageInternal(flatPages[i]);
            return true;
        }
        return false;
    }

    public bool HasContentPage(Models.Circle circle) => flatIndex.ContainsKey($"crc::{circle.Id}");
    public bool HasContentPage(Package package) => flatIndex.ContainsKey($"pkg::{package.Id}");
    public bool HasContentPage(Folder folder) => flatIndex.ContainsKey($"fld::{folder.Id}");

    public bool NavigateToFirstPageOf(Folder folder)
    {
        for (int i = 0; i < flatPages.Count; i++)
        {
            var crumbs = pageBreadcrumb[flatPages[i].Id];
            if (BelongsTo(crumbs, folder))
            {
                SelectPageInternal(flatPages[i]);
                return true;
            }
        }
        return false;
    }

    public bool NavigateToFirstPageOf(Package package)
    {
        for (int i = 0; i < flatPages.Count; i++)
        {
            if (pageToPackage.TryGetValue(flatPages[i].Id, out var pkg) && ReferenceEquals(pkg, package))
            {
                SelectPageInternal(flatPages[i]);
                return true;
            }
        }
        return false;
    }

    public bool NavigateToFirstPageOf(Models.Circle circle)
    {
        for (int i = 0; i < flatPages.Count; i++)
        {
            if (pageToCircle.TryGetValue(flatPages[i].Id, out var crc) && ReferenceEquals(crc, circle))
            {
                SelectPageInternal(flatPages[i]);
                return true;
            }
        }
        return false;
    }

    public Page? Resolve(Page? page)
    {
        if (page is null) return null;
        return flatIndex.TryGetValue(page.Id, out var i) ? flatPages[i] : page;
    }

    private void SelectPageInternal(Page page)
    {
        CurrentPackage = pageToPackage.TryGetValue(page.Id, out var pkg) ? pkg : null;
        CurrentCircle = pageToCircle.TryGetValue(page.Id, out var crc) ? crc : CurrentCircle;
        CurrentPage = page;
        OnStateChanged?.Invoke();
    }

    private int CurrentIndex => CurrentPage is not null && flatIndex.TryGetValue(CurrentPage.Id, out var i) ? i : -1;

    public bool IsCurrent(Page page) =>
        CurrentPage is not null && CurrentPage.Id == page.Id;
}
