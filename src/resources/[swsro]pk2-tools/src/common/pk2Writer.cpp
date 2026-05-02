#ifndef PK2WRITER_H_
	#include "pk2writer.h"
#endif

#ifndef _INC_STDIO
	#include <stdio.h>
#endif

//-----------------------------------------------------------------------------

// Start 1 byte alignment
#pragma pack(push, 1)

	// Structure to the gfx dll object, not going to try and decode it all
	struct tGFXAccess
	{
		DWORD addr1;
		DWORD addr2;
		unsigned char buffer[5930];
	};

// End 1 byte alignment
#pragma pack(pop)

//-----------------------------------------------------------------------------

// Pk2 success function
__declspec(naked) void SuccessFunction()
{
	__asm
	{
		mov eax, 1
		ret
	}
}

//-----------------------------------------------------------------------------

void DefaultOnError_Writer(const char * func, const char * error)
{
	UNREFERENCED_PARAMETER(func);
	UNREFERENCED_PARAMETER(error);
}

//-----------------------------------------------------------------------------

// Ctor
cPk2Writer::cPk2Writer()
{
	// Clear our private data
	dwEax_setup = 0;
	dwEbx_setup = 0;
	dwEcx_setup = 0;
	dwEdx_setup = 0;
	bInit = false;
	bLoaded = false;
	pGFXAccess = NULL;
	hDLL = NULL;

	// Clear string memory
	memset(loadPk2Filename, 0, MAX_PATH + 1);
	memset(accessPk2String, 0, MAX_PATH + 1);

	// Clear function pointers
	GFXDllCreateObject = NULL;
	GFXDllReleaseObject = NULL;

	// Set the default error logging function
	errorFunction = DefaultOnError_Writer;
}

//-----------------------------------------------------------------------------

// Dtor
cPk2Writer::~cPk2Writer()
{
}

//-----------------------------------------------------------------------------

// Create the pk2 api access object
bool cPk2Writer::Initialize(const char * gfxDllFilename)
{
	// Make sure we are not already initialized
	if(bInit)
	{
		errorFunction("cPk2Writer::Initialize", "The object is already initialized.");
		return false;
	}

	// Try to load the DLL
	hDLL = LoadLibrary(gfxDllFilename);
	if(hDLL == NULL)
	{
		errorFunction("cPk2Writer::Initialize", "Could not load GFXFileManager.dll");
		return false;
	}

	// Load the gfx dll startup function
	GFXDllCreateObject = GetProcAddress(hDLL, "GFXDllCreateObject");
	if(!GFXDllCreateObject)
	{
		FreeLibrary(hDLL);
		errorFunction("cPk2Writer::Initialize", "Could not load the GFXDllCreateObject function from GFXFileManager.dll");
		return false;
	}

	// Load the gfx dll cleanup function
	GFXDllReleaseObject = GetProcAddress(hDLL, "GFXDllReleaseObject");
	if(!GFXDllReleaseObject)
	{
		FreeLibrary(hDLL);
		errorFunction("cPk2Writer::Initialize", "Could not load the GFXDllReleaseObject function from GFXFileManager.dll");
		return false;
	}

	// Since we are doing a lot of asm code, save initial registers
	__asm pushad

	// This variable holds the pointer to the newly created access object
	DWORD dwHolder = 0;
	LPVOID pHolder = &dwHolder;

	// Store the function pointer we want to call
	FARPROC gfxFunc = GFXDllCreateObject;

	// Setup the access to the DLL by creating the object
	__asm
	{
		push 0x1007
		push pHolder
		push 0x01
		call gfxFunc
	}

	// Store the pointer to the access object
	pGFXAccess = (tGFXAccess*)(*((DWORD*)pHolder));

	// Holds the registers
	DWORD dwA = 0;
	DWORD dwB = 0;
	DWORD dwC = 0;
	DWORD dwD = 0;

	// Store this to use in asm
	LPVOID ptr1 = pGFXAccess;

	// Setup part two
	__asm
	{
		mov ecx, ptr1
		mov eax, [ecx]
		mov edx, [eax + 0xA4]
		push SuccessFunction
		call edx

		// Save these for when we set the current pk2
		mov dwA, eax
		mov dwB, ebx
		mov dwC, ecx
		mov dwD, edx
	}

	dwEax_setup = dwA;
	dwEbx_setup = dwB;
	dwEcx_setup = dwC;
	dwEdx_setup = dwD;

	// Restore original registers
	__asm popad

	// We are now initialized
	bInit = true;

	// Success
	return true;
}

//-----------------------------------------------------------------------------

