// Copyright (C) 2023  Liu, Han; School of Software, Tsinghua University
//
// This file is part of CBIMS.IFCNormalization.
// CBIMS.IFCNormalization is free software: you can redistribute it and/or modify it under the terms of the GNU Lesser General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// CBIMS.IFCNormalization is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU Lesser General Public License for more details.
// You should have received a copy of the GNU Lesser General Public License along with CBIMS.IFCNormalization. If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xbim.IO.Memory;
using Microsoft.Extensions.Logging;
using CBIMS.IFCNormalization.Interface;
using System.IO;
using Xbim.Common;
using Xbim.IO.Step21;
using Xbim.Common.Metadata;

namespace CBIMS.IFCNormalization.Xbim
{
    public class XbimSTEPDoc : ISTEPDoc
    {
        public MemoryModel _model;
        public ILogger _logger;
        private ExpressMetaData _metadata => _model.Metadata;

        internal Dictionary<int, ISTEPInst> instances = new Dictionary<int, ISTEPInst>();

        private Dictionary<string, HashSet<int>> cateMap = new Dictionary<string, HashSet<int>>();
        //typeUpper to instIds

        private Dictionary<string, Dictionary<int, HashSet<int>>> inverseCache
            = new Dictionary<string, Dictionary<int, HashSet<int>>>();
        //key: entityUpper:attriUpper
        
        
        public XbimSTEPDoc(string ifcPath, ILogger logger = null)
        {
            _logger = logger;
            _model = MemoryModel.OpenReadStep21(ifcPath, _logger);
            _initInstances();

            _patchForOptionalIfcLogical(ifcPath);
        }

        public string GetHeaderString()
        {
            StringBuilder sb = new StringBuilder();
            TextWriter _writer = new StringWriter(sb);
            Part21Writer.WriteHeader(_model.Header, _writer);
            return sb.ToString();
        }

        public ICollection<int> Ids => instances.Keys;

        public IEnumerable<string> Schema_EntityTypes
        {
            get
            {
                return _metadata.Types().Select(t => t.Name);
            }
        }

        private void _initInstances()
        {
            foreach (IPersistEntity instance in _model.Instances)
            {
                XbimSTEPInst _inst = new XbimSTEPInst(instance);
                instances[_inst.Id] = _inst;

                string typeUpper = _inst.TypeUpper;
                if(!cateMap.ContainsKey(typeUpper))
                {
                    cateMap[typeUpper] = new HashSet<int>();
                }
                cateMap[typeUpper].Add(_inst.Id);

                
            }
        }

        private void _patchForOptionalIfcLogical(string ifcPath)
        {
            if (cateMap.ContainsKey("IFCMATERIALLAYER"))
            {
                Dictionary<string, string> lineCache = new Dictionary<string, string>();
                using(StreamReader sr = new StreamReader(ifcPath))
                {
                    while (true) 
                    { 
                        string line = sr.ReadLine();
                        if (line == null)
                            break;

                        if (line.StartsWith("#"))
                        {
                            int place = line.IndexOf('=');
                            if (place != -1)
                            {
                                string prefix = line.Substring(0, place).Trim();
                                string suffix = line.Substring(place + 1).Trim();
                                lineCache[prefix] = suffix;
                            }
                        }
                    }
                }

                foreach(int instId in cateMap["IFCMATERIALLAYER"])
                {
                    string prefix = $"#{instId}";
                    if (lineCache.ContainsKey(prefix))
                    {
                        string suffix = lineCache[prefix];
                        string[] split = suffix.Split(',');
                        if (split.Length > 3)
                        {
                            string target = split[2].Trim();

                            XbimSTEPInst inst = instances[instId] as XbimSTEPInst;

                            switch (target)
                            {
                                case ".U.":
                                    inst.Data.Set(2, XbimSTEPArg.Unknown);
                                    break;
                                case "$":
                                    inst.Data.Set(2, XbimSTEPArg.Nondef);
                                    break;
                                case ".T.":
                                case ".F.":
                                default:
                                    //warn
                                    break;
                            }

                        }
                    }
                }
            }

        }

        public void InitInverseCache(Dictionary<string, HashSet<string>> typesIncludeImportantInv)
        {
            HashSet<string> visitedRel_Arg = new HashSet<string>();
            foreach(string typeName in  typesIncludeImportantInv.Keys)
            {
                foreach(var argName in typesIncludeImportantInv[typeName])
                {
                    _initOneInverseRel(typeName, argName, visitedRel_Arg);
                }
            }
        }

