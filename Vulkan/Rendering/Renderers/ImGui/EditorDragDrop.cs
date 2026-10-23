using System.Text;
using ImGuiNET;

namespace Eidolon.Editor;

/// <summary>
/// Drag-and-drop payload types shared between the editor panels. Asset payloads carry the UTF-8
/// absolute path of the dragged file; Entity payloads carry no data and rely on the editor's own
/// <c>_dragSource</c> field.
/// </summary>
internal static unsafe class EditorDragDrop
{
    public const string EntityPayload = "EIDOLON_ENTITY";
    public const string AssetPayload = "EIDOLON_ASSET";

    /// <summary>
    /// Starts a drag carrying <paramref name="path"/>. Only valid right after the item that is the
    /// drag source was submitted; returns false when ImGui says no drag source is active.
    /// </summary>
    public static bool BeginAssetDrag(string path, string label, string? tooltip = null)
    {
        if (!ImGui.BeginDragDropSource())
            return false;

        var bytes = Encoding.UTF8.GetBytes(path);

        // ImGui copies the payload, so the pinned array only has to live for this call.
        fixed (byte* data = bytes)
        {
            ImGui.SetDragDropPayload(AssetPayload, (nint)data, (uint)bytes.Length);
        }

        ImGui.TextUnformatted(label);

        if (tooltip is not null)
            ImGui.TextDisabled(tooltip);

        ImGui.EndDragDropSource();
        return true;
    }

    /// <summary>True while any asset is being dragged, anywhere in the UI.</summary>
    public static bool IsAssetDragInProgress() => TryGetDraggedAssetPath(out _);

    /// <summary>True while any editor payload (entity or asset) is being dragged.</summary>
    public static bool IsDragInProgress()
    {
        var payload = ImGui.GetDragDropPayload();
        return payload.NativePtr is not null &&
               (payload.IsDataType(AssetPayload) || payload.IsDataType(EntityPayload));
    }

    /// <summary>Path of the asset currently being dragged, if any.</summary>
    public static bool TryGetDraggedAssetPath(out string path)
    {
        var payload = ImGui.GetDragDropPayload();
        return TryReadAssetPayload(payload, out path);
    }

    /// <summary>
    /// Reads an accepted payload. Returns false for null payloads, which is what ImGui hands back
    /// when the accepted payload is of a different type.
    /// </summary>
    public static bool TryReadAssetPayload(ImGuiPayloadPtr payload, out string path)
    {
        path = string.Empty;

        if (payload.NativePtr is null ||
            !payload.IsDataType(AssetPayload) ||
            payload.DataSize == 0 ||
            payload.Data == nint.Zero)
            return false;

        path = Encoding.UTF8.GetString((byte*)payload.Data, (int)payload.DataSize);
        // ImGui payloads and native string APIs may include a trailing NUL. It is a terminator,
        // not part of the filesystem path.
        var terminator = path.IndexOf('\0');
        if (terminator >= 0)
            path = path[..terminator];

        return path.Length > 0;
    }

    /// <summary>
    /// True when the payload under <paramref name="payload"/> has just been dropped on this target.
    /// </summary>
    public static bool IsDelivered(ImGuiPayloadPtr payload) =>
        payload.NativePtr is not null && payload.IsDelivery();
}