// Clean up the pk2 api access object
void cPk2Writer::Deinitialize()
{
	// If we have a gfx dll access object
	if(pGFXAccess)
	{
		// Store the function pointer we want to call
		FARPROC gfxFunc = GFXDllReleaseObject;

		// Store this to use in asm
		LPVOID ptr1 = pGFXAccess;

		// Since we are doing a lot of asm code, save initial registers
		__asm
		{
			// Save registers
			pushad

			// Free the graphics dll
			push ptr1
			call gfxFunc

			// Restore registers
			popad
		}

		// Clear the pointer
		pGFXAccess = 0;
	}

	// If the gfx dll is loaded
	if(hDLL)
	{
		// Unload the library
		FreeLibrary(hDLL);

		// Clear the pointer
		hDLL = 0;
	}

	// Clear our private data
	dwEax_setup = 0;
	dwEbx_setup = 0;
	dwEcx_setup = 0;
	dwEdx_setup = 0;
	bInit = false;
	bLoaded = false;

	// Clear string memory
	memset(loadPk2Filename, 0, MAX_PATH + 1);
	memset(accessPk2String, 0, MAX_PATH + 1);

	// Clear function pointers
	GFXDllCreateObject = NULL;
	GFXDllReleaseObject = NULL;
}

//-----------------------------------------------------------------------------

// Closes the open PK2 file
void cPk2Writer::Close()
{
	// Store this to use in asm
	LPVOID ptr1 = pGFXAccess;

	// Start opening the file
	DWORD var1 = 0;
	LPVOID lpVar1 = &var1;

	// Activate the pk2 file to operate on
	__asm
	{
		// Save registers
		pushad

		// Restore these from after the gfx dll was created
		mov eax, dwEax_setup
		mov ebx, dwEbx_setup
		mov ecx, dwEcx_setup
		mov edx, dwEdx_setup

		// Params to open the pk2 for access
		push lpVar1
		push 0
		push 0

		// Code to open the pk2 for access
		mov ecx, ptr1
		mov edx, [ecx]
		mov eax, [edx + 0x10]		
		call eax

		// Restore registers
		popad
	}

	// No longer loaded
	bLoaded = false;
}

// Sets a pk2 file to import into
bool cPk2Writer::Open(const char * pk2Filename, const char * accessString)
{
	// Result of a function
	DWORD dwResult = 0;

	// Start opening the file
	DWORD var1 = 0;
	LPVOID lpVar1 = &var1;

	// Store these as explicit pointers
	char * paccess = accessPk2String;
	char * pfilename = loadPk2Filename;

	// make sure we are initilized
	if(bInit == false)
	{
		// Log the error
		errorFunction("cPk2Reader::Open", "This object has not been initialized yet.");

		// Error
		return false;
	}

	// Store the values
	_snprintf(loadPk2Filename, MAX_PATH, "%s", pk2Filename);
	_snprintf(accessPk2String, MAX_PATH, "%s", accessString);

	// Store this to use in asm
	LPVOID ptr1 = pGFXAccess;

	// Activate the pk2 file to operate on
	__asm
	{
		// Save registers
		pushad

		// Restore these from after the gfx dll was created
		mov eax, dwEax_setup
		mov ebx, dwEbx_setup
		mov ecx, dwEcx_setup
		mov edx, dwEdx_setup

		// Params to open the pk2 for access
		push lpVar1
		push paccess
		push pfilename

		// Code to open the pk2 for access
		mov ecx, ptr1
		mov edx, [ecx]
		mov eax, [edx + 0x10]		
		call eax

		// Store the result
		mov dwResult, eax

		// Restore registers
		popad
	}

	// Check the result
	if(dwResult == 0)
	{
		// Log the error
		errorFunction("cPk2Writer::Open", "There is a problem accessing the GFXFileManager DLL.");

		// Failure
		return false;
	}

	// We have a filel loaded now
	bLoaded = true;

	// Success
	return true;
}

//-----------------------------------------------------------------------------

