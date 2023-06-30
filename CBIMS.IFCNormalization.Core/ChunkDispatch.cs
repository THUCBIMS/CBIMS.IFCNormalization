// Copyright (C) 2023  Liu, Han; School of Software, Tsinghua University
//
// This file is part of CBIMS.IFCNormalization.
// CBIMS.IFCNormalization is free software: you can redistribute it and/or modify it under the terms of the GNU Lesser General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// CBIMS.IFCNormalization is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU Lesser General Public License for more details.
// You should have received a copy of the GNU Lesser General Public License along with CBIMS.IFCNormalization. If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CBIMS.IFCNormalization.Core;
using CBIMS.IFCNormalization.Interface;

namespace CBIMS.IFCNormalization
{
    internal class ChunkDispatch
    {
        // Constants

        const int PH_GLOBALID = -1;
        const int PH_OWNERHISTORY = -2;


        const int GROUP_SIZE = 1000;

        static readonly Dictionary<int, int> maxChunkCounts = new Dictionary<int, int> {
            { 3, 214 },
            { 4, 2147 },
            { 5, 21474 },
            { 6, 214748 },
            { 7, 2147483 },
            { 8, 21474836 },
            { 9, 214748364 },
        };

        static readonly Dictionary<int, int> chunkSizes = new Dictionary<int, int> {
            { 3, 10000000 },
            { 4, 1000000 },
            { 5, 100000 },
            { 6, 10000 },
            { 7, 1000 },
            { 8, 100 },
            { 9, 10 }
        };

        // Settings

        internal int ChunkLevel = 5;
        internal double ChunkSpareRate = 2.0;

        internal bool DoPara = true;
        internal bool DoSegment = false;

        internal bool UseExpChunkNum = true;
        internal bool RemoveOwnerHistoryOnOutput = false;

        internal bool DoCloneOnMod = false;
        internal bool DoSortUnorderedSet = true;

        public string[] TypesIgnoreGlobalId = new string[] {
            "IfcRelationship", "IfcPropertyDefinition",
            "IfcBeamType", "IfcColumnType",
            "IfcSpatialStructureElement", "IfcProject"
        };
        public string[] TypesIgnoreOwnerHistory = new string[] { "IfcRoot" };
        public (string, string)[] TypesIncludeImportantInv = new (string, string)[] { ("IfcRepresentationItem", "StyledByItem") };


        // Data cache

        internal Dictionary<int, string> chunkTypeMap = new Dictionary<int, string>();
        internal Dictionary<string, List<int>> chunkTypeMapInv = new Dictionary<string, List<int>>();

        internal Dictionary<string, List<int>> typeNameOriginIds = new Dictionary<string, List<int>>();

        private Dictionary<string, Dictionary<string, LibRef>> typeHashToRefMap = new Dictionary<string, Dictionary<string, LibRef>>();
        //for each type, the size of content usually less than OriginIds



        internal Dictionary<int, List<LibRef>> chunkData = new Dictionary<int, List<LibRef>>();

        private string[] idToHashBag = null;
        private byte[][] idToHashBytesBag = null;
        private int[] idToHashCodeBag = null;

        internal ISTEPDoc Model;

        // Settings cache

        internal HashSet<string> _typesIgnoreGlobalId = new HashSet<string>();
        internal HashSet<string> _typesIgnoreOwnerHistory = new HashSet<string>();
        internal Dictionary<string, HashSet<string>> _typesIncludeImportantInv = new Dictionary<string, HashSet<string>>();
        internal Dictionary<string, HashSet<int>> _typeAttrsAsUnordredSet = new Dictionary<string, HashSet<int>>();

        //stat

        internal int Count_Hash { get; private set; } = 0;

