# EsentSerialize
ESENT Serialization class library is built above Managed ESENT. It allows you to store your objects in the underlying extensible storage engine database. [Wikipedia has nice article](https://en.wikipedia.org/wiki/Extensible_Storage_Engine) outlining what’s so special about the extensible storage engine.

The binaries are [published in NuGet](https://www.nuget.org/packages/EsentSerialize), the compiled documentation is [here on the "Releases" page](https://github.com/Const-me/EsentSerialize/releases).

If the text in the CHM is too small for you, [here's a simple fix](http://superuser.com/a/204783/31483).

## Features
* Serializes and deserializes objects. To mark the object to be your database record type, apply [EseTable] attribute to your record class, and column attributes to the class members.
* Available on Windows desktops and servers (.NET 4.5+), Windows Store 8.1+ (PC only; technically, it works on Windows Phone 8.1, but the API is unofficial and won't pass [WACK](https://developer.microsoft.com/en-us/windows/develop/app-certification-kit) API test), Windows Mobile 10.0+. Suitable for building both desktop apps, mobile apps (on supported platforms), and servers.
* Many different column types, ranging from trivial ones like byte or int32, to enums, multi-values, binary streams, arbitrary objects serialized into binary XML.
* Sophisticated indexing: conditional indices if you need to query from a subset of records, tuple indices to implement full-text search.
* Multiple threads can modify the database at the same time, because record-level locking. At the same time, another thread can read the same records without locks, because multi-versioning.
* The recordsets support filtering and sorting by the ESE indexes, and since version 3.0 they expose LINQ-like API to sort and filter.
* The database return records as IEnumerable<tRow> (where tRow is your record class that has the [EseTable] attribute applied) suitable for lazy evaluation, making it possible to sequentially process a huge dataset that does not even fit in your RAM. 
* The library provides DB schema upgrade mechanism, to upgrade DB schema while retaining user's data.
* On desktops and servers, the library supports hot backups.
* Just like Managed ESENT, the library is architecture-neutral ("Any CPU"), and very small because the DB engine is a part of Windows.
* Several demo projects are provided, including [client-server demo](https://github.com/Const-me/EsentSerialize/tree/master/Demos/ClientServer), and some performance benchmark versus SQLite.
* Good documentation.

## Shortcomings
* Not all functionality was ported from version 2.x. Missing features include table-granular import/export, and raw read/write API.
* Those LINQ-like queries are limited to a single index. If you'll try to query multiple indices, expression compiler will throw an exception "no single index covers all referenced columns". You can only do that manually, using Recordset<tRow>.IntersectIndices API.
* Not all parts of the library are tested equally well.
