using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.ComponentModel;

namespace vhdxParser
{
    public class vhdxParser : IDisposable
    {
        private FileStream sourceStream;

        public vhdxParser(string file)
        {
            this.sourceStream = new FileStream(file, FileMode.Open, FileAccess.Read);

        }

        //closes the sourceStream
        public void close()
        {
            this.sourceStream.Close();
        }

        //parses the whole vhdx meta data
        public VHDXData parse()
        {
            VHDXData dataStruct = new VHDXData();
            dataStruct.fileHeader = new FileHeader();

            //read raw file header section
            dataStruct.fileHeader.rawFileHeader = getRawHeader();

            //parse file identifier

            //signature
            dataStruct.fileHeader.fileIdentifier.signature = System.Text.Encoding.ASCII.GetString(dataStruct.fileHeader.rawFileHeader, 0, 8);

            //creator
            dataStruct.fileHeader.fileIdentifier.creator = System.Text.Encoding.Unicode.GetString(dataStruct.fileHeader.rawFileHeader, 8, 512);

            //parse header 1
            dataStruct.fileHeader.header1 = parseHeader(dataStruct.fileHeader.rawFileHeader, 1);

            //parse header 2
            dataStruct.fileHeader.header2 = parseHeader(dataStruct.fileHeader.rawFileHeader, 2);

            //parse reagionTable 1
            dataStruct.fileHeader.regionTable1 = parseRegionTable(1);

            //parse reagionTable 2
            dataStruct.fileHeader.regionTable2 = parseRegionTable(2);

            //parse metadataTable
            dataStruct.metadataTable = parseMetadataTable(dataStruct.fileHeader.regionTable1);


            //parse various values
            byte[] virtualDiskID = getVirtualDiskID(dataStruct.metadataTable);
            dataStruct.virtualDiskID = virtualDiskID;
            ulong virtualDiskSize = getVirtualDiskSize(dataStruct.metadataTable);
            dataStruct.virtualDiskSize = virtualDiskSize;
            uint logicalSectorSize = getLogicalSectorSize(dataStruct.metadataTable);
            dataStruct.logicalSectorSize = logicalSectorSize;
            uint blockSize = getBlockSize(dataStruct.metadataTable);
            dataStruct.blockSize = blockSize;

            //calculate chunkSize
            UInt32 chunkSize = (UInt32)((Math.Pow(2, 32) * logicalSectorSize) / blockSize);
            dataStruct.chunkRatio = chunkSize;

            //parse batTable
            dataStruct.batTable = parseBATTable(dataStruct.fileHeader.regionTable1, chunkSize, 0, false);

            //get raw log
            dataStruct.rawLog = getRawLog();


            return dataStruct;
        }


        //parses header by the given numer (1 or 2)
        private Header parseHeader(byte[] fileHeader, byte headerNumber)
        {
            Header header = new Header();
            Int32 currentOffset = 64 * headerNumber * 1024; //64KB * 1024

            //read signature
            string signature = System.Text.Encoding.ASCII.GetString(fileHeader, currentOffset, 4);
            header.signature = signature;
            currentOffset += 4;

            //read checksum
            byte[] checksum = new byte[4];
            Array.Copy(fileHeader, currentOffset, checksum, 0, 4);
            header.checksum = checksum;
            currentOffset += 4;

            //read sequenceNumber
            UInt64 sequenceNumber = BitConverter.ToUInt64(fileHeader, currentOffset);
            header.sequenceNumber = sequenceNumber;
            currentOffset += 8;

            //read FileWriteGuid
            byte[] fileWriteGuid = new byte[16];
            Array.Copy(fileHeader, currentOffset, fileWriteGuid, 0, 16);
            header.fileWriteGuid = fileWriteGuid;
            currentOffset += 16;

            //read DataWriteGuid
            byte[] dataWriteGuid = new byte[16];
            Array.Copy(fileHeader, currentOffset, dataWriteGuid, 0, 16);
            header.dataWriteGuid = dataWriteGuid;
            currentOffset += 16;

            //read LogGuid
            byte[] logGuid = new byte[16];
            Array.Copy(fileHeader, currentOffset, logGuid, 0, 16);
            header.logGuid = logGuid;
            currentOffset += 16;

            //read LogVersion
            UInt16 logVersion;
            logVersion = BitConverter.ToUInt16(fileHeader, currentOffset);
            header.logVersion = logVersion;
            currentOffset += 2;

            //read Version
            UInt16 version;
            version = BitConverter.ToUInt16(fileHeader, currentOffset);
            header.version = version;
            currentOffset += 2;

            //read LogLength
            UInt32 logLength;
            logLength = BitConverter.ToUInt32(fileHeader, currentOffset);
            header.logLength = logLength;
            currentOffset += 4;

            //read LogOffset
            UInt64 logOffset;
            logOffset = BitConverter.ToUInt32(fileHeader, currentOffset);
            header.logOffset = logOffset;
            currentOffset += 8;

            return header;
        }


