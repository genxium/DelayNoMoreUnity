According to https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-test, use 

```
dotnet test --logger "console;verbosity=detailed"
```

to view console logs during the test run, or use `Test Explorer window of Visual Studio` as mentioned in https://xunit.net/docs/capturing-output.html.

Refer to https://learn.microsoft.com/en-us/dotnet/core/testing/selective-unit-tests?pivots=mstest for how to run only selected tests, or again use `Test Explorer window of Visual Studio`. A useful example is shown below. 

```
dotnet test --filter "TestHardPushbackCalc" --logger "console;verbosity=detailed"
```
