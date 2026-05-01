using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CircleOfTruthAndLove.Models;

namespace CircleOfTruthAndLove.Services;

public class ContentManager
{
    private readonly HttpClient http;
    private readonly List<Package> packages = [];
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public ContentManager(HttpClient http)
    {
        this.http = http;
    }

    public IReadOnlyList<Package> Packages => packages;

    // Flat ordered list of all navigable pages across all packages (depth-first),
    // built once when packages load. Hierarchy is kept untouched; only navigation order is cached.
    private readonly List<Page> flatPages = [];
    private readonly Dictionary<string, int> flatIndex = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Package> pageToPackage = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IReadOnlyList<ContentNode>> pageBreadcrumb = new(StringComparer.Ordinal);

    public IReadOnlyList<Page> FlatPages => flatPages;

    /// <summary>
    /// Returns the breadcrumb ancestor chain (Package, Folder...) for a given page, excluding the page itself.
    /// </summary>
    public IReadOnlyList<ContentNode> GetBreadcrumb(Page page) =>
        pageBreadcrumb.TryGetValue(page.Id, out var b) ? b : Array.Empty<ContentNode>();

    public Package? CurrentPackage { get; private set; }
    public Page? CurrentPage { get; private set; }

    private const string PackagesBasePath = "content/packages";

    public event Action? OnStateChanged;

    public async Task LoadPackageIndexAsync()
    {
        var packageIds = await http.GetFromJsonAsync<List<string>>($"{PackagesBasePath}/packages.json", JsonOptions) ?? [];

        packages.Clear();
        foreach (var id in packageIds)
        {
            var package = await LoadPackageAsync(id);
            if (package is not null)
                packages.Add(package);
        }
        RebuildFlatIndex();
        OnStateChanged?.Invoke();
    }

    private void RebuildFlatIndex()
    {
        flatPages.Clear();
        flatIndex.Clear();
        pageToPackage.Clear();
        pageBreadcrumb.Clear();
        foreach (var pkg in packages)
        {
            var stack = new List<ContentNode> { pkg };
            VisitPackage(pkg, stack);
        }
    }

