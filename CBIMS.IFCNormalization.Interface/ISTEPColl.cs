// Copyright (C) 2023  Liu, Han; School of Software, Tsinghua University
//
// This file is part of CBIMS.IFCNormalization.
// CBIMS.IFCNormalization is free software: you can redistribute it and/or modify it under the terms of the GNU Lesser General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// CBIMS.IFCNormalization is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU Lesser General Public License for more details.
// You should have received a copy of the GNU Lesser General Public License along with CBIMS.IFCNormalization. If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections;
using System.Collections.Generic;

namespace CBIMS.IFCNormalization.Interface
{
   
    public interface ISTEPColl : ISTEPArg, IList<ISTEPArg>
    {
        string Name { get; }
        STEPType GetArgType(int i);

        void Set(int i, ISTEPArg v);
        ISTEPArg Get(int i);

        void SetInt(int i, int v);
        int? GetInt(int i);

        void SetFloat(int i, double v);
        double? GetFloat(int i);

        void SetString(int i, string v);
        string GetString(int i);

        void SetLogical(int i, bool? v);
        bool? GetLogical(int i);

        void SetBinary(int i, byte[] v);
        byte[] GetBinary(int i);

        void SetEnum(int i, string v);
        string GetEnum(int i);

        void SetRef(int i, int v);
        int? GetRef(int i);

        void SetList(int i, ISTEPColl v);
        ISTEPColl GetList(int i);

        void SetNondef(int i);
        void SetOverride(int i);

        void Sort(Comparison<ISTEPArg> comparison);
    }
}
