
# Just mimicing Goroutine
The async/await pattern employed by C# is very different from Golang's goroutine, yet we still hold the same assumption: different sessions of different players might use different OS threads! 

Another thing to keep in mind is that ["await" DOESN'T put its task into another thread](https://learn.microsoft.com/en-us/dotnet/csharp/asynchronous-programming/task-asynchronous-programming-model#BKMK_Threads). An "async task" would only involve another thread (different from current thread) in one of the following scenarios.
- [a] Specified by [Task.Run(...)](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task.run?view=netstandard-2.1) -- that said, the `C# Action` can be deemed an equivalent to `Java Runnable`, while `C# Task` as an equivalent to `Java Future` -- [in C#, a `Task` is the acronym to "async task"](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task?view=netstandard-2.1);
- [b] Configured as [".ConfigureAwait(false)"](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task.configureawait?view=net-7.0), which actually DOESN'T put the awaited task into another thread, but just [puts the "CONTINUATION AFTER AWAIT" into another thread](https://devblogs.microsoft.com/dotnet/configureawait-faq/).

How "go Xxx()" actually works to interact with OS threads is vague in [its official documentation](https://go.dev/doc/) ([ref1](https://go.dev/doc/effective_go#concurrency), [ref2](https://go.dev/ref/mem)). After some reading and experiments, **my best guess is that Goroutine is "guaranteed async, and might switch to another thread or even create a new thread if necessary"**.

Therefore if we really want to mimic the behaviour with "go Xxx()", the closest way would be [a].

# What doesn't switch to another thread immediately
The following 2 scenarios of using "async task" are equivalent in terms of thread switching.
```csharp
// [c]
async void doHeavyWork(...) {
	...
}

void main() {
	doHeavyWork(...);
}
```

```csharp
// [d]
async Task doHeavyWork(...) {
	...
}

void main() {
	_ = doHeavyWork(...);
}
```

According to [C# threadpool documentation](https://learn.microsoft.com/en-us/dotnet/api/system.threading.threadpool?view=net-7.0#remarks), those 2 scenarios should put the "async task" to run on a threadpool thread -- however in practice, **experiments in `frontend/OnlineMapController.[wsSessionTaskAsync | wsSessionActionAsync]` show that in either case, the "async task" ALWAYS continues using the current thread till AFTER THE FIRST AWAIT**.

Therefore if the current thread is responsible for graphics, e.g. MainThread in Unity3D, either [c] or [d] would put the heavy work on it and potentially make graphics laggy -- in practice not a Goroutine equivalent. 

# Invalid syntax notes
Moreover, the following scenario of using "async task" is invalid, i.e. throws `InvalidOperationException: Start may not be called on a promise-style task.` in runtime.
```csharp
// [e]
async Task doHeavyWork(...) {
	...
}

void main() {
	doHeavyWork(...).Start(); // throws `InvalidOperationException` in runtime
}
```
