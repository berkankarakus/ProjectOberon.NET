using ProjectOberon.Emulator;

internal static class OberonTaskSchedulerTests
{
    public static void Run()
    {
        CreatesTasksOffAndInstallsOnce();
        RunsTasksByPeriod();
        RemovesTasksAndUpdatesCurrentTask();
        CollectsActivityCount();
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

    private static void CollectsActivityCount()
    {
        var scheduler = new OberonTaskScheduler();
        scheduler.Collect(17);

        TestAssertions.Equal(17, scheduler.ActivityCount);
    }
}
