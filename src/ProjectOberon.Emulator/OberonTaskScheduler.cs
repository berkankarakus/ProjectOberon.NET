namespace ProjectOberon.Emulator;

public enum OberonTaskState
{
    Off,
    Idle,
    Active
}

public sealed class OberonTask
{
    internal OberonTask(Action handler, int period)
    {
        Handler = handler;
        Period = period;
        Next = this;
    }

    public Action Handler { get; }

    public int Period { get; }

    public int NextTime { get; internal set; }

    public OberonTaskState State { get; internal set; }

    internal OberonTask Next { get; set; }

    internal OberonTaskScheduler? Owner { get; set; }
}

public sealed class OberonTaskScheduler
{
    private readonly OberonTask _idleTask;
    private OberonTask _currentTask;

    public OberonTaskScheduler()
    {
        _idleTask = new OberonTask(static () => { }, 0)
        {
            State = OberonTaskState.Idle
        };
        _currentTask = _idleTask;
    }

    public OberonTask CurrentTask => _currentTask;

    public int TaskCount { get; private set; }

    public int ActivityCount { get; private set; }

    public OberonTask NewTask(Action handler, int period)
    {
        ArgumentNullException.ThrowIfNull(handler);
        if (period < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(period));
        }

        return new OberonTask(handler, period);
    }

    public bool Install(OberonTask task)
    {
        ArgumentNullException.ThrowIfNull(task);
        if (task.State != OberonTaskState.Off || task.Owner is not null)
        {
            return false;
        }

        task.Next = _currentTask.Next;
        _currentTask.Next = task;
        task.State = OberonTaskState.Idle;
        task.NextTime = 0;
        task.Owner = this;
        TaskCount++;
        return true;
    }

    public bool Remove(OberonTask task)
    {
        ArgumentNullException.ThrowIfNull(task);
        if (task.State == OberonTaskState.Off || !ReferenceEquals(task.Owner, this))
        {
            return false;
        }

        var previous = task;
        while (!ReferenceEquals(previous.Next, task))
        {
            previous = previous.Next;
            if (ReferenceEquals(previous, task))
            {
                return false;
            }
        }

        previous.Next = task.Next;
        task.State = OberonTaskState.Off;
        task.Next = task;
        task.Owner = null;
        _currentTask = previous;
        TaskCount--;
        return true;
    }

    public void Collect(int count)
    {
        ActivityCount = count;
    }

    public void Tick(int time)
    {
        if (time < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(time));
        }

        _currentTask = _currentTask.Next;
        if (time < _currentTask.NextTime)
        {
            return;
        }

        _currentTask.NextTime = checked(time + _currentTask.Period);
        _currentTask.State = OberonTaskState.Active;
        try
        {
            _currentTask.Handler();
        }
        finally
        {
            if (_currentTask.State == OberonTaskState.Active)
            {
                _currentTask.State = OberonTaskState.Idle;
            }
        }
    }
}
