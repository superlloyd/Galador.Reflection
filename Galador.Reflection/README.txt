.NET Framework and .NET Core target both use Emit and DynamicMethod
.NET Standard profile no emit/DynamicMethod, since Android and iOS don't support emit (only compile but not run), hence using .NET Standard 2.0 profile.
.NET Framework also use System.Configuration and App.Settings[] to turn log traces on or off at launch time

How to Publish
==============
0] Manage API keys @ https://www.nuget.org/account/apikeys
1] https://docs.microsoft.com/en-us/nuget/create-packages/creating-a-package-dotnet-cli
2] https://docs.microsoft.com/en-us/nuget/nuget-org/publish-a-package
3] run those commands
dotnet pack  -c Release .\Galador.Reflection\Galador.Reflection.csproj
cd .\Galador.Reflection\bin\Release\
..\..\..\..\nuget push .\Galador.Reflection.1.1.0.nupkg -Source https://api.nuget.org/v3/index.json -ApiKey  '=== INSERT API KEY HERE ==='

