// Copyright (C) 2023  Liu, Han; School of Software, Tsinghua University
//
// This file is part of CBIMS.IFCNormalization.
// CBIMS.IFCNormalization is free software: you can redistribute it and/or modify it under the terms of the GNU Lesser General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// CBIMS.IFCNormalization is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU Lesser General Public License for more details.
// You should have received a copy of the GNU Lesser General Public License along with CBIMS.IFCNormalization. If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace CBIMS.IFCNormalization.Interface
{
    public enum STEPType : byte
    {
        INVALID = 0,

        COLL = 1,
        REF = 2,

        LOGICAL = 11,
        INT = 12,
        FLOAT = 13,

        STRING = 21,
        ENUM = 22,
        BINARY = 23,

        NONDEF = 31,
        OVERRIDE = 32,
    }
    public interface ISTEPArg
    {
        ISTEPArg Clone();
        string ToSTEPString();
        STEPType Type { get; }
        object Value { get; }
    }

}
