// Copyright (C) 2023  Liu, Han; School of Software, Tsinghua University
//
// This file is part of CBIMS.IFCNormalization.
// CBIMS.IFCNormalization is free software: you can redistribute it and/or modify it under the terms of the GNU Lesser General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// CBIMS.IFCNormalization is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU Lesser General Public License for more details.
// You should have received a copy of the GNU Lesser General Public License along with CBIMS.IFCNormalization. If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CBIMS.IFCNormalization.Interface
{
    public interface ISTEPDoc
    {
        string GetHeaderString();

        ISTEPInst GetInstance(int id);

        ICollection<int> Ids { get; }

        void InitInverseCache(Dictionary<string, HashSet<string>> typesIncludeImportantInv);
        IEnumerable<int> GetCachedInverseIDs(int entityId, string relArg);


        //SCHEMA ISSUES
        IEnumerable<string> Schema_EntityTypes { get; }
        IEnumerable<string> Schema_GetSubTypes(string entityType);
        
        List<ArgInfo> Schema_GetAllArgInfo(string entityType);

    }

    public class ArgInfo
    {
        public int Index;
        public string ArgName;
        public bool IsCollection;
        public string CollectionType;
        //public bool IsOwn;
        //public bool IsOverrideHidden;
    }
}
