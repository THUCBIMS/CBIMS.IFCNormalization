// Copyright (C) 2023  Liu, Han; School of Software, Tsinghua University
//
// This file is part of CBIMS.IFCNormalization.
// CBIMS.IFCNormalization is free software: you can redistribute it and/or modify it under the terms of the GNU Lesser General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// CBIMS.IFCNormalization is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU Lesser General Public License for more details.
// You should have received a copy of the GNU Lesser General Public License along with CBIMS.IFCNormalization. If not, see <https://www.gnu.org/licenses/>.

using CBIMS.IFCNormalization.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CBIMS.IFCNormalization.Xbim
{
    internal class XbimSTEPArg : ISTEPArg
    {
        public static readonly XbimSTEPArg Nondef 
            = new XbimSTEPArg(STEPType.NONDEF, '$');
        public static readonly XbimSTEPArg Overide 
            = new XbimSTEPArg(STEPType.OVERRIDE, '*');
        public static readonly XbimSTEPArg True
            = new XbimSTEPArg(STEPType.LOGICAL, true);
        public static readonly XbimSTEPArg False
            = new XbimSTEPArg(STEPType.LOGICAL, false);
        public static readonly XbimSTEPArg Unknown
            = new XbimSTEPArg(STEPType.LOGICAL, null);

        //public XbimSTEPArgument()
        //{
        //}
        public XbimSTEPArg(STEPType STEPType, object value)
        {
            this.Type = STEPType;
            this.Value = value;
        }
        public STEPType Type { get; private set; }

        public object Value { get; internal set; }

        public ISTEPArg Clone()
        {
            return new XbimSTEPArg(Type, Value);
        }

        public string ToSTEPString()
        {
            if(Type == STEPType.NONDEF)
                return "$";
            else if(Type == STEPType.OVERRIDE)
                return "*";
            else if(Type == STEPType.COLL)
            {
                return (Value as XbimSTEPColl).ToSTEPString();
            }
            else if (Type == STEPType.LOGICAL)
            {
                if (Value == null)
                    return ".U.";
                if ((bool)Value)
                    return ".T.";
                return ".F.";
            }
            else if (Type == STEPType.REF)
            {
                return $"#{Value}";
            }
            else if (Type == STEPType.INT)
            {

                return $"{Value}";
            }
            else if (Type == STEPType.FLOAT)
            {
                return $"{_format((double)Value)}";
            }
            else if(Type == STEPType.STRING)
            {
                return $"'{Utils_Xbim.Encode(Value as string)}'";
            }
            else if (Type == STEPType.ENUM)
            {
                return $".{Value as string}.";
            }
            else if (Type == STEPType.BINARY)
            {
                throw new NotImplementedException();
            }
            else 
            {
                throw new InvalidOperationException();
            }
            
        }

        private object _format(double value)
        {
            var str = value.ToString();

            if (value % 1 == 0) //is int
            {
                if (!str.Contains('E'))
                {
                    return str + ".";
                }
                else if (!str.Contains('.'))
                {
                    return str.Replace("E", ".0E");
                }
            }
            else if (str.Contains('E') && !str.Contains('.')) // 1E-05 -> 1.0E-05
            {
                return str.Replace("E", ".0E");
            }
            return str;
        }

    }
}
