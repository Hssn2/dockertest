namespace dockertest.Data.Entities;

public class SysSetting
{
    public Guid Id { get; set; }

    public string Key { get; set; } = string.Empty;

    public string? Value { get; set; }

    public DateTime UpdatedAt { get; set; }
}