        //gets the raw bat table
        private RawBatTable getRawBatTable(RegionTable table)
        {
            RawBatTable rawTable = new RawBatTable();
            UInt64 batOffset = 0;
            UInt64 batLength = 0;
            //read offset and length for BAT table
            foreach (RegionTableEntry entry in table.entries)
            {
                if (entry.guid[0] == 0x66)
                {
                    batOffset = entry.fileOffset;
                    batLength = entry.length;
                    break;
                }
            }

            //jump to first BAT entry
            this.sourceStream.Seek((long)batOffset, SeekOrigin.Begin);
            rawTable.vhdxOffset = batOffset;

            //read bat table
            rawTable.rawData = new byte[batLength];
            this.sourceStream.Read(rawTable.rawData, 0, (int)batLength);

            return rawTable;
        }

        //gets the raw header
        private byte[] getRawHeader()
        {
            byte[] buffer = new byte[1048576]; // 1MB * 1024 * 1024

            //just return the first 1MB of data
            this.sourceStream.Seek(0, SeekOrigin.Begin);
            this.sourceStream.Read(buffer, 0, buffer.Length);

            return buffer;
        }

        //gets the raw log section
        private byte[] getRawLog()
        {
            //jump the Header1
            this.sourceStream.Seek(65536, SeekOrigin.Begin); //64KB * 1024

            //jump to Header entry number 18
            this.sourceStream.Seek(68, SeekOrigin.Current); //4 * 17

            //read log length
            byte[] buffer = new byte[4];
            this.sourceStream.Read(buffer, 0, 4);
            UInt64 logLength = BitConverter.ToUInt32(buffer, 0);

            //read log offset
            this.sourceStream.Read(buffer, 0, 4);
            UInt64 logOffset = BitConverter.ToUInt32(buffer, 0);

            //jump to log offset
            this.sourceStream.Seek((Int32)logOffset, SeekOrigin.Begin);

            //read log section
            buffer = new byte[logLength];
            this.sourceStream.Read(buffer, 0, buffer.Length);


            return buffer;
        }

        //reads blockSize from MetadataTable
        private UInt32 getBlockSize(MetadataTable metadataTable)
        {
            UInt32 offset = 0;
            UInt32 length = 0;
            foreach (MetadataTableEntry entry in metadataTable.entries)
            {
                if (entry.itemID[0] == 0x37)
                {
                    offset = entry.offset;
                    length = entry.length;
                }
            }

            //jump to destination
            this.sourceStream.Seek(offset, SeekOrigin.Begin);

            //read block size
            byte[] buffer = new byte[4];
            this.sourceStream.Read(buffer, 0, 4);

            return BitConverter.ToUInt32(buffer, 0);
        }

        //reads "virtual disk ID" from MetadataTable
        private byte[] getVirtualDiskID(MetadataTable metadataTable)
        {
            UInt32 offset = 0;
            UInt32 length = 0;
            foreach (MetadataTableEntry entry in metadataTable.entries)
            {
                if (entry.itemID[0] == 0xAB)
                {
                    offset = entry.offset;
                    length = entry.length;
                }
            }

            //jump to destination
            this.sourceStream.Seek(offset, SeekOrigin.Begin);

            //read virtual disk ID size
            byte[] buffer = new byte[16];
            this.sourceStream.Read(buffer, 0, 16);

            return buffer;
        }

        //reads logicalSectorSize from MetadataTable
        private UInt32 getLogicalSectorSize(MetadataTable metadataTable)
        {
            UInt32 offset = 0;
            UInt32 length = 0;
            foreach (MetadataTableEntry entry in metadataTable.entries)
            {
                if (entry.itemID[0] == 0x1D)
                {
                    offset = entry.offset;
                    length = entry.length;
                }
            }

            //jump to destination
            this.sourceStream.Seek(offset, SeekOrigin.Begin);

            //read logicalSectorSize size
            byte[] buffer = new byte[4];
            this.sourceStream.Read(buffer, 0, 4);

            return BitConverter.ToUInt32(buffer, 0);
        }