        private static ConcurrentDictionary<int, HashAlgorithm> __threadHashAlgorithms = new ConcurrentDictionary<int, HashAlgorithm>();
        //threads
        private static HashAlgorithm _getHashAlgorithmForCurrentThread()
        {
            var threadId = Thread.CurrentThread.ManagedThreadId;
            if (__threadHashAlgorithms.TryGetValue(threadId, out HashAlgorithm algo))
            {
                return algo;
            }
            //var newAlgo = MD5.Create();
            var newAlgo = SHA256.Create();
            __threadHashAlgorithms.TryAdd(threadId, newAlgo);
            return newAlgo;
        }



        internal ChunkDispatch(ISTEPDoc model, int chunkLevel, double chunkSpareRate, 
            bool parallel, bool doSegment)
        {
            this.ChunkLevel = chunkLevel;
            this.ChunkSpareRate = chunkSpareRate;
            this.DoPara = parallel;
            this.DoSegment = doSegment;

            this.Model = model;
        }

        internal void Init()
        {
            var max_id = Model.Ids.Max();
            idToHashBytesBag = new byte[max_id + 1][];
            idToHashCodeBag = new int[max_id + 1];

            _initTypeSettings();

            foreach (var id in Model.Ids)
            {
                var ent = Model.GetInstance(id);
                var typeName = ent.TypeUpper;
                if (!typeNameOriginIds.ContainsKey(typeName))
                {
                    typeNameOriginIds.Add(typeName, new List<int>());

                    //init the place for concurrent input
                    typeHashToRefMap.Add(typeName, null);

                }
                typeNameOriginIds[typeName].Add(id);
            }

            Model.InitInverseCache(_typesIncludeImportantInv);

        }


        internal void HashCal()
        {
            // step 0: group all entities with levels

            Dictionary<int, int> idLevelMap = new Dictionary<int, int>();
            foreach (var id in Model.Ids)
            {
                _levelOneEntity(id, idLevelMap);
            }
            Dictionary<int, List<int>> levelIdMap = _inv(idLevelMap);
            var levels = levelIdMap.Keys.ToList();
            levels.Sort();

            // step 1: parse all entities

            foreach (var level in levels)
            {

                if (DoPara)
                {
                    var ids = levelIdMap[level];
                    List<List<int>> idGroups = _groupIds(ids);
                    Parallel.ForEach(idGroups, idGroup =>
                    {
                        _parseOneEntityGroup(idGroup);
                    });

                }
                else
                {
                    foreach (var id in levelIdMap[level])
                    {
                        var entity = Model.GetInstance(id);
                        _parseOneEntity(entity);
                    }
                }
            }

            // step 2: dealing with ImportantInv

            var types = _typesIncludeImportantInv.Keys.ToList();
            Dictionary<int, byte[]>[] hashOverrides = new Dictionary<int, byte[]>[types.Count];

            if (DoPara)
            {
                (int, string)[] pairs = new (int, string)[types.Count];
                for (int i = 0; i < types.Count; i++)
                {
                    pairs[i] = (i, types[i]);
                }
                Parallel.ForEach(pairs, pair =>
                {
                    hashOverrides[pair.Item1] = _calImportantInv_one_type(pair.Item2);
                });
            }
            else
            {
                for (int i = 0; i < types.Count; i++)
                {
                    hashOverrides[i] = _calImportantInv_one_type(types[i]);
                }
            }



            foreach (var hashOverride in hashOverrides)
            {
                foreach (var kv in hashOverride)
                {
                    idToHashBytesBag[kv.Key] = kv.Value;
                    idToHashCodeBag[kv.Key] = Utils_Hash.GetStableNonNegativeHashCode(kv.Value);
                }
            }

            idToHashBag = new string[idToHashCodeBag.Length];


        }



