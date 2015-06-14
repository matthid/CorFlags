﻿using System;
using System.IO;
using System.Linq;
using Mono.Cecil;

namespace CorFlags
{

	enum ExitCodes : int
	{
		Success = 0,
		Error = 1
	}

	public class CorFlagsInformation {
		public string assemblyVersion;
		public string version;
		public TargetRuntime clr_header;
		public string pe;
		public int corflags;
		public bool ilonly;
		public bool x32bitreq;
		public bool x32bitpref;
		public bool signed;
	}

	public class AssemblyInfo 
	{
		readonly TextWriter output;

		public AssemblyInfo (TextWriter errorOutput)
			: this (errorOutput, Console.Out)
		{
		}

		public AssemblyInfo (TextWriter errorOutput, TextWriter messagesOutput)
		{
			this.output = messagesOutput;
		}

		bool IsMarked(ModuleDefinition targetModule)
		{
			return targetModule.Types.Any(t => t.Name == "IMarked");
		}

		public void AssemblyInfoOutput (CorFlagsInformation info) 
		{
			output.WriteLine ("Version   : {0}", info.version);
			// CLR Header: 2.0 indicates a .Net 1.0 or .Net 1.1 (Everett) image 
			//		 while 2.5 indicates a .Net 2.0 (Whidbey) image.
			output.WriteLine ("CLR Header: {0}", (info.clr_header <= TargetRuntime.Net_1_1) ? "2.0" : "2.5");
			output.WriteLine ("PE        : {0}", info.pe);
			output.WriteLine ("CorFlags  : 0x{0:X}", info.corflags);
			output.WriteLine ("ILONLY    : {0}", info.ilonly ? 1 : 0);
			output.WriteLine ("32BITREQ  : {0}", info.x32bitreq ? 1 : 0);
			output.WriteLine ("32BITPREF : {0}", info.x32bitpref ? 1 : 0);
			output.WriteLine ("Signed    : {0}", info.signed ? 1 : 0);
			//	anycpu: PE = PE32  and  32BIT = 0
			//	   x86: PE = PE32  and  32BIT = 1
			//	64-bit: PE = PE32+ and  32BIT = 0
		}

		public CorFlagsInformation ExtractInfo (ModuleDefinition assembly)
		{
			var info = new CorFlagsInformation ();

			// The user defined version of the assembly
			info.assemblyVersion = assembly.Assembly.Name.Version.ToString ();

			//Version of the mscorlib.dll that was assembly was compiled with.
			info.version = assembly.RuntimeVersion;

			info.clr_header = assembly.Runtime;

			info.pe = (assembly.Architecture == TargetArchitecture.AMD64 || assembly.Architecture == TargetArchitecture.IA64) ? "PE32+" : "PE32";

			info.corflags = (int)assembly.Attributes;

			info.ilonly = (assembly.Attributes & ModuleAttributes.ILOnly) == ModuleAttributes.ILOnly;

			info.x32bitreq = (assembly.Attributes & ModuleAttributes.Required32Bit) == ModuleAttributes.Required32Bit;
			//Console.WriteLine (assembly.Attributes);

			info.x32bitpref = (assembly.Attributes & ModuleAttributes.Preferred32Bit) == ModuleAttributes.Preferred32Bit;

			info.signed = (assembly.Attributes & ModuleAttributes.StrongNameSigned) == ModuleAttributes.StrongNameSigned;

			return info;
		}

		public ModuleDefinition OpenAssembly (CompilerSettings setting,  string aFileName, Report report) {
			var dataPath = Path.GetDirectoryName (GetType ().Assembly.Location);

			var sampleAssemblyPath = Path.Combine (dataPath, aFileName);

			if (!File.Exists (sampleAssemblyPath)) {
				throw new FileNotFoundException();
			}

			var targetModule = ModuleDefinition.ReadModule (sampleAssemblyPath);
			if (!IsMarked (targetModule)) {
				// Console.WriteLine("isMarked?");
			}
			return targetModule;
		}
	}

	class MainClass
	{
		public static void Main (string[] args)
		{
			var output = Console.Out;

			var report = new Report (output);
			var cmd = new CommandLineParser (output);
			var cmdArguments = cmd.ParseArguments (args);

			if (cmdArguments.ArgList.Count == 0 && cmdArguments.SourceFiles.Count == 0) {
				// MS's CorFlags displays help whe no args are supplied and exits success
				cmd.Header ();
				cmd.Usage ();
				Environment.Exit ((int)ExitCodes.Success);
			} else if (cmdArguments.SourceFiles.Count > 0) {
				var assemblyInfo = new AssemblyInfo (output);
				foreach (var assemblyFileName in cmdArguments.SourceFiles) {
					if (!cmdArguments.NoLogo)
						cmd.Header ();
					ModuleDefinition modDef;
					try {
						modDef = assemblyInfo.OpenAssembly (cmdArguments, assemblyFileName, report);
						if (modDef == null) {
							report.Error (998, "Unknown error with no exception opening: {0}", assemblyFileName);
							Environment.Exit ((int)ExitCodes.Error);
						} else {
							var corFlags = assemblyInfo.ExtractInfo (modDef);
							if (cmdArguments.InfoOnly) {
								assemblyInfo.AssemblyInfoOutput (corFlags);
							} else {
								//TODO: Changing flags and saving assembly not implemented yet
								throw new NotImplementedException ();
							}
							Console.WriteLine ();
						}
					} catch (FileNotFoundException) {
						// corflags : error CF002 : Could not open file for reading
						report.Error (2, "{0}", "Could not open file for reading");
						Environment.Exit ((int)ExitCodes.Error);
					} catch (BadImageFormatException) {
						// System.BadImageFormatException: Format of the executable (.exe) or library (.dll) is invalid.
						// corflags : error CF008 : The specified file does not have a valid managed header
						report.Error (8, "{0}", "The specified file does not have a valid managed header");
						Environment.Exit ((int)ExitCodes.Error);
					} catch (Exception e) {
						report.Error (999, "Unknown exception: {0}", e.Message);
						Environment.Exit ((int)ExitCodes.Error);
					}
				}
				Environment.Exit ((int)ExitCodes.Success);
			}
			Environment.Exit ((int)ExitCodes.Error);
		}
	}
}

//.\CorFlags.exe .\perftest-32-release.exe
//Microsoft (R) .NET Framework CorFlags Conversion Tool.  Version  4.0.30319.17929
//Copyright (c) Microsoft Corporation.  All rights reserved.
//
//Version   : v4.0.30319
//CLR Header: 2.5
//PE        : PE32
//CorFlags  : 0x3
//ILONLY    : 1
//32BITREQ  : 1
//32BITPREF : 0
//Signed    : 0
//
//.\CorFlags.exe .\perftest-64-release.exe
//Microsoft (R) .NET Framework CorFlags Conversion Tool.  Version  4.0.30319.17929
//Copyright (c) Microsoft Corporation.  All rights reserved.
//
//Version   : v4.0.30319
//CLR Header: 2.5
//PE        : PE32+
//CorFlags  : 0x1
//ILONLY    : 1
//32BITREQ  : 0
//32BITPREF : 0
//Signed    : 0
//
//These combine to specify the assembly types. Here is how they would look like for:
//
//	anycpu: PE = PE32    and  32BIT = 0
//		x86:      PE = PE32    and  32BIT = 1
//		64-bit:  PE = PE32+  and  32BIT = 0