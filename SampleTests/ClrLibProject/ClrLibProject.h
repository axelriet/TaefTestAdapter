#pragma once

// Native interface of the C++/CLI library (usable from code compiled without /clr)
namespace ClrLibProject {
	class ClrClass
	{
	public:
		int Add(int a, int b);
	};

	// Makes the CLR load referenced assemblies (ClrDotNetLibProject.dll) from the given directory. Required if the
	// process' application base is not the folder of the assemblies (e.g. in TAEF's TE.ProcessHost.exe).
	void ResolveManagedAssembliesFrom(const wchar_t* directory);
}
