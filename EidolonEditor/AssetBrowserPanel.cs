using System.Numerics;
using EidolonCore.Assets;
using EidolonCore.Rendering;
using ImGuiNET;

namespace Eidolon.Editor;

/// <summary>
/// Unity-style project browser: a folder tree on the left and a grid of selectable asset icons on
/// the right. Icons can be dragged into the Scene panel or onto the Game View to spawn entities.
/// </summary>
internal sealed class AssetBrowserPanel
{
    private const string ObjFilter = "Wavefront OBJ (*.obj)\0*.obj\0All files (*.*)\0*.*\0\0";
    private const float FolderPaneWidth = 190f;
    private const float IconSize = 64f;

    private static readonly Vector4 ErrorColor = new(0.95f, 0.42f, 0.42f, 1f);
    private static readonly Vector4 SuccessColor = new(0.55f, 0.85f, 0.55f, 1f);

    /// <summary>Cube corners in {-1,1}³, already projected to 2D (see <see cref="BuildCubeCorners"/>).</summary>
    private static readonly Vector2[] CubeCorners = BuildCubeCorners();

    private static readonly int[] CubeEdges =
    [
        0, 1, 1, 3, 3, 2, 2, 0, // back face
        4, 5, 5, 7, 7, 6, 6, 4, // front face
        0, 4, 1, 5, 2, 6, 3, 7, // connecting edges
    ];

    private readonly AssetLibrary _assets;
    private readonly Action<MeshAsset> _spawnAsset;
    private readonly Action<Mesh> _spawnMesh;

    private AssetFolder? _currentFolder;
    private string _filter = string.Empty;
    private string? _status;
    private bool _statusIsError;

    public AssetBrowserPanel(AssetLibrary assets, Action<MeshAsset> spawnAsset, Action<Mesh> spawnMesh)
    {
        _assets = assets;
        _spawnAsset = spawnAsset;
        _spawnMesh = spawnMesh;
    }

    /// <summary>Asset picked in the grid; the panel is the source of truth for the selection.</summary>
    public MeshAsset? SelectedAsset { get; private set; }

