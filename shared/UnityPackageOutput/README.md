Run the following command on OSX to build.
```
sh> dotnet build shared.csproj
```

The file `shared.dll` is meant to be dynamically built and put into this directory, thus not tracked by Git.  

It's also important that we copied `shared.pdb` into this folder such that in runtime Unity console can show error stack inside `shared.dll`.
