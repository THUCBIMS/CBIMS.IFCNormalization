// Copyright (C) 2023  Liu, Han; School of Software, Tsinghua University
//
// This file is part of CBIMS.IFCNormalization.
// CBIMS.IFCNormalization is free software: you can redistribute it and/or modify it under the terms of the GNU Lesser General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// CBIMS.IFCNormalization is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU Lesser General Public License for more details.
// You should have received a copy of the GNU Lesser General Public License along with CBIMS.IFCNormalization. If not, see <https://www.gnu.org/licenses/>.

using CBIMS.IFCNormalization.Interface;
using System;
using System.Collections;
using System.Linq;
using Xbim.Common;
using Xbim.Ifc2x3.MeasureResource;

namespace CBIMS.IFCNormalization.Xbim
{
    internal class XbimSTEPInst : ISTEPInst
    {
        internal IPersistEntity _entity;
        XbimSTEPColl _data;

        public int Id => _entity.EntityLabel;

        public string TypeUpper => _entity.ExpressType.ExpressNameUpper;

        public ISTEPColl Data { get => _data; }

        internal XbimSTEPInst(IPersistEntity entity) 
        {
            this._entity = entity;

            _initData();
        }

        private void _initData()
        {
            _data = new XbimSTEPColl();
            _data.Name = TypeUpper;
            foreach(var pair in _entity.ExpressType.Properties)
            {

                


                var index = pair.Key;
                var i = index - 1;
                var expressMetaProp = pair.Value;

                if (expressMetaProp.EntityAttribute.State == EntityAttributeState.DerivedOverride)
                {
                    _data.SetOverride(i);
                    continue;
                }


                var propinfo = expressMetaProp.PropertyInfo;
                if (propinfo != null)
                {
                    var value = propinfo.GetValue(_entity);

                    var required_valType = expressMetaProp.PropertyInfo.PropertyType;
                    required_valType = Utils_Xbim._unpack(required_valType);

                    string required_valTypeUpper = required_valType.Name.ToUpperInvariant();

                    ISTEPArg argument = null;
                    try
                    {
                        argument = Utils_Xbim._parse(value, required_valTypeUpper);
                    }
                    catch(Exception ex)
                    {
                        Console.WriteLine($"err 038: {_data.Name}:{propinfo.Name} - " + ex.Message);
                    }

                    if (argument != null)
                    {
                        _data.Set(i, argument);
                    }
                    else
                    {
                        _data.SetNondef(i);
                    }

                }
            }
            
        }

        
    }
}
