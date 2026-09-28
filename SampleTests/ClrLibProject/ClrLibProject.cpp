#include "stdafx.h"

#include "ClrLibProject.h"

using namespace ClrDotNetLibProject;

namespace
{
	ref class AssemblyResolver abstract sealed
	{
	public:
		static System::String^ Directory;

		static System::Reflection::Assembly^ Resolve(System::Object^, System::ResolveEventArgs^ args)
		{
			System::String^ assemblyName = (gcnew System::Reflection::AssemblyName(args->Name))->Name;
			System::String^ candidate = System::IO::Path::Combine(Directory, assemblyName + ".dll");
			return System::IO::File::Exists(candidate) ? System::Reflection::Assembly::LoadFrom(candidate) : nullptr;
		}
	};
}

void ClrLibProject::ResolveManagedAssembliesFrom(const wchar_t* directory)
{
	AssemblyResolver::Directory = gcnew System::String(directory);
	System::AppDomain::CurrentDomain->AssemblyResolve += gcnew System::ResolveEventHandler(&AssemblyResolver::Resolve);
}

int ClrLibProject::ClrClass::Add(int a, int b)
{
	DotNetClass^ instance = gcnew DotNetClass();
	return instance->Add(a, b);
}