        //reads virtualDiskSize from MetadataTable
        private UInt64 getVirtualDiskSize(MetadataTable metadataTable)
        {
            UInt32 offset = 0;
            UInt32 length = 0;
            foreach (MetadataTableEntry entry in metadataTable.entries)
            {
                if (entry.itemID[0] == 0x24)
                {
                    offset = entry.offset;
                    length = entry.length;
                }
            }

            //jump to destination
            this.sourceStream.Seek(offset, SeekOrigin.Begin);

            //read logicalSectorSize size
            byte[] buffer = new byte[8];
            this.sourceStream.Read(buffer, 0, 8);

            return BitConverter.ToUInt64(buffer, 0);
        }

       


        //parses the metadata region
        private MetadataTable parseMetadataTable(RegionTable table)
        {
            MetadataTable metadataTable = new MetadataTable();
            metadataTable.entries = new List<MetadataTableEntry>();
            UInt64 metadataTableOffset = 0;
            UInt32 metadataTableLength = 0;

            //read offset and length for meta table
            foreach (RegionTableEntry entry in table.entries)
            {
                if (entry.guid[0] == 0x06)
                {
                    metadataTableOffset = entry.fileOffset;
                    metadataTableLength = entry.length;
                    break;
                }
            }

            //jump to first meta entry
            this.sourceStream.Seek((long)metadataTableOffset, SeekOrigin.Begin);
            byte[] buffer = new byte[metadataTableLength];
            this.sourceStream.Read(buffer, 0, buffer.Length);

            MetadataTableHeader metadataTableHeader = new MetadataTableHeader();

            //read signature
            metadataTableHeader.signature = Encoding.ASCII.GetString(buffer, 0, 8);

            //read reserved
            metadataTableHeader.reserved = BitConverter.ToUInt16(buffer, 8);

            //read entry count
            metadataTableHeader.entryCount = BitConverter.ToUInt16(buffer, 10);

            metadataTable.header = metadataTableHeader;

            UInt32 entryOffset = 32;
            //iterate through all entries
            for (int i = 0; i < metadataTableHeader.entryCount; i++)
            {
                MetadataTableEntry entry = new MetadataTableEntry();

                //read itemID
                entry.itemID = new byte[16];
                Array.Copy(buffer, entryOffset, entry.itemID, 0, 16);

                //read offset
                entry.offset = BitConverter.ToUInt32(buffer, (int)entryOffset + 16) + (uint)metadataTableOffset; //add metadataTableOffset to get offset from beginning of file

                //read length
                entry.length = BitConverter.ToUInt32(buffer, (int)entryOffset + 20);

                metadataTable.entries.Add(entry);

                entryOffset += 32;
            }

            return metadataTable;
        }


        //parses the BAT table (chunkSize just necessary when removeSectorMask is set)
        private BATTable parseBATTable(RegionTable table, UInt32 chunkSize, UInt32 sectorBitmapBlocksCount, bool removeSectorMask)
        {
            BATTable batTable = new BATTable();
            batTable.entries = new List<BATEntry>();
            UInt64 batOffset = 0;
            UInt32 batLength = 0;

            //read offset and length for BAT table
            foreach (RegionTableEntry entry in table.entries)
            {
                if (entry.guid[0] == 0x66)
                {
                    batOffset = entry.fileOffset;
                    batLength = entry.length;
                    break;
                }
            }

            //jump to first BAT entry
            this.sourceStream.Seek((long)batOffset, SeekOrigin.Begin);

            //read whole table
            byte[] buffer = new byte[batLength];
            this.sourceStream.Read(buffer, 0, (int)batLength);

            batTable.rawBatTable = buffer;

            //each entry consists of 64bit, iterate
            UInt32 entryCount = batLength / 64;
            UInt32 lastSectorMaskDistance = 0;
            UInt32 removedSectorBitmapMasks = 0;
            for (int i = 0; i < entryCount; i++)
            {
                //jump over sector mask?
                if (removeSectorMask && removedSectorBitmapMasks < sectorBitmapBlocksCount && lastSectorMaskDistance > 0 && lastSectorMaskDistance % chunkSize == 0)
                {
                    lastSectorMaskDistance = 0;
                    removedSectorBitmapMasks++;
                    continue;
                }

                BATEntry newEntry = new BATEntry();
                UInt64 batEntry = BitConverter.ToUInt64(buffer, 8 * i);
                newEntry.state = (byte)(batEntry % 8);
                batEntry = batEntry >> 3;
                UInt32 reserved = (UInt32)(batEntry % Math.Pow(2, 17));
                batEntry = batEntry >> 17;
                UInt64 fileOffsetMB = batEntry;

                newEntry.FileOffsetMB = fileOffsetMB;
                newEntry.reserved = reserved;

                batTable.entries.Add(newEntry);

                if (removedSectorBitmapMasks < sectorBitmapBlocksCount && removeSectorMask)
                {
                    lastSectorMaskDistance++;
                }
            }

            return batTable;
        }

        

