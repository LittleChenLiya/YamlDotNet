# YamlDotNet

| Appveyor | NuGet |
|----------|-------|
|[![Build status](https://ci.appveyor.com/api/projects/status/github/aaubry/yamldotnet?svg=true)](https://ci.appveyor.com/project/aaubry/yamldotnet/branch/master)|  [![NuGet](https://img.shields.io/nuget/v/YamlDotNet.svg)](https://www.nuget.org/packages/YamlDotNet/)


YamlDotNet is a YAML library for [netstandard and other .NET runtimes](#the-yamldotnet-library).

YamlDotNet provides low level parsing and emitting of YAML as well as a high level object model similar to XmlDocument. A serialization library is also included that allows to read and write objects from and to YAML streams.

YamlDotNet's conformance with YAML specifications:

|            YAML Spec                | YDN Parser | YDN Emitter |
|:-----------------------------------:|:----------:|:-----------:|
|  [v1.1](https://yaml.org/spec/1.1/)  |     ✓      |      ✓      |
|  [v1.2](https://yaml.org/spec/1.2/spec.html)  |     ✓      |             |


## What is YAML?

YAML, which stands for "YAML Ain't Markup Language", is described as "a human friendly data serialization standard for all programming languages". Like XML, it allows to represent about any kind of data in a portable, platform-independent format. Unlike XML, it is "human friendly", which means that it is easy for a human to read or produce a valid YAML document.

## The YamlDotNet library

The library has now been successfully used in multiple projects and is considered fairly stable. It is compatible with the following runtimes:

* netstandard 2.0
* netstandard 2.1
* .NET 8.0
* .NET 10.0
* .NET Framework 4.7

## Quick start

Here are some quick samples to get you started which can be viewed in [this fiddle](https://dotnetfiddle.net/CQ7ZKi).

### Serialization from an object to a string

```c#
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
...

 var person = new Person
{
    Name = "Abe Lincoln",
    Age = 25,
    HeightInInches = 6f + 4f / 12f,
    Addresses = new Dictionary<string, Address>{
        { "home", new  Address() {
                Street = "2720  Sundown Lane",
                City = "Kentucketsville",
                State = "Calousiyorkida",
                Zip = "99978",
            }},
        { "work", new  Address() {
                Street = "1600 Pennsylvania Avenue NW",
                City = "Washington",
                State = "District of Columbia",
                Zip = "20500",
            }},
    }
};

var serializer = new SerializerBuilder()
    .WithNamingConvention(CamelCaseNamingConvention.Instance)
    .Build();
var yaml = serializer.Serialize(person);
System.Console.WriteLine(yaml);
// Output: 
// name: Abe Lincoln
// age: 25
// heightInInches: 6.3333334922790527
// addresses:
//   home:
//     street: 2720  Sundown Lane
//     city: Kentucketsville
//     state: Calousiyorkida
//     zip: 99978
//   work:
//     street: 1600 Pennsylvania Avenue NW
//     city: Washington
//     state: District of Columbia
//     zip: 20500
```

### Deserialization from a string to an object

```c#
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
...

var yml = @"
name: George Washington
age: 89
height_in_inches: 5.75
addresses:
  home:
    street: 400 Mockingbird Lane
    city: Louaryland
    state: Hawidaho
    zip: 99970
";

var deserializer = new DeserializerBuilder()
    .WithNamingConvention(UnderscoredNamingConvention.Instance)  // see height_in_inches in sample yml 
    .Build();

//yml contains a string containing your YAML
var p = deserializer.Deserialize<Person>(yml);
var h = p.Addresses["home"];
System.Console.WriteLine($"{p.Name} is {p.Age} years old and lives at {h.Street} in {h.City}, {h.State}.");
// Output:
// George Washington is 89 years old and lives at 400 Mockingbird Lane in Louaryland, Hawidaho.
```

### Limiting the size of the object graph with a reference budget

Some YAML documents are small on disk but expand into a very large object graph once aliases are resolved. To protect against this, the deserializer can enforce an optional reference budget. The budget limits two quantities:

- the number of nodes that are **actually constructed** while building the object graph, and
- the depth of **aliases that are currently being resolved**.

Aliases that share an already constructed object are not counted as additional constructions, and the limits are propagated through every nested deserialization call. When a limit is reached, a `ReferenceBudgetExceededException` is thrown with the YAML position and the category of the budget that was exceeded; no partial result is returned to the caller.

The budget is disabled by default. Enable it with `WithReferenceBudget`, passing the maximum number of nodes and the maximum alias depth (either value can be `null` to leave that category unbounded):

```c#
var deserializer = new DeserializerBuilder()
    .WithReferenceBudget(maximumNodes: 10_000, maximumAliasDepth: 64)
    .Build();

try
{
    var result = deserializer.Deserialize<object>(yml);
}
catch (ReferenceBudgetExceededException ex)
{
    // ex.Category identifies which limit was exceeded (NodeCount or AliasDepth).
    // ex.Start / ex.End hold the YAML position of the offending event.
    Console.WriteLine($"{ex.Category} budget exceeded at {ex.Start}: {ex.Message}");
}
```

Setting a budget is recommended when deserializing untrusted input, since it bounds the amount of memory and the depth of alias resolution that a single document can trigger.

## More information

More information can be found in the [project's wiki](https://github.com/aaubry/YamlDotNet/wiki).

## Installing

Just install the [YamlDotNet NuGet package](http://www.nuget.org/packages/YamlDotNet/):

```
PM> Install-Package YamlDotNet
```

If you do not want to use NuGet, you can [download binaries here](https://ci.appveyor.com/project/aaubry/yamldotnet).

YamlDotNet is also available on the [Unity Asset Store](https://assetstore.unity.com/packages/tools/integration/yamldotnet-for-unity-36292).

## Contributing

Please read [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

## Release notes

Please see the Releases at https://github.com/aaubry/YamlDotNet/releases

