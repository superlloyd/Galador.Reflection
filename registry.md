
## Introduction

In my application I needed to be able to dynamically load type and their dependency at runtime. 
My few interaction with various IoC framework / library were not the most successful. Except with MEF, which seemed great at the time!
Except every time I wanted to use it again I completely forget how to set it up and had to Google for a few hours.
Hence this library which is (hopefully) as easy to use as MEF but with almost no setup required.

Enter `Galador.Reflection.Registry`

## Basic Usage


## Functionality
If one ignore the multiple polymorphic version the registry basically implement the following methods:

    class Registry
    {
        // register services
        void RegisterAssemblies(type with export attribute in assemblies)
        void Register(types);
        bool IsRegistered(type);

        // resolve (i.e. create only once in a call) any object
        T Resolve<T>();
        IEnumerable<T> ResolveAll<T>();

        // create: make a new one every time
        T Create<T>();
        bool CanCreate<T>();

        // bonus method: resolve property of existing instance
        void ResolveProperties(instance);
    }

