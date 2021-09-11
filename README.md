# vhdxParser
A parser for the vhdx meta data.
This is a C# class to get and parse vhdx meta data. The vhdx file specifications can be seen here:
https://docs.microsoft.com/en-us/openspecs/windows_protocols/ms-vhdx/83e061f8-f6e2-4de1-91bd-5d518a43d477

The following metadata blocks can be read and/or parsed:
- Header, Log, MetadataTable, BATTable, RegionTable

The following values can be retrieved:
- VirtualDiskID, BlockSize, LogicalSectorSize, VirtualDiskSize

To start using the code you just need to pass a string with the vhdx path to the class constructor.

Then you call:
<code>vhdxParser.parse();</code>
to retrieve an object with the parsed data

The following metadata entries are getting parsed:
- BatTable
- Header
- Log (no parsing atm, just raw bytes) 
- BlockSize
- VirtualDiskID
- LogicalSectorSize
- VirtualDiskSize
- MetadataTable
- RegionTable
