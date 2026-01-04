namespace SubashaVentures.Domain.Enums;

/// <summary>
/// Available icon sizes for SBV icons
/// </summary>
public enum IconSize
{
    Size_32 = 32,
    Size_64 = 64,
    Size_72 = 72,
    Size_96 = 96,
    Size_128 = 128,
    Size_144 = 144,
    Size_152 = 152,
    Size_192 = 192,
    Size_256 = 192,
    Size_384 = 384,
    Size_512 = 512,
    Size_1024 = 1024
}

/// <summary>
/// Available icon types in the project
/// </summary>
public enum IconType
{
    /// <summary>
    /// Main SubashaVentures brand icon
    /// </summary>
    SBV_ICON
}

/// <summary>
/// Available SVG types (to be populated as SVGs are added)
/// </summary>
public enum SvgType
{
    /// <summary>
    /// Placeholder - no SVGs exist yet
    /// </summary>
    None = 0
    // Add SVG types here as they're created:
    // Logo,
    // Menu,
    // Close,
    // etc.
}
