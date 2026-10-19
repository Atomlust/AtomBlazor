namespace AtomBlazor.Primitives;

// One scope per menu panel. Siblings coordinate here; nested panels receive their own scope.
internal sealed class MenuScope(Func<bool> isOpen, Func<Task> closeRoot)
{
    private MenuSub? activeSub;

    internal bool IsOpen => isOpen();

    internal Task CloseRootAsync() => closeRoot();

    internal void Activate(MenuSub sub)
    {
        if (activeSub != sub)
        {
            activeSub?.Close(false);
            activeSub = sub;
        }
    }

    internal void Release(MenuSub sub)
    {
        if (activeSub == sub)
        {
            activeSub = null;
        }
    }
}