    private void VisitPackage(Package pkg, List<ContentNode> ancestors)
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
            // Package content page IS the package node — its breadcrumb excludes itself.
            var snapshot = ancestors.Take(ancestors.Count - 1).ToList();
            AddFlatPage(p, pkg, snapshot);
        }
        foreach (var item in pkg.Items)
            VisitItem(item, pkg, ancestors);
    }

    private void VisitItem(ContentItem item, Package pkg, List<ContentNode> ancestors)
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
                    // Folder content page's breadcrumb excludes the folder itself
                    var snapshot = ancestors.Take(ancestors.Count - 1).ToList();
                    AddFlatPage(p, pkg, snapshot);
                }
                foreach (var child in folder.Items)
                    VisitItem(child, pkg, ancestors);
                ancestors.RemoveAt(ancestors.Count - 1);
                break;
            case Page page:
                AddFlatPage(page, pkg, ancestors);
                break;
        }
    }

    private void AddFlatPage(Page page, Package pkg, List<ContentNode> ancestors)
    {
        flatIndex[page.Id] = flatPages.Count;
        flatPages.Add(page);
        pageToPackage[page.Id] = pkg;
        pageBreadcrumb[page.Id] = ancestors.ToArray();
    }

    private async Task<Package?> LoadPackageAsync(string id)
    {
        var package = await http.GetFromJsonAsync<Package>($"{PackagesBasePath}/{id}/package.json", JsonOptions);
        if (package is not null)
            ResolveContentPaths(package);
        return package;
    }

    private static void ResolveContentPaths(Package package)
    {
        var packageBase = $"{PackagesBasePath}/{package.EffectiveFolderName}";
        // Package's own page lives at the package root. Its filename defaults to package Id.
        ResolvePageContent(package.Content, packageBase, package.Id, defaultExtension: ".svg");
        foreach (var item in package.Items)
            ResolveItem(item, packageBase);
    }

    private static void ResolveItem(ContentItem item, string parentBase)
    {
        switch (item)
        {
            case Folder folder:
                var folderBase = $"{parentBase}/{folder.EffectiveFolderName}";
                ResolvePageContent(folder.Content, folderBase, folder.Id, defaultExtension: ".svg");
                foreach (var child in folder.Items)
                    ResolveItem(child, folderBase);
                break;
            case Page page:
                // Page files sit directly in the parent folder; default filename = "<Id>.svg".
                ResolvePageContent(page.Content, parentBase, page.Id, defaultExtension: ".svg");
                break;
        }
    }

    private static void ResolvePageContent(PageContent? content, string baseFolder, string ownerId, string defaultExtension)
    {
        if (content is null) return;
        var fileName = string.IsNullOrWhiteSpace(content.FileName)
            ? ownerId + defaultExtension
            : content.FileName!;
        content.ResolvedPath = $"{baseFolder}/{fileName}";
    }

    public void SelectPackage(Package? package)
    {
        CurrentPackage = package;
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
        // Always use canonical cached instance so prev/next index lookups are O(1).
        var canonical = flatIndex.TryGetValue(page.Id, out var i) ? flatPages[i] : page;
        CurrentPage = canonical;
        if (pageToPackage.TryGetValue(canonical.Id, out var pkg))
            CurrentPackage = pkg;
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
        if (CurrentPage is null)
        {
            return;
        }
        var idx = CurrentIndex;
        if (idx > 0)
            SelectPageInternal(flatPages[idx - 1]);
        else
            SelectPackage(null); // back to home
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

    public void NavigateHome() => SelectPackage(null);

    /// <summary>
    /// Navigate to the first page of the current page's chapter (immediate parent: Folder, or Package if no folder).
    /// </summary>
    public void NavigateChapterFirst()
    {
        var span = GetChapterSpan(CurrentPage);
        if (span is { } s)
            SelectPageInternal(flatPages[s.start]);
    }

    /// <summary>
    /// Navigate to the last page of the current page's chapter (immediate parent: Folder, or Package if no folder).
    /// </summary>
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
        var chapter = crumbs[^1]; // immediate parent

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
                break; // contiguous span ended
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

    /// <summary>
    /// Selects the synthetic content page of a package (if it has Content). Returns false if no content.
    /// </summary>
    public bool SelectPackageNode(Package package)
    {
        if (flatIndex.TryGetValue($"pkg::{package.Id}", out var i))
        {
            SelectPageInternal(flatPages[i]);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Selects the synthetic content page of a folder (if it has Content). Returns false if no content.
    /// </summary>
    public bool SelectFolderNode(Folder folder)
    {
        if (flatIndex.TryGetValue($"fld::{folder.Id}", out var i))
        {
            SelectPageInternal(flatPages[i]);
            return true;
        }
        return false;
    }

    public bool HasContentPage(Package package) => flatIndex.ContainsKey($"pkg::{package.Id}");
    public bool HasContentPage(Folder folder) => flatIndex.ContainsKey($"fld::{folder.Id}");

    /// <summary>
    /// Navigates to the first page belonging to the given folder/chapter (used when the folder has no own content page).
    /// </summary>
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

    /// <summary>
    /// Navigates to the first page belonging to the given package (used when the package has no own content page).
    /// </summary>
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

    /// <summary>
    /// Resolves the canonical cached page instance for a given page identity (handles
    /// synthetic package/folder content pages where callers may construct a transient instance).
    /// </summary>
    public Page? Resolve(Page? page)
    {
        if (page is null) return null;
        return flatIndex.TryGetValue(page.Id, out var i) ? flatPages[i] : page;
    }

    private void SelectPageInternal(Page page)
    {
        var pkg = pageToPackage.TryGetValue(page.Id, out var p) ? p : CurrentPackage;
        if (pkg != CurrentPackage)
        {
            CurrentPackage = pkg;
        }
        CurrentPage = page;
        OnStateChanged?.Invoke();
    }

    private int CurrentIndex => CurrentPage is not null && flatIndex.TryGetValue(CurrentPage.Id, out var i) ? i : -1;

    public bool IsCurrent(Page page) =>
        CurrentPage is not null && CurrentPage.Id == page.Id;

    public List<Page> GetAllPages() => flatPages.ToList();
}
