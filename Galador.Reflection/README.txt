.NET Framework and .NET Core target both use Emit and DynamicMethod
.NET Standard profile no emit/DynamicMethod, since Android and iOS don't support emit (only compile but not run), hence using .NET Standard 2.0 profile.
.NET Framework also use System.Configuration and App.Settings[] to turn log traces on or off at launch time

How to Publish
==============
0] Get API key @ https://www.nuget.org/
1] https://docs.microsoft.com/en-us/nuget/create-packages/creating-a-package-dotnet-cli
2] https://docs.microsoft.com/en-us/nuget/nuget-org/publish-a-package