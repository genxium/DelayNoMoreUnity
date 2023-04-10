The async/await pattern employed by C# is very different from Golang's goroutine, yet we still hold the same assumption: different sessions of different players might use different OS threads! 

Another thing to keep in mind is that ["await" DOESN'T put its task into another thread](https://learn.microsoft.com/en-us/dotnet/csharp/asynchronous-programming/task-asynchronous-programming-model#BKMK_Threads). An "async task" would only involve another thread (different from current thread) in one of the following scenarios.
- [a] Specified by [Task.Run(...)](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task.run?view=netstandard-2.1);
- [b] Declared as "async Task doHeavyWork(...){}", then called as "doHeavyWork(...).Start()";
- [c] Declared as "async void doHeavyWork(...){}", then called as "doHeavyWork(...)";
- [d] Configured as [".ConfigureAwait(false)"](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task.configureawait?view=net-7.0), which actually DOESN'T put the awaited task into another thread, but just [puts the "CONTINUATION AFTER AWAIT" into another thread](https://devblogs.microsoft.com/dotnet/configureawait-faq/).

Therefore if we really want to mimic the behaviour with "go Xxx()", the closest way would be [b] or [c].
