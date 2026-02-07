// [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public class EditorAttributes
{
    public string Text { get; }
    public string Color { get; }
    
    public EditorAttributes(string text, string color = "#3498db")
    {
        Text = text;
        Color = color;
    }
}

[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
public class HeaderAttribute : Attribute
{
    public string Text { get; }
    
    public HeaderAttribute(string text)
    {
        Text = text;
    }
}
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = true)]
public class GroupAttribute : Attribute
{
    public string Name { get; }
    public string Color { get; }
    public int Order { get; set; } = 0;
    
    public GroupAttribute(string name, string color = "#3498db")
    {
        Name = name;
        Color = color;
    }
}