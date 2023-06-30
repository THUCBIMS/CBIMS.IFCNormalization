// Copyright (C) 2023  Liu, Han; School of Software, Tsinghua University
//
// This file is part of CBIMS.IFCNormalization.
// CBIMS.IFCNormalization is free software: you can redistribute it and/or modify it under the terms of the GNU Lesser General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// CBIMS.IFCNormalization is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU Lesser General Public License for more details.
// You should have received a copy of the GNU Lesser General Public License along with CBIMS.IFCNormalization. If not, see <https://www.gnu.org/licenses/>.

using CBIMS.IFCNormalization.Interface;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CBIMS.IFCNormalization.Xbim
{
    internal class XbimSTEPColl : List<ISTEPArg>, ISTEPColl
    {
        public string Name { get; internal set; }

        public STEPType Type => STEPType.COLL;

        public object Value => this;

        public ISTEPArg Clone()
        {
            XbimSTEPColl new_list = new XbimSTEPColl();
            foreach (var item in this)
            {
                new_list.Add(item.Clone());
            }
            return new_list;
        }

        public ISTEPArg Get(int i)
        {
            if(this.Count > i)
                return this[i];
            return null;
        }

        public STEPType GetArgType(int i)
        {
            ISTEPArg arg = Get(i);
            if(arg == null)
                return STEPType.INVALID;
            return arg.Type;
        }

        public byte[] GetBinary(int i)
        {
            ISTEPArg arg = Get(i);
            if (arg == null)
                return null;
            if (arg.Type == STEPType.BINARY)
                return (byte[])arg.Value;
            throw new InvalidCastException();
        }

        public bool? GetLogical(int i)
        {
            ISTEPArg arg = Get(i);
            if (arg == null)
                return null;
            if (arg.Type == STEPType.LOGICAL)
            {
                if ((char)arg.Value == 'T')
                    return true;
                if ((char)arg.Value == 'F')
                    return false;
                if ((char)arg.Value == 'U')
                    return null;
            }
            throw new InvalidCastException();
        }

        public string GetEnum(int i)
        {
            ISTEPArg arg = Get(i);
            if (arg == null)
                return null;
            if (arg.Type == STEPType.ENUM)
                return arg.Value as string;
            throw new InvalidCastException();
        }

        public double? GetFloat(int i)
        {
            ISTEPArg arg = Get(i);
            if (arg == null)
                return null;
            if (arg.Type == STEPType.FLOAT)
                return (double)arg.Value;
            throw new InvalidCastException();
        }

        public int? GetInt(int i)
        {
            ISTEPArg arg = Get(i);
            if (arg == null)
                return null;
            if (arg.Type == STEPType.INT)
                return (int)arg.Value;
            throw new InvalidCastException();
        }

        public ISTEPColl GetList(int i)
        {
            ISTEPArg arg = Get(i);
            if (arg == null)
                return null;
            if (arg.Type == STEPType.COLL)
                return arg.Value as XbimSTEPColl;
            throw new InvalidCastException();
        }

        public int? GetRef(int i)
        {
            ISTEPArg arg = Get(i);
            if (arg == null)
                return null;
            if (arg.Type == STEPType.REF)
                return (int)arg.Value;
            throw new InvalidCastException();
        }

        public string GetString(int i)
        {
            ISTEPArg arg = Get(i);
            if (arg == null)
                return null;
            if (arg.Type == STEPType.STRING)
                return arg.Value as string;
            throw new InvalidCastException();
        }

        public void Set(int i, ISTEPArg v)
        {
            if(this.Count <= i)
            {
                while (this.Count < i)
                {
                    this.Add(XbimSTEPArg.Nondef);
                }
                this.Add(v);
            }
            else
            {
                this[i] = v;
            }
            
        }

        public void SetBinary(int i, byte[] v)
        {
            Set(i, new XbimSTEPArg(STEPType.BINARY, v));
        }

        public void SetLogical(int i, bool? v)
        {
            if(v.HasValue)
            {
                if (v.Value)
                {
                    Set(i, XbimSTEPArg.True);
                }
                else
                {
                    Set(i, XbimSTEPArg.False);
                }
            }
            else
            {
                Set(i, XbimSTEPArg.Unknown);
            }
            
        }

        public void SetEnum(int i, string v)
        {
            Set(i, new XbimSTEPArg(STEPType.ENUM, v));
        }

        public void SetFloat(int i, double v)
        {
            Set(i, new XbimSTEPArg(STEPType.FLOAT, v));
        }

        public void SetInt(int i, int v)
        {
            Set(i, new XbimSTEPArg(STEPType.INT, v));
        }

        public void SetList(int i, ISTEPColl v)
        {
            Set(i, new XbimSTEPArg(STEPType.COLL, v));
        }

        public void SetNondef(int i)
        {
            Set(i, XbimSTEPArg.Nondef);
        }

        public void SetOverride(int i)
        {
            Set(i, XbimSTEPArg.Overide);
        }

        public void SetRef(int i, int v)
        {
            Set(i, new XbimSTEPArg(STEPType.REF, v));
        }

        public void SetString(int i, string v)
        {
            Set(i, new XbimSTEPArg(STEPType.STRING, v));
        }

        public string ToSTEPString()
        {
            StringBuilder sb = new StringBuilder();
            if (!string.IsNullOrEmpty(Name))
            {
                sb.Append(Name);
            }
            sb.Append('(');
            bool first = true;
            foreach(var item in this)
            {
                if (first)
                {
                    first = false;
                }
                else
                {
                    sb.Append(',');
                }
                sb.Append(item.ToSTEPString());
            }
            sb.Append(')');
            return sb.ToString();
        }

    }
}
