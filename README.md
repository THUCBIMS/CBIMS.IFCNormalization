# CBIMS.IFCNormalization


## Introduction

CBIMS.IFCNormalization is a tool to "normalize" an IFC file (in ISO 10303-21). The normalized IFC file can be used for better comparison of changes between versions, and for storage in Git-like tools.

The tool has the following features:

* Merging redundant nodes in an IFC file.
* Re-assigning stable integer row IDs according to the row contents and references.
* Sorting the rows, and (optionally) segmenting the rows into chunks.

Please refer to:

*Liu H, Gao G, Gu M.* 
**A Parallel IFC Normalization Algorithm for Incremental Storage and Version Control.**
International Workshop on Intelligent Computing in Engineering (EG-ICE), 2023: 511-520. 


## Dependencies and resources

The project uses the following dependencies from the NuGet:

* `Xbim.IO.MemoryModel` - https://github.com/xBimTeam/XbimEssentials

Other resources contained in the repository:

* `CBIMS.IFCNormalization.Core/ThirdParty/IfcGuid.cs` - Methods for converting between GUIDs and IFC GlobalIds, available from: https://github.com/hakonhc/IfcGuid/blob/master/IfcGuid/IfcGuid.cs .


## Folders in the repository

* `CBIMS.IFCNormalization.CMD` - The command-line entrance of the tool.
* `CBIMS.IFCNormalization.Core` - The core implementation of the IFC normalization algorithm.
* `CBIMS.IFCNormalization.Interface` - The interface for accessing the input IFC data structure.
* `CBIMS.IFCNormalization.Xbim` - The implementation of the interface for getting IFC data using `Xbim`

## Usage

```
dotnet CBIMS.IFCNormalization.CMD.dll -i <input path>
        [-o <output path>]
        [--level <chunk level>]
        [--spare <chunk spare rate>]
        [--parallel <true|false>]
        [--exp_chunk_num <true|false>]
        [--rm_ownerhistory <true|false>]
        [--do_segment <true|false>]
```

`-i: `
        An input IFC path.

`-o: <default inputPath/inputFileName.norm.ifc> `
        The output IFC path.
        	
`--level: <default 5> `
        The level of chunk size.
```
Level   Chunk size      Max chunk numbers
3       10000000        214
4       1000000         2147
5       100000          21474
6       10000           214748
7       1000            2147483
8       100             21474836
9       10              214748364
```

`--spare: <default 2.0> `
        A real spare rate greater than 1.0 to make more efficient assignment of node storage.

`--parallel: <default true> `
        Use multi-core CPU to speed up calculation.

`--exp_chunk_num: <default true> `
        Using the exponential function for increasing the number of chunks of each type.

`--rm_ownerhistory: <default true> `
        Removing IfcOwnerHistory references for all IfcRoot nodes on output.

`--do_segment: <default false> `
        Adding `"/*========*/"` as segmentation for each chunk.
        
## License

CBIMS.IFCNormalization uses `GNU Lesser General Public License (LGPL)`. 
Please refer to:
https://www.gnu.org/licenses/lgpl-3.0.en.html

## About CBIMS

**CBIMS** (Computable Building Information Modeling Standards) is a set of research projects aiming at the development of computable BIM standards for the interoperability of BIM data exchange and the automation of BIM-based workflow. The projects are led by the BIM research group at the School of Software, Tsinghua University, China.

