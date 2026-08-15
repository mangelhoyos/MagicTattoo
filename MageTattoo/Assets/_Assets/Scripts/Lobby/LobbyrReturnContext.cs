public static class LobbyReturnContext
{
    public static bool HasConnectionError { get; private set; }

    public static void SetConnectionError()
    {
        HasConnectionError = true;
    }

    public static bool ConsumeConnectionError()
    {
        if (!HasConnectionError)
            return false;

        HasConnectionError = false;
        return true;
    }
}