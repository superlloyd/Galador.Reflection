@rem public steps on Dec 2021
@rem get you key at 

dotnet pack  -c Release .\Galador.Reflection\Galador.Reflection.csproj
cd .\Galador.Reflection\bin\Release\
..\..\..\..\nuget push .\Galador.Reflection.1.1.0.nupkg -Source https://api.nuget.org/v3/index.json -ApiKey  '=== INSERT API KEY HERE ==='