    public void Draw()
    {
        // First-run placement only; afterwards the dock layout owns the position.
        ImGui.SetNextWindowPos(new Vector2(740f, 60f), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(new Vector2(560f, 380f), ImGuiCond.FirstUseEver);
        ImGui.Begin("Eidolon / Browser");

        DrawToolbar();

        var contentAvail = ImGui.GetContentRegionAvail();

        // Keep the icon grid usable when the dock node is narrow, and leave room for the footer.
        var panesHeight = MathF.Max(60f, contentAvail.Y - ImGui.GetFrameHeightWithSpacing() * 2f);
        var folderWidth = Math.Clamp(contentAvail.X * 0.32f, 96f, FolderPaneWidth);

        if (ImGui.BeginChild("##assetFolders", new Vector2(folderWidth, panesHeight), ImGuiChildFlags.Borders))
            DrawFolderTree();

        ImGui.EndChild();
        ImGui.SameLine();

        if (ImGui.BeginChild("##assetGrid", new Vector2(0f, panesHeight), ImGuiChildFlags.Borders))
            DrawGrid();

        ImGui.EndChild();

        DrawFooter();
        ImGui.End();
    }

    private void DrawToolbar()
    {
        ImGui.SetNextItemWidth(-96f);
        ImGui.InputTextWithHint("##assetFilter", "Search assets...", ref _filter, 256);

        ImGui.SameLine();

        if (ImGui.Button("Rescan"))
        {
            var found = _assets.Refresh();

            // The old asset objects are gone with the old tree, so the selection has to go too.
            SelectedAsset = null;
            _currentFolder = _assets.Root;
            SetStatus($"Rescanned: {found} mesh asset(s).", isError: false);
        }
    }

    private void DrawFolderTree()
    {
        if (_assets.Root is not { } root)
        {
            ImGui.TextDisabled("No asset folder scanned.");
            return;
        }

        if (_currentFolder is null || !ReferenceEquals(ResolveRoot(_currentFolder), root))
            _currentFolder = root;

        DrawFolderNode(root, isRoot: true);
    }

    /// <summary>Walks to the root so a stale folder from a previous scan still resolves.</summary>
    private static AssetFolder? ResolveRoot(AssetFolder folder)
    {
        var current = folder;

        while (current.Parent is { } parent)
            current = parent;

        return current;
    }

    private void DrawFolderNode(AssetFolder folder, bool isRoot = false)
    {
        var hasChildren = folder.Subfolders.Count > 0;

        var flags = ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.SpanAvailWidth;
        if (!hasChildren)
            flags |= ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.NoTreePushOnOpen;
        if (isRoot)
            flags |= ImGuiTreeNodeFlags.DefaultOpen;
        if (ReferenceEquals(_currentFolder, folder))
            flags |= ImGuiTreeNodeFlags.Selected;

        var open = ImGui.TreeNodeEx($"{folder.Name}##{folder.Path}", flags);

        if (ImGui.IsItemClicked())
        {
            _currentFolder = folder;

            if (_filter.Length > 0)
                _filter = string.Empty;
        }

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip($"{folder.Path}\n{folder.TotalFileCount} asset(s)");

        if (open && hasChildren)
        {
            foreach (var subfolder in folder.Subfolders)
                DrawFolderNode(subfolder);

            ImGui.TreePop();
        }
    }

    private void DrawGrid()
    {
        if (_assets.Root is not { } root)
        {
            ImGui.TextDisabled("Point the AssetLibrary at a folder to browse assets.");
            return;
        }

        var search = _filter.Trim();

        if (search.Length > 0)
        {
            var matches = new List<MeshAsset>();

            foreach (var asset in _assets.Catalog)
            {
                if (asset.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    asset.RelativePath.Contains(search, StringComparison.OrdinalIgnoreCase))
                    matches.Add(asset);
            }

            ImGui.TextDisabled($"Search results for \"{search}\"  ({matches.Count})");
            ImGui.Separator();
            DrawTiles(matches, "No assets match the search.");
            return;
        }

        var folder = _currentFolder ?? root;
        DrawBreadcrumb(folder);
        ImGui.Separator();
        DrawTiles(folder.Files, "This folder is empty");
    }

    private void DrawBreadcrumb(AssetFolder folder)
    {
        var chain = new List<AssetFolder>();

        for (AssetFolder? current = folder; current is not null; current = current.Parent)
            chain.Add(current);

        chain.Reverse();

        for (var i = 0; i < chain.Count; i++)
        {
            if (i > 0)
            {
                ImGui.SameLine();
                ImGui.TextDisabled("/");
                ImGui.SameLine();
            }

            var isCurrent = i == chain.Count - 1;
            var label = isCurrent ? chain[i].Name : $"{chain[i].Name}##crumb{i}";

            if (isCurrent)
            {
                ImGui.TextUnformatted(label);
            }
            else if (ImGui.SmallButton(label))
            {
                _currentFolder = chain[i];
            }
        }

        ImGui.SameLine();
        ImGui.TextDisabled($"   {folder.Files.Count} item(s)");
    }

    private void DrawTiles(IReadOnlyList<MeshAsset> assets, string emptyMessage)
    {
        if (assets.Count == 0)
        {
            ImGui.TextDisabled(emptyMessage);
            return;
        }

        var cell = new Vector2(IconSize + 32f, IconSize + ImGui.GetTextLineHeight() + 20f);
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var width = ImGui.GetContentRegionAvail().X;
        var columns = Math.Max(1, (int)((width + spacing) / (cell.X + spacing)));

        for (var i = 0; i < assets.Count; i++)
        {
            if (i % columns != 0)
                ImGui.SameLine();

            DrawTile(assets[i], cell);
        }
    }

    private void DrawTile(MeshAsset asset, Vector2 cell)
    {
        ImGui.PushID(asset.Path);

        var origin = ImGui.GetCursorScreenPos();
        ImGui.InvisibleButton("##tile", cell);

        var hovered = ImGui.IsItemHovered();

        if (ImGui.IsItemClicked())
            SelectedAsset = asset;

        if (hovered)
        {
            var detail = asset.IsLoaded && asset.Mesh is { } loaded
                ? $"{asset.RelativePath}\n{FormatSize(asset.SizeInBytes)}  {loaded.TriangleCount} tris"
                : $"{asset.RelativePath}\n{FormatSize(asset.SizeInBytes)}";

            ImGui.SetTooltip(detail);

            if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                _spawnAsset(asset);
        }

        if (EditorDragDrop.BeginAssetDrag(asset.Path, asset.Name, asset.RelativePath))
            SelectedAsset = asset;

        DrawTileVisual(asset, origin, cell, hovered);
        ImGui.PopID();
    }

    private void DrawTileVisual(MeshAsset asset, Vector2 origin, Vector2 cell, bool hovered)
    {
        var drawList = ImGui.GetWindowDrawList();
        var selected = ReferenceEquals(SelectedAsset, asset);
        var rectMax = origin + cell;

        if (selected)
            drawList.AddRectFilled(origin, rectMax, ImGui.GetColorU32(ImGuiCol.Header, 0.90f), 4f, ImDrawFlags.None);
        else if (hovered)
            drawList.AddRectFilled(origin, rectMax, ImGui.GetColorU32(ImGuiCol.HeaderHovered, 0.55f), 4f, ImDrawFlags.None);

        var iconMin = new Vector2(origin.X + (cell.X - IconSize) * 0.5f, origin.Y + 4f);

        var fill = ImGui.GetColorU32(ImGuiCol.FrameBg, 0.85f);
        var line = ImGui.ColorConvertFloat4ToU32(new Vector4(0.52f, 0.76f, 0.96f, 1f));
        DrawMeshIcon(drawList, iconMin, IconSize, fill, line);

        var label = Ellipsize(asset.Name, cell.X - 6f);
        var labelSize = ImGui.CalcTextSize(label);
        var labelPosition = new Vector2(
            origin.X + (cell.X - labelSize.X) * 0.5f,
            origin.Y + IconSize + 8f);

        drawList.AddText(labelPosition, ImGui.GetColorU32(ImGuiCol.Text), label);
    }

    /// <summary>A small isometric wireframe cube, so mesh assets read as "3D thing" at a glance.</summary>
    private static void DrawMeshIcon(ImDrawListPtr drawList, Vector2 min, float size, uint fill, uint line)
    {
        drawList.AddRectFilled(min, min + new Vector2(size), fill, 4f, ImDrawFlags.None);

        var center = min + new Vector2(size * 0.5f);
        var scale = size * 0.36f;

        for (var i = 0; i + 1 < CubeEdges.Length; i += 2)
        {
            var from = center + CubeCorners[CubeEdges[i]] * scale;
            var to = center + CubeCorners[CubeEdges[i + 1]] * scale;
            drawList.AddLine(from, to, line, 1.5f);
        }
    }

    private static Vector2[] BuildCubeCorners()
    {
        var corners = new Vector2[8];

        for (var i = 0; i < corners.Length; i++)
        {
            var x = (i & 1) != 0 ? 1f : -1f;
            var y = (i & 2) != 0 ? 1f : -1f;
            var z = (i & 4) != 0 ? 1f : -1f;

            // Simple isometric projection, normalised to the -1..1 range.
            corners[i] = new Vector2((x - z) * 0.5f, (x + z) * 0.25f - y * 0.5f);
        }

        // Corner index bit order must match the edge table above: bit0 = x, bit1 = y, bit2 = z.
        return corners;
    }

    private void DrawFooter()
    {
        ImGui.Separator();

        if (ImGui.Button("Open .obj..."))
            ImportFromFileDialog();

        ImGui.SameLine();

        if (ImGui.Button("Add to scene") && SelectedAsset is { } selected)
            _spawnAsset(selected);

        if (SelectedAsset is { } asset)
        {
            ImGui.SameLine();
            ImGui.TextDisabled(asset.IsLoaded && asset.Mesh is { } mesh
                ? $"{asset.Name}  {FormatSize(asset.SizeInBytes)}  {mesh.TriangleCount} tris"
                : $"{asset.Name}  {FormatSize(asset.SizeInBytes)}");
        }

        if (_status is not null)
            ImGui.TextColored(_statusIsError ? ErrorColor : SuccessColor, _status);
    }

    private void ImportFromFileDialog()
    {
        var path = FileDialog.TryPickFile("Import model", ObjFilter, _assets.RootDirectory);

        if (path is null)
            return;

        try
        {
            var mesh = _assets.LoadObj(path);
            _spawnMesh(mesh);
            SetStatus($"Imported '{mesh.Name}' ({mesh.Vertices.Length} verts, {mesh.TriangleCount} tris).",
                isError: false);
        }
        catch (Exception ex)
        {
            SetStatus($"Import failed: {ex.Message}", isError: true);
        }
    }

    private void SetStatus(string message, bool isError)
    {
        _status = message;
        _statusIsError = isError;
    }

    /// <summary>
    /// Trims a label so long file names stay inside their tile. Uses ASCII dots because the
    /// default ImGui font only ships Latin-1 glyphs.
    /// </summary>
    private static string Ellipsize(string text, float maxWidth)
    {
        const string suffix = "...";

        if (ImGui.CalcTextSize(text).X <= maxWidth)
            return text;

        var trimmed = text;

        while (trimmed.Length > 1)
        {
            trimmed = trimmed[..^1];
            var candidate = trimmed + suffix;

            if (ImGui.CalcTextSize(candidate).X <= maxWidth)
                return candidate;
        }

        return text.Length > 0 ? text[..1] : text;
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024f:F0} KB",
        _ => $"{bytes / (1024f * 1024f):F1} MB",
    };
}