// Replace a file in a pk2 with another from file
bool cPk2Writer::ImportFile(const char * entryFilename, const char * inputFilename)
{
	// Pointer to the input data
	LPBYTE updateData = 0;

	// Size of the new data
	DWORD updateDataSize = 0;

	// Store the pointer to the string for asm access
	char * pkFilePathName = (char*)entryFilename;

	// Handle to the input file
	HANDLE hFile = 0;

	// Cannot replace a file if there is no PK2 set
	if(strlen(pkFilePathName) == 0)
	{
		// Log the error
		errorFunction("cPk2Writer::ImportFile", "A PK2 file must be set as active first.");

		// Failure
		return false;
	}

	// Try and open the settings config file to verify that it is there
	hFile = CreateFile(inputFilename, GENERIC_READ, 0, NULL, OPEN_EXISTING, 0, NULL);
	if(hFile == INVALID_HANDLE_VALUE)
	{
		// Build the error message
		char error[256] = {0};
		_snprintf(error, 255, "Could not open the input file: %s", inputFilename);

		// Log the error
		errorFunction("cPk2Writer::ImportFile", error);

		// Failure
		return false;
	}

	// Store file size
	updateDataSize = GetFileSize(hFile, NULL);

	// Make sure there is data to replace the entry with
	if(updateDataSize == 0)
	{
		// Close the handle
		CloseHandle(hFile);

		// Log the error
		errorFunction("cPk2Writer::ImportFile", "The replacement file must be at least 1 byte in size.");

		// Failure
		return false;
	}

	// Allocate data for the new file
	updateData = new BYTE[updateDataSize];

	// Make sure it was allocated
	if(updateData == NULL)
	{
		// Close the handle
		CloseHandle(hFile);

		// Log the error
		errorFunction("cPk2Writer::ImportFile", "There is not enough memory to allocate for the file.");

		// Error
		return false;
	}

	// Zero out the memory
	memset(updateData, 0, updateDataSize);

	// Read in the data
	DWORD dwRead = 0;
	if(ReadFile(hFile, updateData, updateDataSize, &dwRead, 0) == 0)
	{
		// Close the handle
		CloseHandle(hFile);

		// Free allocated memory
		delete [] updateData;

		// Log the error
		errorFunction("cPk2Writer::ImportFile", "Could not read from the input file.");

		// Error
		return false;
	}

	// Make sure we read in all the data
	if(dwRead != updateDataSize)
	{
		// Close the handle
		CloseHandle(hFile);

		// Free allocated memory
		delete [] updateData;

		// Log the error
		errorFunction("cPk2Writer::ImportFile", "Could not read all of the data from the input file.");

		// Error
		return false;
	}

	// Close the handle
	CloseHandle(hFile);

	// Result of the operation
	DWORD result = 0;

	// Store this to use in asm
	LPVOID ptr1 = pGFXAccess;

	// Step 1
	__asm
	{
		// Setup dll access functions
		mov esi, ptr1
		mov edx, [esi]
		mov eax, [edx + 0x3C]
		mov ecx, esi

		// Prepare the pk2 to be patched
		push updateDataSize
		push pkFilePathName
		call eax

		// Save the result, which is the file in the update process
		mov result, eax
		mov ebx, eax

		// Save regs
		pushad
	}

	// Check result
	if(result == 0)
	{
		// Close the handle
		CloseHandle(hFile);

		// Free allocated memory
		delete [] updateData;

		// Log the error
		errorFunction("cPk2Writer::ImportFile", "Step 1 Failed.");

		// Failure
		return false;
	}
	
	// How many bytes were written
	DWORD asmWrote = 0;

	// Step 2
	__asm
	{
		popad 

		// Store how many bytes were written
		lea ecx, asmWrote
		push ecx

		// Save the file size
		mov eax, updateDataSize
		push eax

		// Prepare the dll functions
		mov edi, [esi]
		add edi, 0x4C
		mov edx, [edi]

		// Data to update
		mov eax, updateData
		push eax

		// File # to patch
		push ebx

		mov ecx, esi
		call edx
		mov result, eax

		// Save regs
		pushad
	}

	// Check result
	if(result == 0)
	{
		// Close the handle
		CloseHandle(hFile);

		// Free allocated memory
		delete [] updateData;

		// Log the error
		errorFunction("cPk2Writer::ImportFile", "Step 2 Failed.");

		// Failure
		return false;
	}

	// Step 3
	__asm
	{
		popad
		mov eax, [esi]
		mov edx,[eax + 0x44]
		push ebx
		mov ecx, esi
		call edx
		mov result, eax
	}

	// Check result
	if(result == 0)
	{
		// Close the handle
		CloseHandle(hFile);

		// Free allocated memory
		delete [] updateData;

		// Log the error
		errorFunction("cPk2Writer::ImportFile", "Step 3 Failed.");

		// Failure
		return false;
	}

	// Free allocated memory
	delete [] updateData;

	// Success
	return true;
}

//-----------------------------------------------------------------------------

// Set the error logging function
void cPk2Writer::SetOnErrorFunction(OnErrorFunction func)
{
	// Store the error function
	errorFunction = func;

	// If the user does not want one, then set the default handler
	if(errorFunction == NULL)
	{
		errorFunction = DefaultOnError_Writer;
	}
}

// Returns true if a PK2 file is currently loaded
bool cPk2Writer::IsLoaded()
{
	return bLoaded;
}