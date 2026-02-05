using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class CommandQueue : MonoBehaviour
{
    private readonly Queue<ICommand> _queue = new Queue<ICommand>();
    private bool _isRunning = false;

    public void Enqueue(ICommand command)
    {
        _queue.Enqueue(command);
        if (!_isRunning)
        {
            ProcessQueue().Forget();
        }
    }

    private async UniTaskVoid ProcessQueue()
    {
        _isRunning = true;
        while (_queue.Count > 0)
        {
            var command = _queue.Dequeue();
            await command.ExecuteAsync();
        }
        _isRunning = false;
    }
    
    public void Clear()
    {
        _queue.Clear();
        _isRunning = false;
    }
}