        internal void InitPrefixSpaces()
        {
            if (DoPara)
            {
                Parallel.ForEach(typeNameOriginIds.Keys, type =>
                {
                    _collectOneType(type);
                });
            }
            else
            {
                foreach (var type in typeNameOriginIds.Keys)
                {
                    _collectOneType(type);
                }
            }


            Dictionary<string, int> typeChunkCount = new Dictionary<string, int>();

            double chunkSize = chunkSizes[ChunkLevel];

            int maxChunkCount = maxChunkCounts[ChunkLevel];

            int totalChunks = 0;

            foreach (var type in typeHashToRefMap.Keys)
            {
                int typeUniqueNodeCount = typeHashToRefMap[type].Count;

                int min_chunk_count = (int)Math.Ceiling(typeUniqueNodeCount * ChunkSpareRate / chunkSize);

                if (UseExpChunkNum)
                {
                    int exp = 0;
                    while (true)
                    {
                        int chunk_count = (int)Math.Pow(2, exp);
                        if (chunk_count >= min_chunk_count)
                        {
                            typeChunkCount[type] = chunk_count;
                            break;
                        }
                        exp++;
                    }
                }
                else
                {
                    typeChunkCount[type] = min_chunk_count;
                }


                totalChunks += typeChunkCount[type];
            }

            if (totalChunks > maxChunkCount)
                throw new InvalidOperationException();

            _allocateTypeChunkIds(typeChunkCount);

        }



        internal void Dispatch()
        {

            if (DoPara)
            {
                Parallel.ForEach(typeNameOriginIds.Keys, type =>
                {
                    _dispatchOneType(type);
                });

                Parallel.ForEach(chunkData.Keys, chunkCode =>
                {
                    _dispatchOneChunk(chunkCode);
                });
            }
            else
            {
                foreach (var type in typeNameOriginIds.Keys)
                {
                    _dispatchOneType(type);
                }

                foreach (var chunkCode in chunkData.Keys)
                {
                    _dispatchOneChunk(chunkCode);
                }
            }

        }

        internal IEnumerable<byte[]> ExportBytes()
        {
            yield return Encoding.ASCII.GetBytes(_getHeaderBlockStr());

            var chunkCodes = chunkData.Keys.ToList();
            chunkCodes.Sort();

            foreach (var chunkCode in chunkCodes)
            {
                yield return Encoding.ASCII.GetBytes(_getChunkBlockStr(chunkCode));
            }

            yield return Encoding.ASCII.GetBytes(_getBottomBlockStr());
        }

        internal string AssembleResult()
        {
            StringBuilder sb = new StringBuilder();

            sb.Append(_getHeaderBlockStr());

            if(DoSegment)
                sb.AppendLine("/*========*/");

            var chunkCodes = chunkData.Keys.ToList();
            chunkCodes.Sort();

            if (DoPara)
            {
                string[] outstrs = new string[chunkCodes.Max()];

                Parallel.ForEach(chunkCodes, chunkCode =>
                {
                    outstrs[chunkCode - 1] = _getChunkBlockStr(chunkCode);
                });

                foreach (var chunkCode in chunkCodes)
                {
                    sb.Append(outstrs[chunkCode - 1]);
                    if (DoSegment)
                        sb.AppendLine("/*========*/");
                }
            }
            else
            {
                foreach (var chunkCode in chunkCodes)
                {
                    sb.Append(_getChunkBlockStr(chunkCode));
                    if (DoSegment)
                        sb.AppendLine("/*========*/");
                }
            }

            sb.Append(_getBottomBlockStr());


            return sb.ToString();

        }

        private string _getHeaderBlockStr()
        {
            StringBuilder sb = new StringBuilder();

            //sb.AppendLine("ISO-10303-21;");
            //sb.AppendLine("HEADER;");

            sb.AppendLine(Model.GetHeaderString());

            //sb.AppendLine("ENDSEC;");
            //sb.AppendLine("DATA;");

            return sb.ToString();
        }

        private string _getBottomBlockStr()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("ENDSEC;");
            sb.AppendLine("END-ISO-10303-21;");
            return sb.ToString();
        }






