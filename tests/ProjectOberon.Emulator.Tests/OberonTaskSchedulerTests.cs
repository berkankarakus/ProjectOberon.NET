using ProjectOberon.Emulator;

internal static class OberonTaskSchedulerTests
{
    public static void Run()
    {
        CreatesTasksOffAndInstallsOnce();
        RunsTasksByPeriod();
        RemovesTasksAndUpdatesCurrentTask();
        RejectsRemovingTasksOwnedByAnotherScheduler();
        CollectsActivityCount();
        ValidatesTasksAndRestoresStateAfterFailure();
        RestoresExecutingTaskStateWhenHandlerRemovesAnotherTask();
    }

    private static void CreatesTasksOffAndInstallsOnce()
    {
        var scheduler = new OberonTaskScheduler();
        var task = scheduler.NewTask(static () => { }, 10);

        TestAssertions.Equal(OberonTaskState.Off, task.State);
        TestAssertions.True(scheduler.Install(task));
        TestAssertions.False(scheduler.Install(task));
        TestAssertions.Equal(OberonTaskState.Idle, task.State);
        TestAssertions.Equal(1, scheduler.TaskCount);
    }

    private static void RunsTasksByPeriod()
    {
        var calls = 0;
        var scheduler = new OberonTaskScheduler();
        var task = scheduler.NewTask(() => calls++, 10);
        scheduler.Install(task);

        scheduler.Tick(0);
        scheduler.Tick(5);
        scheduler.Tick(10);
        scheduler.Tick(19);
        scheduler.Tick(20);

        TestAssertions.Equal(3, calls);
        TestAssertions.Equal(OberonTaskState.Idle, task.State);
        TestAssertions.Equal(30, task.NextTime);
    }

    private static void RemovesTasksAndUpdatesCurrentTask()
    {
        var scheduler = new OberonTaskScheduler();
        var first = scheduler.NewTask(static () => { }, 1);
        var second = scheduler.NewTask(static () => { }, 1);
        scheduler.Install(first);
        scheduler.Install(second);

        TestAssertions.True(scheduler.Remove(first));
        TestAssertions.Equal(OberonTaskState.Off, first.State);
        TestAssertions.False(scheduler.Remove(first));
        TestAssertions.Equal(1, scheduler.TaskCount);
        TestAssertions.Equal(second, scheduler.CurrentTask);
    }

    private static void RejectsRemovingTasksOwnedByAnotherScheduler()
    {
        var owner = new OberonTaskScheduler();
        var other = new OberonTaskScheduler();
        var task = owner.NewTask(static () => { }, 1);
        var currentTask = other.CurrentTask;

        owner.Install(task);

        TestAssertions.False(other.Remove(task));
        TestAssertions.Equal(OberonTaskState.Idle, task.State);
        TestAssertions.Equal(1, owner.TaskCount);
        TestAssertions.Equal(0, other.TaskCount);
        TestAssertions.Equal(currentTask, other.CurrentTask);
    }

    private static void CollectsActivityCount()
    {
        var scheduler = new OberonTaskScheduler();
        scheduler.Collect(17);

        TestAssertions.Equal(17, scheduler.ActivityCount);
    }

    private static void ValidatesTasksAndRestoresStateAfterFailure()
    {
        var scheduler = new OberonTaskScheduler();
        TestAssertions.Throws<ArgumentNullException>(() => scheduler.NewTask(null!, 1));
        TestAssertions.Throws<ArgumentOutOfRangeException>(() => scheduler.NewTask(static () => { }, -1));
        TestAssertions.Throws<ArgumentOutOfRangeException>(() => scheduler.Tick(-1));

        var task = scheduler.NewTask(static () => throw new InvalidOperationException("task failure"), 1);
        scheduler.Install(task);
        TestAssertions.Throws<InvalidOperationException>(() => scheduler.Tick(0));
        TestAssertions.Equal(OberonTaskState.Idle, task.State);
    }

    private static void RestoresExecutingTaskStateWhenHandlerRemovesAnotherTask()
    {
        var scheduler = new OberonTaskScheduler();
        var target = scheduler.NewTask(static () => { }, 1);
        scheduler.Install(target);

        var executing = scheduler.NewTask(() => scheduler.Remove(target), 1);
        scheduler.Install(scheduler.NewTask(static () => { }, 1));
        scheduler.Install(executing);

        scheduler.Tick(0);

        TestAssertions.Equal(OberonTaskState.Idle, executing.State);
        TestAssertions.Equal(OberonTaskState.Off, target.State);
    }
}
