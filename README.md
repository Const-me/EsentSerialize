# EsentSerialize
ESENT Serialization class library is built above Managed ESENT. It allows you to store your objects in the underlying extensible storage engine database. [Wikipedia has nice article](https://en.wikipedia.org/wiki/Extensible_Storage_Engine) outlining what’s so special about the extensible storage engine.

The binaries are [published in NuGet](https://www.nuget.org/packages/EsentSerialize).

## Features
* Serializes and deserializes objects. To mark the object to be your database record type, apply [EseTable] attribute to your record class, and column attributes to the class members. 
* Available on Windows desktops and servers (.NET 4.5+), Windows Store 8.1+ (PC only; technically, it works on Windows Phone 8.1, but the API is unofficial and won't pass [WACK](https://developer.microsoft.com/en-us/windows/develop/app-certification-kit) API test), Windows Mobile 10.0+. Suitable for building both desktop apps, mobile apps (on supported platforms), and servers.
* Many different column types, ranging from trivial ones like byte or int32, to enums, multi-values, binary streams, arbitrary objects serialized into binary XML.
* The library is multithreading-aware, concurrent database access by several threads is OK.
* The recordsets support filtering and sorting by the ESE indexes, and since version 3.0 exposes LINQ-like API to sort and filter.
* The database return records as IEnumerable<tRow> (where tRow is your record class that has the [EseTable] attribute applied) suitable for lazy evaluation, making it possible to sequentially process a huge dataset that does not even fit in your RAM. 
* The library provides DB schema upgrade mechanism,to upgrade DB schema while retaining user's data.
* Just like Managed ESENT, the library is architecture-neutral ("Any CPU").
* Good documentation, several demo projects are provided.