        private void _allocateTypeChunkIds(Dictionary<string, int> typeChunkCount)
        {
            Dictionary<string, string> allNames = new Dictionary<string, string>();
            foreach (string typeName in typeChunkCount.Keys)
            {
                for (int i = 0; i < typeChunkCount[typeName]; i++)
                {
                    string new_name = typeName + "_" + i;

                    allNames[new_name] = typeName;
                }
            }
            var _allNames = allNames.Keys.ToList();
            _allNames.Sort();

            int maxChunkCount = maxChunkCounts[ChunkLevel];

            foreach (string new_name in _allNames)
            {
                int hashCode = Utils_Hash.GetStableNonNegativeHashCode(new_name);

                string typeName = allNames[new_name];

                int chunkID = hashCode % maxChunkCount;
                while (true)
                {
                    if (!chunkTypeMap.ContainsKey(chunkID))
                    {
                        chunkTypeMap[chunkID] = typeName;
                        if (!chunkTypeMapInv.ContainsKey(typeName))
                            chunkTypeMapInv[typeName] = new List<int>();
                        chunkTypeMapInv[typeName].Add(chunkID);
                        chunkData[chunkID] = new List<LibRef>();
                        break;
                    }
                    else
                    {
                        chunkID++;
                        if (chunkID == maxChunkCount)
                            chunkID = 0;
                    }
                }

            }

            foreach (var list in chunkTypeMapInv.Values)
            {
                list.Sort();
            }

        }

        private void _initTypeSettings()
        {
            _initTypeList(TypesIgnoreGlobalId, _typesIgnoreGlobalId);

            _initTypeList(TypesIgnoreOwnerHistory, _typesIgnoreOwnerHistory);

            _initTypeAttrPair(TypesIncludeImportantInv, _typesIncludeImportantInv);

            //20230103 try to cache unordered set positions
            if (DoSortUnorderedSet)
            {

                foreach(var typeName in Model.Schema_EntityTypes)
                {
                    string nameUpper = typeName.ToUpperInvariant();

                    var argInfos = Model.Schema_GetAllArgInfo(typeName);
                    foreach (var argInfo in argInfos)
                    {
                        if(argInfo.IsCollection && argInfo.CollectionType == "SET")
                        {
                            if (!_typeAttrsAsUnordredSet.ContainsKey(nameUpper))
                            {
                                _typeAttrsAsUnordredSet[nameUpper] = new HashSet<int>();
                            }
                            _typeAttrsAsUnordredSet[nameUpper].Add(argInfo.Index);
                        }
                    }
                }
            }
        }


        private void _initTypeList(string[] source, HashSet<string> target)
        {
            foreach (var entityName in source)
            {
                __initTypeList(entityName, target);
            }
        }
        private void __initTypeList(string entityName, HashSet<string> target)
        {

            var upper = entityName.ToUpperInvariant();

            if (target.Contains(upper))
                return;

            target.Add(upper);
            
            var subTypes = Model.Schema_GetSubTypes(entityName);

            if (subTypes != null && subTypes.Any())
            {
                foreach (string subEntity in subTypes)
                {
                    __initTypeList(subEntity, target);
                }
            }


        }

        private void _initTypeAttrPair((string, string)[] sourcePairs, Dictionary<string, HashSet<string>> target)
        {
            foreach (var pair in sourcePairs)
            {
                __initTypeAttrPair(pair, target);
            }
        }

        private void __initTypeAttrPair((string, string) sourcePair, Dictionary<string, HashSet<string>> target)
        {
            var entityName = sourcePair.Item1;
            var attrName = sourcePair.Item2;

            var upper = entityName.ToUpperInvariant();

            if (!target.ContainsKey(upper))
            {
                target[upper] = new HashSet<string>();
            }

            if (target[upper].Contains(attrName))
                return;


            target[upper].Add(attrName);


            var subTypes = Model.Schema_GetSubTypes(entityName);

            if (subTypes != null && subTypes.Any())
            {
                foreach (string subEntity in subTypes)
                {
                    __initTypeAttrPair((subEntity, attrName), target);
                }
            }
        }





