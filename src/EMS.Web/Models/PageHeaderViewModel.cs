namespace EMS.Web.Models;

public record PageAction(string Text, string Url, string Style = "outline", string? Icon = null, string Method = "get");

/// <summary>Backs the common page header partial (UI guideline section 6).</summary>
public class PageHeaderViewModel
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public PageAction? PrimaryAction { get; set; }
    public List<PageAction> SecondaryActions { get; set; } = new();
    public string? StatusBadge { get; set; }
}
