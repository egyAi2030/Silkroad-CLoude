#ifndef PK2WRITER_H_
#define PK2WRITER_H_

//-----------------------------------------------------------------------------

#ifndef _WINDOWS_
	#include <windows.h>
#endif

#ifndef _STRING_
	#include <string>
#endif

//-----------------------------------------------------------------------------

// Typedef function pointer
typedef void (*OnErrorFunction)(const char *, const char *);

// Forward declaration
struct tGFXAccess;
 
// Pk2 writing class that will update existing files in a pk2 archive
class cPk2Writer
{
private:
	// Set of registers for storing registers for context change
	DWORD dwEax_setup;
	DWORD dwEbx_setup;
	DWORD dwEcx_setup;
	DWORD dwEdx_setup;

	// Object for gfx dll access
	tGFXAccess * pGFXAccess;

	// Handle to the gfx dll
	HMODULE hDLL;

	// Holds the path to the loaded pk2 files
	char loadPk2Filename[MAX_PATH + 1];

	// Holds the pk2 access key
	char accessPk2String[MAX_PATH + 1];

	// Handles to the graphics functions
	FARPROC GFXDllCreateObject;
	FARPROC GFXDllReleaseObject;

	// Error reporting function
	OnErrorFunction errorFunction;

	// Initialized and loaded flag
	bool bInit;
	bool bLoaded;

public:
	// Ctor
	cPk2Writer();
	// Dtor
	~cPk2Writer();

	// Create the pk2 access object
	bool Initialize(const char * gfxDllFilename);
	// Clean up the pk2 access object
	void Deinitialize();

	// Sets a pk2 file to import into
	bool Open(const char * pk2Filename, const char * accessString);
	// Closes the open PK2 file
	void Close();

	// Replace a file in a pk2 with another from file
	bool ImportFile(const char * entryFilename, const char * inputFilename);

	// Returns true if a PK2 file is currently loaded
	bool IsLoaded();

	// Set the error logging function
	void SetOnErrorFunction(OnErrorFunction func);
};

#endif