        private int _levelOneEntity(int id, Dictionary<int, int> idLevelMap)
        {
            if (idLevelMap.ContainsKey(id))
                return idLevelMap[id];
            var ent = Model.GetInstance(id);
            HashSet<int> refs = new HashSet<int>();
            _getRefs(ent.Data, refs);

            int level = 0;

            foreach (var inner_ref in refs)
            {
                int _level = _levelOneEntity(inner_ref, idLevelMap);
                if (level < _level + 1) level = _level + 1;
            }

            idLevelMap.Add(id, level);
            return level;
        }

        private void _getRefs(ISTEPColl data, HashSet<int> inner_refs)
        {
            foreach (var item in data)
            {
                if (item.Type == STEPType.REF)
                {
                    inner_refs.Add((int)item.Value);
                }
                else if (item is ISTEPColl list)
                {
                    _getRefs(list, inner_refs);
                }
            }
        }

        private Dictionary<int, List<int>> _inv(Dictionary<int, int> idLevelMap)
        {
            Dictionary<int, List<int>> output = new Dictionary<int, List<int>>();
            foreach (var id in idLevelMap.Keys)
            {
                var level = idLevelMap[id];
                if (!output.ContainsKey(level))
                    output.Add(level, new List<int>());
                output[level].Add(id);
            }
            return output;
        }


        private List<List<int>> _groupIds(List<int> ids)
        {
            //int size = chunkSizes[ChunkLevel];
            int size = GROUP_SIZE;

            List<List<int>> output = new List<List<int>>();
            List<int> current = null;

            int i = 0;
            foreach (var id in ids)
            {
                if (i % size == 0)
                {
                    current = new List<int>();
                    output.Add(current);
                }
                current.Add(id);
                i++;
            }
            return output;
        }

        private void _parseOneEntityGroup(List<int> idGroup)
        {
            foreach (var id in idGroup)
            {
                var entity = Model.GetInstance(id);
                _parseOneEntity(entity);
            }
        }

        private void _parseOneEntity(ISTEPInst entity)
        {
            int id = entity.Id;

            var data = entity.Data;

            bool cloned = false;

            int true_ownerHistory = PH_OWNERHISTORY;

            if (_typesIgnoreGlobalId.Contains(entity.TypeUpper))
            {
                if (DoCloneOnMod && !cloned)
                {
                    data = data.Clone() as ISTEPColl;
                    cloned = true;
                }
                data.SetRef(0, PH_GLOBALID);
            }
            if (_typesIgnoreOwnerHistory.Contains(entity.TypeUpper))
            {
                if (data[1].Type == STEPType.REF)
                {
                    if (DoCloneOnMod && !cloned)
                    {
                        data = data.Clone() as ISTEPColl;
                        cloned = true;
                    }
                    true_ownerHistory = data.GetRef(1).Value;
                    data.SetRef(1, PH_OWNERHISTORY);
                }
            }


            if (_typeAttrsAsUnordredSet.ContainsKey(entity.TypeUpper))
            {
                foreach (var index in _typeAttrsAsUnordredSet[entity.TypeUpper])
                {
                    if (data[index] is ISTEPColl ilist)
                    {
                        ilist.Sort(_sortArgList);
                    }

                }
            }

            List<byte> content = new List<byte>(1024);

            _append(content, entity.TypeUpper);
            _parseOneList(content, data, idToHashBytesBag);
            content.Add((byte)';');

            var contentArr = content.ToArray();
            contentArr = _HashEncode(contentArr);


            idToHashBytesBag[id] = contentArr;
            idToHashCodeBag[id] = Utils_Hash.GetStableNonNegativeHashCode(contentArr);

            //recover true OwnerHistory

            if (true_ownerHistory != PH_OWNERHISTORY)
            {
                data.SetRef(1, true_ownerHistory);
            }

        }

