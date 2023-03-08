# Importing tweaks for Unity3D
The MsBuild configuration is guided by [Microsoft documentation - Customize Your Build](https://learn.microsoft.com/en-us/visualstudio/msbuild/customize-your-build?view=vs-2022). 

To avoid Unity3D from generating redundant `*.meta` files, a folder `UnityPackageOutput` is reserved to hold its `package.json` for importing.
