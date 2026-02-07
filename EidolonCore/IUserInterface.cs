namespace Eidolon.Vulkan;

internal interface IUserInterface : IDisposable
{
    UI_MODE  UIMode { get; }
}

public enum UI_MODE
{
    LIGHT_MODE,
    DARK_MODE,
    CUSTOM_MODE,
} 