        private int _sortRefList(int x, int y)
        {
            var codeX = idToHashCodeBag[x];
            var codeY = idToHashCodeBag[y];
            if (codeX != codeY)
            {
                return codeX.CompareTo(codeY);
            }

            var hashX = idToHashBytesBag[x];
            var hashY = idToHashBytesBag[y];

            return _compare(hashX, hashY);
        }

        private static int _compare(byte[] hashX, byte[] hashY)
        {
            for (int i = 0; i < hashX.Length && i < hashY.Length; i++)
            {
                if (hashX[i] != hashY[i])
                {
                    return hashX[i].CompareTo(hashY[i]);
                }
            }
            if (hashX.Length != hashY.Length)
            {
                return hashX.Length.CompareTo(hashY.Length);
            }
            return 0;
        }

        private int _sortArgList(ISTEPArg x, ISTEPArg y)
        {
            var xType = x.Type; 
            var yType = y.Type;
            if (xType == STEPType.REF && yType == STEPType.REF)
            {
                return _sortRefList((int)x.Value, (int)y.Value);
            }
            else if (xType == STEPType.INT && yType == STEPType.INT)
            {
                return ((int)x.Value).CompareTo((int)y.Value);
            }
            else if (xType == STEPType.FLOAT && yType == STEPType.FLOAT)
            {
                return ((double)x.Value).CompareTo((double)y.Value);
            }
            else if (xType == STEPType.STRING && yType == STEPType.STRING)
            {
                return ((string)x.Value).CompareTo((string)y.Value);
            }
            else
            {
                return x.ToSTEPString().CompareTo(y.ToSTEPString());
            }
        }


        private byte[] _HashEncode(byte[] bytes)
        {
            //Count_Hash++;
            HashAlgorithm algo = _getHashAlgorithmForCurrentThread();
            return algo.ComputeHash(bytes);
        }


        private static void _parseOneList(List<byte> content, ISTEPColl data, byte[][] idToHashBytesBag)
        {
            content.Add((byte)'(');

            bool first = true;
            foreach (var arg in data)
            {
                if (first)
                {
                    first = false;
                }
                else
                {
                    content.Add((byte)',');
                }

                var argType = arg.Type;

                if (argType == STEPType.COLL)
                {
                    var sublist = arg as ISTEPColl;
                    if (sublist.Name != null)
                    {
                        _append(content, sublist.Name);
                    }

                    _parseOneList(content, sublist, idToHashBytesBag);
                }
                else if (argType == STEPType.REF)
                {
                    int _id = (int)arg.Value;
                    if (_id > 0)
                    {
                        content.AddRange(idToHashBytesBag[_id]);
                    }
                    else if (_id == PH_GLOBALID || _id == PH_OWNERHISTORY)
                    {
                        content.Add((byte)'$');
                    }
                    else
                    {
                        throw new NotImplementedException();
                    }
                }
                else if (argType == STEPType.INT)
                {
                    if (arg.Value is int intval)
                    {
                        long longval = intval;
                        content.AddRange(BitConverter.GetBytes(longval));
                    } 
                    else if (arg.Value is long longval)
                    {
                        content.AddRange(BitConverter.GetBytes(longval));
                    }
                    else
                    {
                        throw new NotImplementedException();
                    }
                    
                }
                else if (argType == STEPType.FLOAT)
                {
                    content.AddRange(BitConverter.GetBytes((double)arg.Value));
                }
                else if (argType == STEPType.STRING)
                {
                    _append(content, (string)arg.Value);
                }
                else if (argType == STEPType.ENUM)
                {
                    _append(content, (string)arg.Value);
                }
                else if (argType == STEPType.LOGICAL)
                {
                    bool? boolval = (bool?)arg.Value;
                    char v = boolval.HasValue ? (boolval.Value ? 'T' : 'F') : 'U';
                    content.Add((byte)v);
                }
                else if (argType == STEPType.BINARY)
                {
                    content.AddRange((byte[])arg.Value);
                }
                else if (argType == STEPType.NONDEF)
                {
                    content.Add((byte)'$');
                }
                else if (argType == STEPType.OVERRIDE)
                {
                    content.Add((byte)'*');
                }
                else
                {
                    throw new NotImplementedException();
                    //_append(ref content, ref pos, arg.ToSTEPString());
                }
            }
            content.Add((byte)')');
        }



