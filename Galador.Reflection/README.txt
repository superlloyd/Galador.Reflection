.NET Framework and .NET Core target both use Emit and DynamicMethod
.NET Standard profile no emit/DynamicMethod, since Android and iOS don't support emit (only compile but not run), hence using .NET Standard 2.0 profile.
.NET Framework also use System.Configuration and App.Settings[] to turn log traces on or off at launch time