        public IEnumerable<int> GetCachedInverseIDs(int entityId, string relArg)
        {
            var _inst = GetInstance(entityId) as XbimSTEPInst;

            string cacheKey = $"{_inst.TypeUpper}:{relArg.ToUpper()}";

            if (inverseCache.ContainsKey(cacheKey) && inverseCache[cacheKey].ContainsKey(_inst.Id))
            {
                return inverseCache[cacheKey][_inst.Id];
            }
            else
            {
                return Enumerable.Empty<int>();
            }
        }

        public ISTEPInst GetInstance(int id)
        {
            if(instances.ContainsKey(id))
            {
                return instances[id];
            }
            return null;
        }

        private void _initOneInverseRel(string typeName, string invArgName, HashSet<string> visitedRel_Arg)
        {

            var targetDef = _metadata.ExpressType(typeName.ToUpperInvariant());
            var invDef = targetDef.Inverses.First(t => t.Name == invArgName);

            Type relType = Utils_Xbim._unpack(invDef.PropertyInfo.PropertyType);
            string relTypeUpper = relType.Name.ToUpperInvariant();
            var relDef = _metadata.ExpressType(relTypeUpper);
            var relArgName = invDef.InverseAttributeProperty.RemoteProperty;

            string check_visited = $"{relTypeUpper}:{relArgName.ToUpperInvariant()}";

            if (visitedRel_Arg.Contains(check_visited))
                return;
            visitedRel_Arg.Add(check_visited);

            int relArgIndex = -1;
            ExpressMetaProperty relArgDef = null;

            foreach(var index in relDef.Properties.Keys)
            {
                var p_def = relDef.Properties[index];
                if (p_def.Name == relArgName)
                {
                    relArgIndex = index - 1; //HERE! in Xbim the index starts at 1
                    relArgDef = p_def;
                }
            }


            //instances

            if (cateMap.ContainsKey(relTypeUpper))
            {

                var relIds = cateMap[relTypeUpper];
                foreach (var relId in relIds)
                {
                    var relNode = instances[relId];

                    HashSet<int> targets = _getRefTargets(relNode, relArgIndex);

                    foreach (var target in targets)
                    {
                        var targetInst = instances[target];
                        var cacheKey = $"{targetInst.TypeUpper}:{invArgName.ToUpperInvariant()}";
                        if (!inverseCache.ContainsKey(cacheKey))
                        {
                            inverseCache[cacheKey] = new Dictionary<int, HashSet<int>>();
                        }
                        if (!inverseCache[cacheKey].ContainsKey(target))
                        {
                            inverseCache[cacheKey][target] = new HashSet<int>();
                        }
                        inverseCache[cacheKey][target].Add(relId);
                    }

                }
            }
        }

        private HashSet<int> _getRefTargets(ISTEPInst relNode, int argIndex)
        {
            var targets = relNode.Data.Get(argIndex);
            if (targets.Type == STEPType.COLL && targets is ISTEPColl list)
            {
                var output = new HashSet<int>();
                foreach (var item in list)
                {
                    if (item.Value is int)
                        output.Add((int)item.Value);
                    else
                        throw new InvalidDataException();
                }
                return output;
            }
            else if (targets.Type == STEPType.REF && targets.Value is int)
            {
                return new HashSet<int> { (int)targets.Value };
            }
            else if (targets.Type == STEPType.NONDEF)
            {
                //pass
                return new HashSet<int>();
            }
            else
                throw new InvalidDataException();
        }



        public IEnumerable<string> Schema_GetSubTypes(string entityType)
        {
            var def = _metadata.ExpressType(entityType.ToUpperInvariant());
            if (def != null)
            {
                return def.SubTypes.Select(t => t.Name);
            }
            throw new ArgumentException();
        }

        public List<ArgInfo> Schema_GetAllArgInfo(string entityType)
        {
            var def = _metadata.ExpressType(entityType.ToUpperInvariant());
            if (def != null)
            {
                List<ArgInfo> output = new List<ArgInfo>();
                foreach(var index in def.Properties.Keys)
                {
                    var arg = def.Properties[index];
                    output.Add(new ArgInfo
                    {
                        Index = index - 1, //Xbim arg index start with 1
                        ArgName = arg.Name,
                        IsCollection = arg.EntityAttribute.IsEnumerable,
                        CollectionType = arg.EntityAttribute.EntityType.ToString().ToUpperInvariant(),
                    });
                }
                
                return output;
            }
            throw new ArgumentException();
        }

    }
}