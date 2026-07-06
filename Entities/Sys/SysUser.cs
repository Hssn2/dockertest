namespace dockertest.Entities.Sys;

public sealed class SysUser : SysEntity
{
    public string Username { get; set; } = "";
    public string Email { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public bool IsActive { get; set; } = true;
}
