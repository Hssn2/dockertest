namespace dockertest.Entities.Sys;

public sealed class SysMenu : SysEntity
{
    public string Code { get; set; } = "";
    public string Title { get; set; } = "";
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
