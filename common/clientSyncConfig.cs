public static class ClientNetworkConfig
{
    public static int PlayerSyncIntervalMs { get; } = 50;
}

public static class ClientUiConfig
{
    public static double WorldHudRefreshIntervalSeconds { get; } = 0.1d;
}

public static class RemoteEntitySyncConfig
{
    public static float DefaultInterpolationSeconds { get; } = 0.1f;
    public static float MinInterpolationSeconds { get; } = 0.03f;
    public static float MaxInterpolationSeconds { get; } = 0.2f;
    public static float SnapDistance { get; } = 1.5f;
}

public static class RemotePlayerSyncConfig
{
    public static float DefaultInterpolationSeconds { get; } = 0.06f;
    public static float MinInterpolationSeconds { get; } = 0.02f;
    public static float MaxInterpolationSeconds { get; } = 0.12f;
    public static float SnapDistance { get; } = 0.9f;
}
