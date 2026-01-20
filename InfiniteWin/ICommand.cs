namespace InfiniteWin
{
    /// <summary>
    /// Interface for undoable commands
    /// </summary>
    public interface ICommand
    {
        void Execute();
        void Undo();
    }
}
