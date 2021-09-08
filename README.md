# vhdxParser
A parser for the vhdx meta data
This is a C# class to get and parse vhdx meta data. The vhdx file specifications can be seen here:
https://docs.microsoft.com/en-us/openspecs/windows_protocols/ms-vhdx/83e061f8-f6e2-4de1-91bd-5d518a43d477

The following metadata blocks can be read and/or parsed:
- Header, Log, MetadataTable, BATTable, RegionTable

The following values can be retrieved:
- VirtualDiskID, BlockSize, LogicalSectorSize, VirtualDiskSize

To start using the code you just need to pass a readable System.IO.FileStream for a vhdx file to the class constructor.
Then you can use the following methods:
- getRawBatTable
- getRawHeader
- getRawLog
- getBlockSize
- getVirtualDiskID
- getLogicalSectorSize
- getVirtualDiskSize
- getRawMetadataTable
- parseMetadataTable
- parseBATTable
- parseRegionTable