        private static void _append(List<byte> content, string v)
        {
            byte[] bytes = Encoding.Default.GetBytes(v);
            content.AddRange(bytes);
        }



        private string _getChunkBlockStr(int chunkCode)
        {
            StringBuilder sb = new StringBuilder();

            var refs = chunkData[chunkCode];
            foreach (var _ref in refs)
            {
                sb.AppendLine(_outputOneEntity(_ref));
            }

            return sb.ToString();
        }


        private string _outputOneEntity(LibRef _ref)
        {
            var entity = Model.GetInstance(_ref.InID);

            var data = entity.Data;

            bool cloned = false;

            if (_typesIgnoreGlobalId.Contains(entity.TypeUpper))
            {
                if (DoCloneOnMod && !cloned)
                {
                    data = data.Clone() as ISTEPColl;
                    cloned = true;
                }
                _hashToGlobalId(data, _ref.HashStr);
            }
            if (RemoveOwnerHistoryOnOutput && _typesIgnoreOwnerHistory.Contains(entity.TypeUpper))
            {
                if (data[1].Type == STEPType.REF)
                {
                    if (DoCloneOnMod && !cloned)
                    {
                        data = data.Clone() as ISTEPColl;
                        cloned = true;
                    }
                    data.SetNondef(1);
                }
            }

            StringBuilder output = new StringBuilder();

            output.Append(_ref.ToStringForStorage());
            output.Append("=");
            output.Append(entity.TypeUpper);
            _outputOneList(output, data);
            output.Append(";");

            return output.ToString();
        }

        private void _outputOneList(StringBuilder sb, ISTEPColl data)
        {
            sb.Append("(");
            bool first = true;
            foreach (var arg in data)
            {
                if (first)
                {
                    first = false;
                }
                else
                {
                    sb.Append(",");
                }

                if (arg is ISTEPColl sublist)
                {
                    if (sublist.Name != null)
                    {
                        sb.Append(sublist.Name.ToUpperInvariant());
                    }
                    _outputOneList(sb, sublist);
                }
                else if (arg.Type == STEPType.REF)
                {
                    var _id = (int)arg.Value;
                    if (_id > 0)
                    {
                        var hash = idToHashBag[_id];
                        var target = Model.GetInstance(_id);

                        var targetRef = typeHashToRefMap[target.TypeUpper][hash];

                        sb.Append(targetRef.ToStringForStorage());
                    }
                    else
                    {
                        throw new NotImplementedException();
                    }
                }
                else
                {
                    sb.Append(arg.ToSTEPString());
                }
            }
            sb.Append(")");
        }


        private static void _hashToGlobalId(ISTEPColl data, string hashStr)
        {
            byte[] bytes = MD5.Create().ComputeHash(Encoding.Default.GetBytes(hashStr));
            Guid guid = new Guid(bytes);
            data.SetString(0, IfcGuid.IfcGuid.ToIfcGuid(guid));
        }

        private void _collectOneType(string type)
        {
            Dictionary<string, LibRef> _localhashToRefMap = new Dictionary<string, LibRef>();
            foreach (var id in typeNameOriginIds[type])
            {
                var hashBytes = idToHashBytesBag[id];
                var hashStr = Utils_Hash.BinToBase64String(hashBytes);
                idToHashBag[id] = hashStr;
                var hashCode = idToHashCodeBag[id];
                if (!_localhashToRefMap.ContainsKey(hashStr))
                {
                    _localhashToRefMap[hashStr] = new LibRef()
                    {
                        InID = id,
                        HashStr = hashStr,
                        HashCode = hashCode
                    };
                }
            }

            //merge to the total data
            typeHashToRefMap[type] = _localhashToRefMap;

        }

