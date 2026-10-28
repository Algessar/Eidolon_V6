using System.Runtime.InteropServices;

namespace Eidolon.Editor;

/// <summary>
/// Native "open file" dialog so the user can browse the file system instead of typing a path.
/// </summary>
/// <remarks>
/// Implemented with the Win32 common dialog. On any other platform <see cref="TryPickFile"/>
/// returns null so callers can fall back to something else.
/// </remarks>
internal static unsafe class FileDialog
{
    private const int OfnNoChangeDir = 0x00000008;
    private const int OfnPathMustExist = 0x00000800;
    private const int OfnFileMustExist = 0x00001000;
    private const int OfnExplorer = 0x00080000;
    private const int OfnOverwritePrompt = 0x00000002;

    /// <summary>
    /// Shows a modal open-file dialog. Returns the chosen path, or null when the user cancels.
    /// </summary>
    /// <param name="filter">
    /// Win32 filter string, e.g. <c>"OBJ (*.obj)\0*.obj\0All files (*.*)\0*.*\0\0"</c>.
    /// </param>
    public static string? TryPickFile(string title, string filter, string? initialDirectory = null)
    {
        if (!OperatingSystem.IsWindows())
            return null;

        var buffer = new char[32768];

        fixed (char* filePointer = buffer)
        {
            var dialog = new OpenFileName
            {
                StructSize = Marshal.SizeOf<OpenFileName>(),
                // Owner is whatever is in front, which is the editor when the user clicks the button.
                Owner = GetForegroundWindow(),
                Filter = filter,
                FilterIndex = 1,
                File = (IntPtr)filePointer,
                MaxFile = buffer.Length,
                InitialDirectory = string.IsNullOrEmpty(initialDirectory) ? null : initialDirectory,
                Title = title,
                DefaultExtension = "obj",
                Flags = OfnExplorer | OfnFileMustExist | OfnPathMustExist | OfnNoChangeDir,
            };

            if (!GetOpenFileNameW(ref dialog))
                return null; // cancelled, or the dialog failed to open
        }

        var length = Array.IndexOf(buffer, '\0');
        if (length < 0)
            length = buffer.Length;

        return length == 0 ? null : new string(buffer, 0, length);
    }

    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    private static extern bool GetOpenFileNameW(ref OpenFileName openFileName);

    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    private static extern bool GetSaveFileNameW(ref OpenFileName openFileName);

    /// <summary>Shows the native "save as" dialog. Returns the chosen path, or null when cancelled.</summary>
    public static string? TrySaveFile(string title, string filter, string defaultExtension = "json",
        string? initialDirectory = null)
    {
        if (!OperatingSystem.IsWindows())
            return null;

        var buffer = new char[32768];

        fixed (char* filePointer = buffer)
        {
            var dialog = new OpenFileName
            {
                StructSize = Marshal.SizeOf<OpenFileName>(),
                Owner = GetForegroundWindow(),
                Filter = filter,
                FilterIndex = 1,
                File = (IntPtr)filePointer,
                MaxFile = buffer.Length,
                InitialDirectory = string.IsNullOrEmpty(initialDirectory) ? null : initialDirectory,
                Title = title,
                DefaultExtension = defaultExtension,
                Flags = OfnExplorer | OfnPathMustExist | OfnOverwritePrompt,
            };

            if (!GetSaveFileNameW(ref dialog))
                return null;
        }

        var length = Array.IndexOf(buffer, (char)0);

        if (length < 0)
            length = buffer.Length;

        return length == 0 ? null : new string(buffer, 0, length);
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    /// <summary>Mirrors OPENFILENAMEW; field order and sizes must not change.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct OpenFileName
    {
        public int StructSize;
        public IntPtr Owner;
        public IntPtr Instance;

        [MarshalAs(UnmanagedType.LPWStr)] public string? Filter;
        [MarshalAs(UnmanagedType.LPWStr)] public string? CustomFilter;
        public int MaxCustomFilter;
        public int FilterIndex;

        public IntPtr File;
        public int MaxFile;

        public IntPtr FileTitle;
        public int MaxFileTitle;

        [MarshalAs(UnmanagedType.LPWStr)] public string? InitialDirectory;
        [MarshalAs(UnmanagedType.LPWStr)] public string? Title;

        public int Flags;
        public short FileOffset;
        public short FileExtension;

        [MarshalAs(UnmanagedType.LPWStr)] public string? DefaultExtension;

        public IntPtr CustomData;
        public IntPtr Hook;
        [MarshalAs(UnmanagedType.LPWStr)] public string? TemplateName;

        public IntPtr Reserved;
        public int ReservedValue;
        public int FlagsEx;
    }
}
