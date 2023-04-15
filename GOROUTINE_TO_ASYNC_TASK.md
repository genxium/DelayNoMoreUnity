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
    ... // still running on the same thread as "main"

    await firstHeavyBlockingIOReceiveAsync(); // yields CPU immediately and thus allows "doLightWork" to run immediately on the same thread as "main"; however when "firstHeavyBlockingIOReceiveAsync" actually runs on CPU again (it doesn't have to, e.g. when all the blocking I/O operations are done via DirectMemoryAccess), it also runs on the same thread as "main", i.e. no immediate thread switching even within "firstHeavyBlockingIOReceiveAsync"!  

    ... // might run on a different thread from "main", depending on the details of "firstHeavyBlockingIOReceiveAsync"
}

void doLightWork(...) {
    ... // still running on the same thread as "main"
}

void main() {
    doHeavyWork(...);
    doLightWork(...);
}
```

```csharp
// [d]
async Task doHeavyWork(...) {
    /* all the same as [c] */
}

void doLightWork(...) {
    /* all the same as [c] */
}

void main() {
    _ = doHeavyWork(...);
    doLightWork(...);
}
```

According to [C# threadpool documentation](https://learn.microsoft.com/en-us/dotnet/api/system.threading.threadpool?view=net-7.0#remarks), those 2 scenarios should put the "async task" to run on a threadpool thread -- however in practice, **experiments in `frontend/OnlineMapController.[wsSessionTaskAsync | wsSessionActionAsync]` show that in either case, the "async task" ALWAYS continues using the current thread till AFTER THE FIRST AWAIT**.

Therefore if the current thread is responsible for graphics, e.g. MainThread in Unity3D, either [c] or [d] would put the heavy work on it and potentially make graphics laggy -- in practice not a Goroutine equivalent. 

# What switches to another thread immediately
If the current thread is NOT responsible for graphics, using async/await could improve throughput a lot, some useful patterns are listed below assuming that "void main" is running in a graphics thread. 

```csharp
// [c.1]
async void doHeavyWork(...) {
    /* all the same as [c] */
}

void main() {
    Task.Run(() => {
        doHeavyWork(...);
    });
}
```

```csharp
// [c.2]
async void doHeavyWork(...) {
    /* all the same as [c] */
}

void main() {
    new Thread(() => {
        doHeavyWork(...);
    }).Start();
}
```

```csharp
// [d.1]
async Task doHeavyWork(...) {
    /* all the same as [c] */
}

void main() {
    Task.Run(async () => {
        await doHeavyWork(...);
    });
}
```

```csharp
// [d.2]
async Task doHeavyWork(...) {
    /* all the same as [c] */
}

void main() {
    Task.Run(() => {
        _ = doHeavyWork(...);
    });
}
```

```csharp
// [d.3]
async Task doHeavyWork(...) {
    /* all the same as [c] */
}

void main() {
    new Thread(() => {
        _ = doHeavyWork(...);
    }).Start();
}
```

In all cases above, there's `lambda creation overhead` which is inevitable -- and we have thread switching overheads anyway, see [this note](https://app.yinxiang.com/fx/6f48c146-7db8-4a64-bdf0-3c874cd9290d) for more information.

# Invalid syntax notes
Moreover, the following scenario of using "async task" is invalid, i.e. throws `InvalidOperationException: Start may not be called on a promise-style task.` in runtime.
```csharp
// [e]
async Task doHeavyWork(...) {
    /* all the same as [c] */
}

void doLightWork(...) {
    /* all the same as [c] */
}

void main() {
    doHeavyWork(...).Start(); // throws `InvalidOperationException` in runtime
	doLightWork(...);
}
```
