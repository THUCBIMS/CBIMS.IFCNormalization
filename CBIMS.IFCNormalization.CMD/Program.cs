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
using System.Reflection;
using System.Text;
using CBIMS.IFCNormalization.Xbim;

namespace CBIMS.IFCNormalization.CMD
{
    internal class Program
    {

        private const string HELP_INFO_SUFFIX =
            "-i <input path> \r\n" +
            "\t[-o <output path>] \r\n" +
            "\t[--level <chunk level>] \r\n" +
            "\t[--spare <chunk spare rate>] \r\n" +
            "\t[--parallel <true|false>] \r\n" +
            "\t[--exp_chunk_num <true|false>]\r\n" +
            "\t[--rm_ownerhistory <true|false>]\r\n" +
            "\t[--do_segment <true|false>]\r\n" +
            "\r\n" +
            "-i:\r\n" +
            "\tAn input IFC path." +
            "\r\n" +
            "-o:\r\n" +
            "\t<default inputPath/inputFileName.norm.ifc> The output IFC path." +
            "\r\n" +
            "--level:\r\n" +
            "\t<default 5> The level of chunk size." +
            "\t\tLevel\tChunk size\tMax chunk numbers\r\n" +
            "\t\t3\t10000000\t214\r\n" +
            "\t\t4\t1000000\t\t2147\r\n" +
            "\t\t5\t100000\t\t21474\r\n" +
            "\t\t6\t10000\t\t214748\r\n" +
            "\t\t7\t1000\t\t2147483\r\n" +
            "\t\t8\t100\t\t21474836\r\n" +
            "\t\t9\t10\t\t214748364\r\n" +
            "\r\n" +
            "--spare:\r\n" +
            "\t<default 2.0> A real spare rate greater than 1.0 to make more efficient assignment of node storage." +
            "\r\n" +
            "--parallel:\r\n" +
            "\t<default true> Use multi-core CPU to speed up calculation." +
            "\r\n" +
            "--exp_chunk_num:\r\n" +
            "\t<default true> Using exponential function for number of chunks of each type." +
            "\r\n" +
            "--rm_ownerhistory:\r\n" +
            "\t<default true> Removing IfcOwnerHistory references for all IfcRoot nodes on output." +
            "\r\n" +
            "--do_segment:\r\n" +
            "\t<default false> Adding \"/*========*/\" as segmentation for each chunk." +
            "\r\n" +
            "";

        static void Main(string[] args)
        {
            Console.WriteLine();
            Console.WriteLine(Assembly.GetExecutingAssembly().GetName().Name);
            Console.WriteLine("\tCore Version: " + Assembly.GetAssembly(typeof(RunNormalization)).GetName().Version.ToString());

            var loader_type = typeof(XbimSTEPDoc);

            var loader_assemblyName = Assembly.GetAssembly(loader_type).GetName();
            Console.WriteLine("\tLoader Version: " + loader_assemblyName.Name + " " + loader_assemblyName.Version.ToString());


            string ifcPath = null;
            string outPath = null;

            int chunkLevel = 5;
            double chunkSpareRate = 2.0;
            
            bool doPara = true;
            bool useExpChunkNum = true;
            bool removeOwnerHistory = true;
            bool doSegment = false;
            

            for (int i = 0; i < args.Length; i++)
            {
                var op = args[i];
                string val = "";
                if (i < args.Length - 1)
                {
                    val = args[i + 1];
                }
                
                switch (op)
                {
                    case "-h":
                        _WriteHelp();
                        return;
                    case "-i":
                        ifcPath = val;
                        i++;
                        break;
                    case "-o":
                        outPath = val;
                        i++;
                        break;
                    case "--level":
                        if (int.TryParse(val, out int _level))
                        {
                            if (_level >= 3 && _level <= 9)
                                chunkLevel = _level;
                        }
                        i++;
                        break;
                    case "--spare":
                        if (double.TryParse(val, out double _chunkSpareRate))
                        {
                            if (_chunkSpareRate >= 1)
                                chunkSpareRate = _chunkSpareRate;
                        }
                        i++;
                        break;
                    case "--parallel":
                        if (val.ToLower() == "true")
                            doPara = true;
                        if (val.ToLower() == "false")
                            doPara = false;
                        i++;
                        break;
                    case "--exp_chunk_num":
                        if (val.ToLower() == "true")
                            useExpChunkNum = true;
                        if (val.ToLower() == "false")
                            useExpChunkNum = false;
                        i++;
                        break;
                    case "--rm_ownerhistory":
                        if (val.ToLower() == "true")
                            removeOwnerHistory = true;
                        if (val.ToLower() == "false")
                            removeOwnerHistory = false;
                        i++;
                        break;
                    case "--do_segment":
                        if (val.ToLower() == "true")
                            doSegment = true;
                        if (val.ToLower() == "false")
                            doSegment = false;
                        i++;
                        break;
                    default:
                        Console.WriteLine("ERR: UNKNOWN ARGUMENT");
                        _WriteHelp();
                        return;
                }
            }

            if(string.IsNullOrWhiteSpace(ifcPath))
            {
                Console.WriteLine("ERR: NULL INPUT PATH");
                _WriteHelp();
                return;
            }

            ifcPath = _fixPath(ifcPath);

            if (!File.Exists(ifcPath))
            {
                Console.WriteLine("ERR: FILE NOT EXISTS");
                return;
            }

            if (string.IsNullOrWhiteSpace(outPath))
            {
                if (ifcPath.EndsWith(".ifc"))
                    outPath = ifcPath.Substring(0, ifcPath.Length - 4) + ".norm.ifc";
                else
                    outPath = ifcPath + ".norm.ifc";
            }

            outPath = _fixPath(outPath);


            DateTime start = DateTime.Now;

            XbimSTEPDoc model = new XbimSTEPDoc(ifcPath);

            DateTime end_load = DateTime.Now;

            Console.WriteLine("Time Load: " + (end_load - start).TotalSeconds);


            RunNormalization.Do(model, outPath, chunkLevel, chunkSpareRate, 
                doPara, useExpChunkNum, removeOwnerHistory, doSegment);

            Console.WriteLine("END");

        }


        private static void _WriteHelp()
        {
            string fileName = Assembly.GetEntryAssembly().Location;
            fileName = Path.GetFileName(fileName);
            fileName = fileName.Replace(".dll", ".exe");

            Console.WriteLine();
            Console.WriteLine("Help of " + Assembly.GetExecutingAssembly().GetName().Name);
            Console.WriteLine();
            Console.WriteLine("Usage:");
            Console.WriteLine($"\t{fileName} {HELP_INFO_SUFFIX}");
        }


        private static string _fixPath(string path)
        {
            while (path.StartsWith("\'") || path.StartsWith("\""))
            {
                path = path.Substring(1);
            }
            while (path.EndsWith("\'") || path.EndsWith("\""))
            {
                path = path.Substring(0, path.Length - 1);
            }
            return path;
        }
    }
}
