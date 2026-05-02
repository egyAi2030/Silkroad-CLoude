#ifndef PK2READER_H_
#define PK2READER_H_

//-----------------------------------------------------------------------------

#ifndef _WINDOWS_
	#include <windows.h>
#endif

#ifndef _STRING_
	#include <string>
#endif

#ifndef _VECTOR_
	#include <vector>
#endif

//-----------------------------------------------------------------------------

// Forward declarations
struct tPk2Header;
struct tNode;
class cBlowFish;

//-----------------------------------------------------------------------------

// Structure for a pk2 entry
struct tPK2Entry
{
	// Type of entry is it, 0 is NULL, 1 is a folder, 2 is a file
	int type;
	
	// Name of the entry
	char name[MAX_PATH + 1];

	// Path of the entry
	char path[MAX_PATH + 1];

	// Entry file times
	FILETIME createTime;
	FILETIME modifyTime;

	// Position of the entry in the pk2
	DWORD position;

	// Size of the entry
	DWORD size;

	// Directory depth of entry
	int level;
};

//-----------------------------------------------------------------------------

// Typedef function pointer
typedef void (*OnErrorFunction)(const char *, const char *);

// This is a PK2 reading class. It will load a pk2, parse through the encrypted 
// file entries and allow a user to extract files.
class cPk2Reader
{
public:
	// Mode to search for PK2 entries
	enum SearchMode_String{MODE_ANY, MODE_PATH_ONLY, MODE_TITLE_ONLY};

private:
	// Blowfish structure for this object
	cBlowFish * pBlowFish;

	// A list of pointers to free from memory
	std::vector<void*> memoryList;

	// Pk2 name
	std::string mPk2Name;

	// The private PK2 header structure
	tPk2Header * mHeader;

	// Vector of entries
	std::vector<tPK2Entry*> entries;

	// Name of the current pk2 file
	std::string mFileName;

	// Private internal parsing function
	bool Parse(HANDLE hFile, unsigned char * buffer, tNode * &current, int level);

	// Done parsing the pk2 flag
	bool allDone;

	// Error reporting function
	OnErrorFunction errorFunction;

	// Initialized and loaded flag
	bool bInit;
	bool bLoaded;

public:
	// Default ctor for the class
	cPk2Reader();
	// Default dtor for the class, cleans up the memory used by the class
	~cPk2Reader();

	// Return a vector containing all of the entries in the PK2 file
	std::vector<tPK2Entry *> GetAllEntries();
	// Returns an entry at a specified index
	tPK2Entry * GetEntry(size_t index);

	// Return the name of the PK2 file that is currently loaded
	std::string GetPK2Name();

	// Returns true if a PK2 file is currently loaded
	bool IsLoaded();

	// Create the pk2 access object
	bool Initialize();
	// Destory the pk2 access object
	void Deinitialize();

	// Loads and parses a PK2 file
	bool Open(std::string filename);
	// Closes the PK2 file
	void Close();

	// Searches the vector set for elements that either have the substring title and/or path, NULL means the ignore that param
	std::vector<tPK2Entry *> Search(std::vector<tPK2Entry *> & set, const char * phrase, SearchMode_String mode);
	// Searches for elements that either have the substring title and/or path, NULL means the ignore that param
	std::vector<tPK2Entry *> Search(const char * phrase, SearchMode_String mode);
	// Searches the vector set for elements that have a size bettwen min and max, -1 means ignore the parameter
	std::vector<tPK2Entry *> Search(std::vector<tPK2Entry *> & set, int minSize, int maxSize);
	// Searches for elements that have a size bettwen min and max, -1 means ignore the parameter
	std::vector<tPK2Entry *> Search(int minSize, int maxSize);
	// Searches the vector set for elements that has the specific type
	std::vector<tPK2Entry *> Search(std::vector<tPK2Entry *> & set, int type);
	// Searches the vector set for elements that has the specific type
	std::vector<tPK2Entry *> Search(int type);

	// Extract a single entry
	bool Extract(const char * outPath, tPK2Entry * entry);
	// Extracts the entire pk2 archive
	bool Extract(const char * outPath);
	// Extract a vector of entries
	bool Extract(const char * outPath, std::vector<tPK2Entry *> & set);

	// Creates a file for the header information for all pk2 entries
	bool GenerateListing(const char * outPath);

	// Set the error logging function
	void SetOnErrorFunction(OnErrorFunction func);
};

//-----------------------------------------------------------------------------

#endif
