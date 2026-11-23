namespace EnterpriseOMS.Infrastructure.Persistence.Seed;

/// <summary>
/// Switches for optional sample data, bound from the "SeedDemoData" config section. Demo data is only
/// ever seeded in Development (the database initialiser does not run elsewhere). Set a flag to false
/// to start with an empty catalog or an empty warehouse list.
/// </summary>
public class SeedDemoDataSettings
{
    public const string SectionName = "SeedDemoData";

    public bool Products { get; set; } = true;
    public bool Warehouses { get; set; } = true;
    public bool Inventory { get; set; } = true;
}