        private void _dispatchOneType(string type)
        {
            var _localhashToRefMap = typeHashToRefMap[type];

            var chunkCodes = chunkTypeMapInv[type];
            int chunkCount = chunkCodes.Count;

            int chunkSize = chunkSizes[ChunkLevel];

            var refList = _localhashToRefMap.Values.ToList();

            refList.Sort((x, y) =>
                x.HashCode == y.HashCode ? x.HashStr.CompareTo(y.HashStr) : x.HashCode - y.HashCode);

            if (chunkCount == 1)
            {
                var chunkCode = chunkCodes[0];
                foreach (var _ref in refList)
                {
                    chunkData[chunkCode].Add(_ref);
                }
            }
            else
            {
                foreach (var _ref in refList)
                {
                    int code = _ref.HashCode;
                    var chunkRec = code % chunkCount;
                    var chunkCode = chunkCodes[chunkRec];

                    while (chunkData[chunkCode].Count >= chunkSize)
                    {
                        chunkRec = (chunkRec + 1) % chunkCount;
                        chunkCode = chunkCodes[chunkRec];
                    }

                    chunkData[chunkCode].Add(_ref);
                }
            }

        }


        private void _dispatchOneChunk(int chunkCode)
        {
            int chunkSize = chunkSizes[ChunkLevel];

            var list = chunkData[chunkCode];
            if (list.Count > chunkSize)
            {
                throw new InvalidOperationException();
            }

            HashSet<int> usedPlaces = new HashSet<int>();
            foreach (var _ref in list)
            {
                int code = _ref.HashCode;

                int place = code % chunkSize;
                while (true)
                {
                    if (!usedPlaces.Contains(place))
                    {
                        usedPlaces.Add(place);

                        _ref.OutID = chunkCode * chunkSize + place;

                        break;
                    }
                    else
                    {
                        place++;
                        if (place == chunkSize)
                            place = 0;
                    }
                }
            }

            list.Sort((x, y) => x.OutID.CompareTo(y.OutID));
        }

        private Dictionary<int, byte[]> _calImportantInv_one_type(string type)
        {
            Dictionary<int, byte[]> hashOverride = new Dictionary<int, byte[]>();

            if (typeNameOriginIds.ContainsKey(type))
            {

                var attrsSort = _typesIncludeImportantInv[type].ToList();

                attrsSort.Sort();

                foreach (var id in typeNameOriginIds[type])
                {
                    var hashBytes = idToHashBytesBag[id];

                    List<byte> sb = new List<byte>(1024);
                    sb.AddRange(hashBytes);

                    bool changed = false;
                    foreach (var attr in attrsSort)
                    {
                        var invIds = Model.GetCachedInverseIDs(id, attr);

                        if (!invIds.Any())
                            continue;

                        changed = true;

                        List<int> invRefs = new List<int>();
                        foreach (var invId in invIds)
                        {
                            invRefs.Add(invId);
                        }

                        invRefs.Sort(_sortRefList);

                        sb.Add((byte)'|');
                        _append(sb, attr);
                        _append(sb, ":[");
                        foreach (var invId in invIds)
                        {
                            sb.AddRange(idToHashBytesBag[id]);
                            sb.Add((byte)',');
                        }
                        sb.Add((byte)']');
                    }

                    if (changed)
                    {
                        var sbArr = sb.ToArray();
                        sbArr = _HashEncode(sbArr);
                        hashOverride.Add(id, sbArr);
                    }
                }
            }

            return hashOverride;
        }

    }
}
