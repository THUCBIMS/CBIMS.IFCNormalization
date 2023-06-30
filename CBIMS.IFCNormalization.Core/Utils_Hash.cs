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
using System.Threading.Tasks;

namespace CBIMS.IFCNormalization.Core
{
    public static class Utils_Hash
    {

        public static int GetStableHashCode(string str)
        {
            unchecked
            {
                int hash = 5381;
                foreach (char c in str)
                {
                    hash = (hash << 5) + hash + c; // hash * 33 + c
                }
                return hash;
            }
        }

        public static int GetStableNonNegativeHashCode(string str)
        {
            unchecked
            {
                int hash = 5381;
                foreach (char c in str)
                {
                    hash = (hash << 5) + hash + c; // hash * 33 + c
                }
                return hash & 0x7FFFFFFF;
            }
        }

        public static int GetStableNonNegativeHashCode(byte[] bytes)
        {
            unchecked
            {
                int hash = 5381;
                foreach (byte c in bytes)
                {
                    hash = (hash << 5) + hash + c; // hash * 33 + c
                }
                return hash & 0x7FFFFFFF;
            }
        }

        public static string BinToBase64String(byte[] bytes)
        {
            return Convert.ToBase64String(bytes);
        }
    }
}