        //parses the region table by the given number (1 or 2)
        private RegionTable parseRegionTable(byte number)
        {
            if (number == 1)
            {
                //set pointer to RegionTable 1
                this.sourceStream.Seek(192 * 1024, SeekOrigin.Begin);
            }
            else
            {
                //set pointer to RegionTable 2
                this.sourceStream.Seek(256 * 1024, SeekOrigin.Begin);
            }

            //reserve buffer
            int regionSize = 256 * 1024 - 192 * 1024;
            byte[] buffer = new byte[regionSize];
            this.sourceStream.Read(buffer, 0, regionSize);


            RegionTable regionTable = new RegionTable();

            //===== parse header =====

            RegionTableHeader regionTableHeader = new RegionTableHeader();

            //read signature
            regionTableHeader.signature = Encoding.UTF8.GetString(buffer, 0, 4);

            //read checksum
            regionTableHeader.checksum = BitConverter.ToUInt32(buffer, 4);

            //read entry count
            regionTableHeader.entryCount = BitConverter.ToUInt32(buffer, 8);

            //read reserved
            regionTableHeader.reserved = BitConverter.ToUInt32(buffer, 12);

            regionTable.header = regionTableHeader;

            //===== parse entries =====

            RegionTableEntry[] entries = new RegionTableEntry[regionTableHeader.entryCount];
            Int32 entryByteOffset = 16;
            for (int i = 0; i < regionTableHeader.entryCount; i++)
            {
                RegionTableEntry entry = new RegionTableEntry();
                entry.guid = new byte[16];

                //copy guid
                Array.Copy(buffer, entryByteOffset, entry.guid, 0, 16);

                //read file offset
                entry.fileOffset = BitConverter.ToUInt64(buffer, entryByteOffset + 16);

                //read length
                entry.length = BitConverter.ToUInt32(buffer, entryByteOffset + 16 + 8);

                //read required
                entry.required = BitConverter.ToUInt32(buffer, entryByteOffset + 16 + 8 + 4);

                entries[i] = entry;
                entryByteOffset += 16 + 8 + 8;
            }

            regionTable.entries = entries;
            return regionTable;
        }

        public void Dispose()
        {
            this.close();
        }
    }


    public struct VHDXData
    {
        public FileHeader fileHeader;
        public BATTable batTable;
        public MetadataTable metadataTable;
        public byte[] rawLog;
        public UInt32 chunkRatio;
        public UInt32 logicalSectorSize;
        public UInt32 blockSize;
        public UInt64 virtualDiskSize;
        public byte[] virtualDiskID;
    }


    public struct RegionTable
    {
        public RegionTableHeader header;
        public RegionTableEntry[] entries;
    }

    public struct RegionTableHeader
    {
        public string signature;
        public UInt32 checksum;
        public UInt32 entryCount;
        public UInt32 reserved;
    }

    public struct RegionTableEntry
    {
        public byte[] guid;
        public UInt64 fileOffset;
        public UInt32 length;
        public UInt32 required;
    }

    public struct BATTable
    {
        public List<BATEntry> entries;
        public byte[] rawBatTable;
    }

    public struct BATEntry
    {
        public byte state;
        public UInt32 reserved;
        public UInt64 FileOffsetMB;
        public UInt32 payload;
    }

    public struct MetadataTable
    {
        public MetadataTableHeader header;
        public List<MetadataTableEntry> entries;
    }

    public struct MetadataTableHeader
    {
        public string signature;
        public UInt16 reserved;
        public UInt16 entryCount;
        public byte[] reserved2;
    }

    public struct MetadataTableEntry
    {
        public byte[] itemID;
        public UInt32 offset;
        public UInt32 length;
    }

    public struct FileHeader
    {
        public FileIdentifier fileIdentifier;
        public Header header1;
        public Header header2;
        public RegionTable regionTable1;
        public RegionTable regionTable2;
        public byte[] rawFileHeader;
    }

    public struct Header
    {
        public string signature;
        public byte[] checksum;
        public UInt64 sequenceNumber;
        public byte[] fileWriteGuid;
        public byte[] dataWriteGuid;
        public byte[] logGuid;
        public UInt16 logVersion;
        public UInt16 version;
        public UInt32 logLength;
        public UInt64 logOffset;
    }

    public struct FileIdentifier
    {
        public string signature;
        public string creator;
    }


    public struct RawBatTable
    {
        public UInt64 vhdxOffset;
        public byte[] rawData;
    }

    public struct RawLog
    {
        public UInt64 vhdxOffset, logLength;
        public byte[] rawData;
    }

}
