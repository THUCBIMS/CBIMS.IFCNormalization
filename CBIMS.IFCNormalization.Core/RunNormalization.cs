// Copyright (C) 2023  Liu, Han; School of Software, Tsinghua University
//
// This file is part of CBIMS.IFCNormalization.
// CBIMS.IFCNormalization is free software: you can redistribute it and/or modify it under the terms of the GNU Lesser General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// CBIMS.IFCNormalization is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU Lesser General Public License for more details.
// You should have received a copy of the GNU Lesser General Public License along with CBIMS.IFCNormalization. If not, see <https://www.gnu.org/licenses/>.

using CBIMS.IFCNormalization.Interface;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace CBIMS.IFCNormalization
{
    public static class RunNormalization
    {
        public static void Do(ISTEPDoc model, string outPath,
            int chunkLevel, double chunkSpareRate,
            bool doPara, bool useExpChunkNum, bool removeOwnerHistory, 
            bool doSegment)
        {

            DateTime start = DateTime.Now;

            ChunkDispatch cp = new ChunkDispatch(model, chunkLevel, chunkSpareRate, 
                doPara, doSegment);
            cp.UseExpChunkNum = useExpChunkNum;
            cp.RemoveOwnerHistoryOnOutput = removeOwnerHistory;
            cp.Init();

            DateTime end_init = DateTime.Now;
            Console.WriteLine("\tTime Init: " + (end_init - start).TotalSeconds);

            cp.HashCal();

            DateTime end_hash = DateTime.Now;
            Console.WriteLine("\tTime Hash: " + (end_hash - end_init).TotalSeconds);
            //Console.WriteLine("Count Hash: " + cp.Count_Hash);

            cp.InitPrefixSpaces();
            cp.Dispatch();

            DateTime end_dispatch = DateTime.Now;
            Console.WriteLine("\tTime Dispatch: " + (end_dispatch - end_hash).TotalSeconds);

            string output = cp.AssembleResult();
            DateTime end_assemble = DateTime.Now;
            Console.WriteLine("\tTime AssembleResult: " + (end_assemble - end_dispatch).TotalSeconds);

            Console.WriteLine("Time Normalization: " + (end_assemble - start).TotalSeconds);


            if (outPath != null)
            {
                Console.WriteLine("Write To: " + outPath);
                using (StreamWriter writer = new StreamWriter(outPath))
                {
                    writer.Write(output);
                }
                
            }

            DateTime end_write = DateTime.Now;
            Console.WriteLine("Time Write: " + (end_write - end_assemble).TotalSeconds);

        }
    }
}
