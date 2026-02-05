using Cysharp.Threading.Tasks;

public interface ICommand
{
    UniTask ExecuteAsync();
    // UniTask UndoAsync(); // Undo functionality can be added later if needed
}
