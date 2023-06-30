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
using System.Data.SqlTypes;
using System.Linq;
using System.Text;
using Xbim.Common;

namespace CBIMS.IFCNormalization.Xbim
{
    internal static class Utils_Xbim
    {
        internal static string Encode(string str)
        {
            if (str == null)
            {
                return "";
            }

            StringBuilder sb = new StringBuilder();
            List<char> charArray = new List<char>();

            foreach (char ch in str)
            {
                bool within_spf_range = ch >= 0x20 && ch <= 0x7e && ch != '\'' && ch != '\\';
                if (within_spf_range)
                {
                    if (charArray.Count > 0)
                    {
                        byte[] bytes = Encoding.Unicode.GetBytes(charArray.ToArray());
                        for (int j = 0; j < bytes.Length; j += 2)
                        {
                            sb.AppendFormat("{0}{1}", bytes[j + 1].ToString("x").PadLeft(2, '0').ToUpper(), bytes[j].ToString("x").PadLeft(2, '0').ToUpper());
                        }
                        sb.Append("\\X0\\");
                        charArray.Clear();
                    }
                    sb.Append(ch);
                }
                else
                {
                    if (charArray.Count == 0)
                    {
                        sb.Append("\\X2\\");
                    }
                    charArray.Add(ch);
                }
            }

            if (charArray.Count > 0)
            {
                byte[] bytes = Encoding.Unicode.GetBytes(charArray.ToArray());
                for (int j = 0; j < bytes.Length; j += 2)
                {
                    sb.AppendFormat("{0}{1}", bytes[j + 1].ToString("x").PadLeft(2, '0').ToUpper(), bytes[j].ToString("x").PadLeft(2, '0').ToUpper());
                }
                sb.Append("\\X0\\");
                charArray.Clear();
            }
            return sb.ToString();
        }

        internal static Type _unpack(Type type)
        {
            while (type.IsGenericType)
            {
                type = type.GetGenericArguments().First();
            }
            return type;
        }


        internal static ISTEPArg _pack(string vType, XbimSTEPArg arg, bool as_select)
        {
            if (as_select)
            {
                var list = new XbimSTEPColl() { arg };
                list.Name = vType;
                return list;
            }
            else
            {
                return arg;
            }

        }


        internal static ISTEPArg _parse(object value, string required_valTypeUpper)
        {
            if (value == null)
            {
                return XbimSTEPArg.Nondef;
            }

            if (value is IEnumerable es && !(value is string) && !(value is IPersistEntity))
            {
                var coll = new XbimSTEPColl();
                foreach (var e in es)
                {
                    coll.Add(_parse(e, required_valTypeUpper));
                }
                if (coll.Any())
                    return coll;
                return XbimSTEPArg.Nondef;
            }
            else if (value is IPersistEntity ent)
            {
                return new XbimSTEPArg(STEPType.REF, ent.EntityLabel);
            }
            else if (value is IExpressValueType val)
            {
                var vType = val.GetType();

                vType = _unpack(vType);

                string vTypeUpper = vType.Name.ToUpperInvariant();
                object raw = val.Value;

                bool as_select = required_valTypeUpper != vTypeUpper;

                if (vTypeUpper == "IFCLOGICAL" || vTypeUpper == "IFCBOOLEAN")
                {
                    if (raw == null)
                    {
                        return _pack(vTypeUpper, XbimSTEPArg.Unknown, as_select);
                    }
                    else
                    {
                        if ((bool)raw)
                        {
                            return _pack(vTypeUpper, XbimSTEPArg.True, as_select);
                        }
                        else
                        {
                            return _pack(vTypeUpper, XbimSTEPArg.False, as_select);
                        }
                    }
                }
                else if (raw == null)
                {
                    return XbimSTEPArg.Nondef;
                }
                else if (raw is int intval)
                {
                    return _pack(vTypeUpper, new XbimSTEPArg(STEPType.INT, intval), as_select);
                }
                else if (raw is long longval)
                {
                    return _pack(vTypeUpper, new XbimSTEPArg(STEPType.INT, longval), as_select);
                }
                else if (raw is double dblval)
                {
                    return _pack(vTypeUpper, new XbimSTEPArg(STEPType.FLOAT, dblval), as_select);
                }
                else if (raw is string strval)
                {
                    return _pack(vTypeUpper, new XbimSTEPArg(STEPType.STRING, strval), as_select);
                }
                else if (raw is byte[] binval)
                {
                    return _pack(vTypeUpper, new XbimSTEPArg(STEPType.BINARY, binval), as_select);
                }
                else if(raw is IEnumerable raw_es)
                {
                    var coll = new XbimSTEPColl();
                    foreach (var e in raw_es)
                    {
                        coll.Add(_parse(e, required_valTypeUpper));
                    }
                    return coll;
                }
                else
                {
                    var rawType = raw.GetType();
                    throw new NotSupportedException("IExpressValueType " + rawType.Name);
                }
            }
            else if (value is int intval)
            {
                return new XbimSTEPArg(STEPType.INT, intval);
            }
            else if (value is long longval)
            {
                //TODO: HERE dealing with long val
                return new XbimSTEPArg(STEPType.INT, longval);
            }
            else if (value is double dblval)
            {
                return new XbimSTEPArg(STEPType.FLOAT, dblval);
            }
            else if (value is string strval)
            {
                return new XbimSTEPArg(STEPType.STRING, strval);
            }
            else if (value is bool boolval)
            {
                return new XbimSTEPArg(STEPType.LOGICAL, boolval);
            }
            else if (value is Enum _enum)
            {
                return new XbimSTEPArg(STEPType.ENUM, _enum.ToString());
            }
            else
            {
                var vType = value.GetType();
                throw new NotSupportedException(vType.Name);
            }
        }

    }

}
