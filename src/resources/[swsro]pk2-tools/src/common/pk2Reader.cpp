#ifndef PK2READER_H_
	#include "pk2Reader.h"
#endif

#ifndef _SHLOBJ_H_
	#include <shlobj.h>
#endif

#ifndef _SSTREAM_
	#include <sstream>
#endif

#ifndef BLOWFISH_H_
	#include "BlowFish.h"
#endif

//-----------------------------------------------------------------------------

// Start 1 byte alignment
#pragma pack(push, 1)

	// The PK2 Header
	struct tPk2Header
	{
		char header[30];
		DWORD version;
		DWORD unk2;
		unsigned char reserved[218];
	};

	// The private PK2 File Entry Header
	struct tPk2EntryPrivate
	{
		BYTE type;
		char name[81];
		FILETIME accessTime;
		FILETIME createTime;
		FILETIME modifyTime;
		DWORD positionLow;
		DWORD positionHigh;
		DWORD size;
		DWORD nextChain;
		WORD reserved1;
		WORD reserved2;
		WORD reserved3;
	};

// End 1 byte alignment
#pragma pack(pop)

//-----------------------------------------------------------------------------

struct tNode
{
	tNode * parent;
	tNode * sibling;
	tNode * child;
	tPk2EntryPrivate entry;
};

//-----------------------------------------------------------------------------

// Returns the translated error code as a string
std::string ErrorCodeToString(DWORD dwError)
{
	LPVOID lpMsgBuf = NULL;
	FormatMessage(FORMAT_MESSAGE_ALLOCATE_BUFFER | FORMAT_MESSAGE_FROM_SYSTEM | FORMAT_MESSAGE_IGNORE_INSERTS, NULL, dwError, MAKELANGID(LANG_NEUTRAL, SUBLANG_DEFAULT), (LPTSTR)&lpMsgBuf, 0, NULL);
	std::stringstream ss;
	ss << ((LPCTSTR)lpMsgBuf);
	LocalFree(lpMsgBuf);
	return ss.str();
}

//-----------------------------------------------------------------------------

void DefaultOnError_Reader(const char * func, const char * error)
{
	UNREFERENCED_PARAMETER(func);
	UNREFERENCED_PARAMETER(error);
}

//-----------------------------------------------------------------------------

// Default ctor for the class
cPk2Reader::cPk2Reader()
{
	// Clear our private data
	pBlowFish = NULL;
	mHeader = NULL;
	bLoaded = false;
	allDone = false;
	bInit = false;

	// Set the default error logging function
	errorFunction = DefaultOnError_Reader;
}

// Default dtor for the class, cleans up the memory used by the class
cPk2Reader::~cPk2Reader()
{
}

// Create the pk2 api access object
bool cPk2Reader::Initialize()
{
	// Make sure we are not already initialized
	if(bInit)
	{
		errorFunction("cPk2Reader::Initialize", "The object is already initialized.");
		return false;
	}

	// Silkroad Pk2 key data (modified key, not algorithm)
	unsigned char keyData[] = {0x31, 0xC8, 0xD4, 0x7D, 0x4C, 0x73};

	// Make sure our Pk2Header is actually 256 bytes at runtime.
	DWORD dwSize = sizeof(tPk2Header);
	if(dwSize != 256)
	{
		errorFunction("cPk2Reader::Initialize", "The tPk2Header structure is not 256 bytes on this computer.");
		return false;
	}

	// Make sure our tPk2EntryPrivate is actually 128 bytes at runtime.
	dwSize = sizeof(tPk2EntryPrivate);
	if(dwSize != 128)
	{
		errorFunction("cPk2Reader::Initialize", "The tPk2EntryPrivate structure is not 128 bytes on this computer.");
		return false;
	}

	// Allocate new memory for the header
	mHeader = new tPk2Header;

	// Make sure the system has enough memory
	if(mHeader == NULL)
	{
		errorFunction("cPk2Reader::Initialize", "There is not enough memory to allocate a tPk2Header object.");
		return false;
	}

	// Zero out the memory
	memset(mHeader, 0, sizeof(tPk2Header));

	// No file loaded
	bLoaded = false;

	// Allocate memory for the blowfish object
	pBlowFish = new cBlowFish;

	// Make sure the system has enough memory
	if(pBlowFish == NULL)
	{
		errorFunction("cPk2Reader::Initialize", "There is not enough memory to allocate a cBlowFish object.");
		return false;
	}

	// Initialize the blowfish algo
	pBlowFish->Initialize(keyData, 6);

	// Clear the flag
	allDone = false;

	// Set the default error logging function
	errorFunction = DefaultOnError_Reader;

	// We are now initialized
	bInit = true;

	// Success
	return true;
}

// Destroy the pk2 access object
void cPk2Reader::Deinitialize()
{
	if(mHeader)
	{
		// Deallocate memory
		delete mHeader;
		mHeader = 0;
	}

	if(pBlowFish)
	{
		// Deallocate memory
		delete pBlowFish;
		pBlowFish = 0;
	}

	// Clear our private data
	bLoaded = false;
	allDone = false;
	bInit = false;
}

// Return a vector containing all of the entries in the PK2 file
std::vector<tPK2Entry *> cPk2Reader::GetAllEntries()
{
	return entries;
}

// Returns an entry at a specified index
tPK2Entry * cPk2Reader::GetEntry(size_t index)
{
	// Bounds checking
	if(index < 0 || index > entries.size())
	{
		errorFunction("cPk2Reader::GetEntry", "Invalid index specified, out of bounds.");
		return 0;
	}

	// Return the entry at a specified index
	return entries[index];
}

// Return the name of the PK2 file that is currently loaded
std::string cPk2Reader::GetPK2Name()
{
	return mPk2Name;
}

// Returns true if a PK2 file is currently loaded
bool cPk2Reader::IsLoaded()
{
	return bLoaded;
}

// Closes the PK2 file
void cPk2Reader::Close()
{
	// No longer a file loaded
	bLoaded = false;

	size_t sze = entries.size();
	for(size_t x = 0; x < sze; ++x)
	{
		// If there is memory
		if(entries[x])
		{
			// Free it
			delete entries[x];
		}
	}
	// Clear all the entries now
	entries.clear();

	// Erase the names
	mFileName = "";
	mPk2Name = "";
}

// Loads and parses a PK2 file
bool cPk2Reader::Open(std::string filename)
{
	// Read operation variable
	DWORD dwRead = 0;

	// The string to compare the first 30 bytes of the header to
	char headerVerify[30] = {0};

	// Input buffer for reading the pk2
	unsigned char buffer[128] = {0};

	// Temporary current node
	tNode * current = 0;

	// Level of the files
	int level = 0;

	// make sure we are initialized
	if(bInit == false)
	{
		// Log the error
		errorFunction("cPk2Reader::Open", "This object has not been initialized yet.");

		// Error
		return false;
	}

	// Try to open the PK2 file
	HANDLE hFile = CreateFile(filename.c_str(), GENERIC_READ, FILE_SHARE_READ, NULL, OPEN_EXISTING, 0, NULL);
	if(hFile == INVALID_HANDLE_VALUE)
	{
		// Log the error
		errorFunction("cPk2Reader::Open", ErrorCodeToString(GetLastError()).c_str());

		// Error
		return false;
	}

	// Close the existing pk2 if we have one opened
	Close();

	// Store the file name
	mFileName = filename;

	// Build the name of the pk2 file itself (assumes its in the right format of 'media.pk2' and not something weird like media.pk2.blah)
	int start = (int)mFileName.find_last_of('\\') + 1;
	int count =  (int)mFileName.find_last_of('.');
	mPk2Name = mFileName.substr(start, count - start);

	// Try to read in the header
	if(ReadFile(hFile, mHeader, sizeof(tPk2Header), &dwRead, NULL) == 0)
	{
		// Done with the file handle
		CloseHandle(hFile);

		// Log the error
		errorFunction("cPk2Reader::Open", ErrorCodeToString(GetLastError()).c_str());

		// Error
		return false;
	}

	// Verify the bytes read filled in the header
	if(dwRead != sizeof(tPk2Header))
	{
		// Done with the file handle
		CloseHandle(hFile);

		// Log the error
		errorFunction("cPk2Reader::Open", "dwRead != sizeof(tPk2Header)");

		// Error
		return false;
	}

	// Make sure the version is correct
	if(mHeader->version != 0x01000002)
	{
		// Done with the file handle
		CloseHandle(hFile);

		// Log the error
		errorFunction("cPk2Reader::Open", "mHeader->version != 0x01000002");

		// Error
		return false;
	}

	// Make sure the unknown value is correct
	//[UPDATED]0x5b63de01
	if(mHeader->unk2 != 0x5b63de01)
	{
		// Done with the file handle
		CloseHandle(hFile);

		// Log the error
		errorFunction("cPk2Reader::Open", "mHeader->unk2 != 0x30dad801");

		// Error
		return false;
	}

	// Build the verify string
	_snprintf(headerVerify, 30, "%s%c", "JoyMax File Manager!", 0x0A);

	// Compare the strings
	if(strcmp(mHeader->header, headerVerify) != 0)
	{
		// Done with the file handle
		CloseHandle(hFile);

		// Log the error
		errorFunction("cPk2Reader::Open", "Invalid file manager header string.");

		// Error
		return false;
	}

	// Try to read in the header
	if(ReadFile(hFile, buffer, sizeof(tPk2EntryPrivate), &dwRead, NULL) == 0)
	{
		// Done with the file handle
		CloseHandle(hFile);

		// Log the error
		errorFunction("cPk2Reader::Open", ErrorCodeToString(GetLastError()).c_str());

		// Error
		return false;
	}

	// Make sure we got a full entry
	if(dwRead != sizeof(tPk2EntryPrivate))
	{
		// Done with the file handle
		CloseHandle(hFile);

		// Log the error
		errorFunction("cPk2Reader::Open", "dwRead != sizeof(tPk2EntryPrivate)");

		// Error
		return false;
	}

	// New root node memory
	tNode * mRoot = new tNode;

	// Make sure the system has enough memory
	if(mRoot == NULL)
	{
		// Done with the file handle
		CloseHandle(hFile);

		// Log the error
		errorFunction("cPk2Reader::Open", "There is not enough memory to allocate a tNode object.");

		// Error
		return false;
	}

	// Decode the buffer into the root node
	pBlowFish->Decode(buffer, (BYTE*)&mRoot->entry, sizeof(tPk2EntryPrivate));

	// Store the current node pointer for parsing
	current = mRoot;

	// Save the entry into the vector of entries
	tPK2Entry * e = new tPK2Entry;
	memset(e, 0, sizeof(tPK2Entry));
	_snprintf(e->name, MAX_PATH, "%s", mRoot->entry.name);
	e->position = mRoot->entry.positionLow;
	e->size = mRoot->entry.size;
	e->createTime = mRoot->entry.createTime;
	e->modifyTime =  mRoot->entry.modifyTime;
	e->type = mRoot->entry.type;
	e->level = level;
	entries.push_back(e);

	// Reserver space for entries
	entries.reserve(60000);

	// Clear this flag
	allDone = false;

	// Start the parse process and run it until it's done, when false is returned
	while(Parse(hFile, buffer, current, level));

	// Clear this flag
	allDone = false;

	// Done with the file handle
	CloseHandle(hFile);

	// Deallocate memory from the root
	delete mRoot;

	// Prestore
	size_t size1 = memoryList.size();

	// Loop through all the memory entries
	for(size_t x = 0; x < size1; ++x)
	{
		// Get the pointer
		tNode * tmp = (tNode *)memoryList[x];

		// Free the memory
		delete tmp;
	}

	// Empty the list
	memoryList.clear();

	// Path for the entry
	char path[2048] = {0};

	// Current file directory depth
	int curLevel = 0;
	
	// Prestore
	size1 = entries.size();

	// Looop through all entries to build file path
	for(size_t x = 0; x < size1; ++x)
	{
		// If this entry has a name
		if(strlen(entries[x]->name))
		{
			// If there is a sub directory to process
			if(curLevel < entries[x]->level)
			{
				// If it's a directory
				if(entries[x - 1]->type == 1)
				{
					// Build the path
					strcat(path, entries[x - 1]->name);
					strcat(path, "\\");

					// New level
					curLevel++;
				}
			}

			// If the current level is deeper than the current level
			while(curLevel > entries[x]->level)
			{
				// Go up one level
				curLevel--;

				// If we are back at root level, just clear the whole path
				if(curLevel == 0)
				{
					memset(path, 0, 2048);
				}
				// Otherwise
				else
				{
					// Store the length of the current path
					size_t olen = strlen(path);

					// Only if there is a current path
					if(olen)
					{
						// Take out the last path seperator
						path[olen - 1] = 0;

						// Reduce string length
						olen--;

						// Loop backwards looking for the last \ 
						for(size_t x = olen - 1; x > 0; --x)
						{
							// If it matches a \ 
							if(path[x] == '\\')
								break;

							// Clear out the character
							path[x] = 0;
						}
					}
				}
			}

			// Copy the path into the entry
			_snprintf(entries[x]->path, MAX_PATH, "%s", path);
		}
	}

	// We have a file loaded now
	bLoaded = true;

	// Success
	return true;
}

// Private internal parsing function
bool cPk2Reader::Parse(HANDLE hFile, unsigned char * buffer, tNode * &current, int level)
{
	// Are we in a NULL section
	static bool inClear = false;

	// Holds the temp entry
	tPk2EntryPrivate tmpEntry = {0};

	// Hold read bytes
	DWORD dwRead = 0;

	// Try to read in the header
	if(ReadFile(hFile, buffer, sizeof(tPk2EntryPrivate), &dwRead, NULL) == 0)
	{
		// Done with the file handle
		CloseHandle(hFile);

		// Log the error
		errorFunction("cPk2Reader::Parse", ErrorCodeToString(GetLastError()).c_str());

		// Error
		return false;
	}

	// Make sure we got a full entry
	if(dwRead != sizeof(tPk2EntryPrivate))
	{
		// Done with the file handle
		CloseHandle(hFile);

		// Log the error
		errorFunction("cPk2Reader::Parse", "dwRead != sizeof(tPk2EntryPrivate)");

		// Error
		return false;
	}

	// If we are done processing all entries, return
	if(allDone)
	{
		// End this cycle
		return false;
	}

	// Decode the buffer into the entry
	pBlowFish->Decode(buffer, (BYTE*)&tmpEntry, sizeof(tPk2EntryPrivate));

	// End of the chain when these are not all 0's -- ASSUMPTION!
	if(!(tmpEntry.reserved1 == 0 && tmpEntry.reserved2 == 0 && tmpEntry.reserved3 == 0))
	{
		// All done
		inClear = false;

		// End this cycle
		return false;
	}

	// If there is no name, it is a NULL entry
	if(strlen(tmpEntry.name) == 0)
	{
		// We are in a filler space
		inClear = true;
	}
	else
	{
		// If we are in the filler space
		if(inClear)
		{
			// No longer in the filler space
			inClear = false;

			// Done with this chain
			return false;
		}
	}

	// If we get to this condition, we are done parsing the pk2
	if(level == 0 && strcmp(tmpEntry.name, ".") == 0)
	{
		// We do not need to process entries anymore
		allDone = true;

		// End this cycle
		return false;
	}

	// Save the entry
	current->sibling = new tNode;
	memset(current->sibling, 0, sizeof(tNode));

	// Track the memory entry to free at the end
	memoryList.push_back(current->sibling);

	// Save the parent before we move the current pointer
	current->sibling->parent = current->parent;

	// Advance the current entry pointer
	current = current->sibling;

	// Store the new data
	memcpy(&current->entry, &tmpEntry, sizeof(tPk2EntryPrivate));

	// Save the entry into the vector of entries
	tPK2Entry * e = new tPK2Entry;
	memset(e, 0, sizeof(tPK2Entry));
	_snprintf(e->name, MAX_PATH, "%s", tmpEntry.name);
	e->position = tmpEntry.positionLow;
	e->size = tmpEntry.size;
	e->createTime = tmpEntry.createTime;
	e->modifyTime =  tmpEntry.modifyTime;
	e->type = tmpEntry.type;
	e->level = level;

	// Save the entry
	entries.push_back(e);

	// Process the entry
	if(current->entry.type == 1 && strcmp(current->entry.name, ".") != 0 && strcmp(current->entry.name, "..") != 0)
	{
		// Current position to resume to
		DWORD curPos = SetFilePointer(hFile, 0, 0, FILE_CURRENT);

		// Change to the new location
		SetFilePointer(hFile, current->entry.positionLow, 0, FILE_BEGIN);

		// Allocate memory for a child node
		current->child = new tNode;

		// Clear the memory
		memset(current->child, 0, sizeof(tNode));

		// Track the memory entry to free at the end
		memoryList.push_back(current->child);

		// Store the parent node
		current->child->parent = current;

		// Recurse
		while(Parse(hFile, buffer, current->child, level + 1));

		// Restore
		SetFilePointer(hFile, curPos, 0, FILE_BEGIN);
	}

	// If we have a pointer to another set of data
	if(current->entry.nextChain)
	{
		// Current position to resume to
		DWORD curPos = SetFilePointer(hFile, 0, 0, FILE_CURRENT);

		// Change to the new location
		SetFilePointer(hFile, current->entry.nextChain, 0, FILE_BEGIN);

		// Recurse
		while(Parse(hFile, buffer, current, level));

		// Restore
		SetFilePointer(hFile, curPos, 0, FILE_BEGIN);
	}

	// Success
	return true;
}

// Searches the vector set for elements that either have the substring title and/or path, NULL means the ignore that param
std::vector<tPK2Entry *> cPk2Reader::Search(std::vector<tPK2Entry *> & set, const char * phrase, SearchMode_String mode)
{
	// Temp vector
	std::vector<tPK2Entry *> results;

	// Prestore
	size_t size1 = set.size();

	// Loop through the set
	for(size_t x = 0; x < size1; ++x)
	{
		// Only process entries with a name (but don't worry about size in case folders are needed)
		if(strlen(set[x]->name) && strcmp(set[x]->name, ".") != 0 && strcmp(set[x]->name, "..") != 0)
		{
			// If there is a string for the title
			if(mode == MODE_TITLE_ONLY)
			{
				// If it's not found, we cannot add the entry
				std::string s = set[x]->name;
				if(s.find(phrase) == -1)
				{
					continue;
				}
			}

			// If there is a string for the path
			if(mode == MODE_PATH_ONLY)
			{
				// If it's not found, we cannot add the entry
				std::string s = set[x]->path;
				if(s.find(phrase) == -1)
				{
					continue;
				}
			}

			// If we are searching either string, we just need to find one occurance
			if(mode == MODE_ANY)
			{
				std::string n = set[x]->name;
				std::string p = set[x]->path;
				if(n.find(phrase) == -1 && p.find(phrase) == -1)
				{
					continue;
				}
			}

			// Add the entry
			results.push_back(set[x]);
		}
	}
	return results;
}

// Searches for elements that either have the substring title and/or path, NULL means the ignore that param
std::vector<tPK2Entry *> cPk2Reader::Search(const char * phrase, SearchMode_String mode)
{
	// Search the entire set
	return Search(entries, phrase, mode);
}

// Searches the vector set for elements that have a size bettwen min and max, -1 means ignore the parameter
std::vector<tPK2Entry *> cPk2Reader::Search(std::vector<tPK2Entry *> & set, int minSize, int maxSize)
{
	// Temp vector
	std::vector<tPK2Entry *> results;

	// Prestore
	size_t size1 = set.size();

	// Loop through the set
	for(size_t x = 0; x < size1; ++x)
	{
		// Only process entries with a name and have a filesize
		if(strlen(set[x]->name) && set[x]->size && strcmp(set[x]->name, ".") != 0 && strcmp(set[x]->name, "..") != 0)
		{
			// Too small
			if(minSize != -1 && set[x]->size < (size_t)minSize)
				continue;

			// Too big
			if(maxSize != -1 && set[x]->size > (size_t)maxSize)
				continue;

			// Keep
			results.push_back(set[x]);
		}
	}
	return results;
}

// Searches for elements that have a size bettwen min and max, -1 means ignore the parameter
std::vector<tPK2Entry *> cPk2Reader::Search(int minSize, int maxSize)
{
	// Search the entire set
	return Search(entries, minSize, maxSize);
}

// Searches the vector set for elements that has the specific type
std::vector<tPK2Entry *> cPk2Reader::Search(std::vector<tPK2Entry *> & set, int type)
{
	// Temp vector
	std::vector<tPK2Entry *> results;

	// Prestore
	size_t size1 = set.size();

	// Loop through the set
	for(size_t x = 0; x < size1; ++x)
	{
		// Only process entries that are not the "." or ".." folders
		if(set[x]->type == type && strcmp(set[x]->name, ".") != 0 && strcmp(set[x]->name, "..") != 0)
		{
			// Keep
			results.push_back(set[x]);
		}
	}
	return results;
}

// Searches the vector set for elements that has the specific type
std::vector<tPK2Entry *> cPk2Reader::Search(int type)
{
	// Search the entire set
	return Search(entries, type);
}

// Extract a single entry
bool cPk2Reader::Extract(const char * outPath, tPK2Entry * entry)
{
	// Temp vector
	std::vector<tPK2Entry *> tE;

	// Save the single element
	tE.push_back(entry);

	// Return the result
	return Extract(outPath, tE);
}

// Extracts the entire pk2 archive
bool cPk2Reader::Extract(const char * outPath)
{
	// Return the result
	return Extract(outPath, entries);
}

// Extract a vector of entries
bool cPk2Reader::Extract(const char * outPath, std::vector<tPK2Entry *> & set)
{
	// Try to open the PK2 file
	HANDLE hFile = CreateFile(mFileName.c_str(), GENERIC_READ, FILE_SHARE_READ, NULL, OPEN_EXISTING, 0, NULL);
	if(hFile == INVALID_HANDLE_VALUE)
	{
		// Log the error
		errorFunction("cPk2Reader::Extract", ErrorCodeToString(GetLastError()).c_str());

		// Error
		return false;
	}

	// Let's do some efficient programming ;) I will find the largest file being extracted and
	// allocate a memory buffer that larger. That way, only one memory allocaiton is done and
	// file extraction of larger archives is faster.
	size_t maxAlloc = 0;

	// Prestore size
	size_t size1 = set.size();

	// Loop through all the set and write out the files
	for(size_t x = 0; x < size1; ++x)
	{
		// Find the largest file we have to save
		if(set[x]->size > maxAlloc)
		{
			maxAlloc = set[x]->size;
		}
	}

	// Allocate memory in a block that will hold all files
	char * buffer = new char[maxAlloc];

	// Check to make sure there was enough memory
	if(buffer == NULL)
	{
		// Done with the file
		CloseHandle(hFile);

		// Log the error
		errorFunction("cPk2Reader::Extract", "There is not enough memory to allocate a reading buffer.");

		// Failure
		return false;
	}

	// Number of bytes read
	DWORD dwRead = 0;

	// Save where the file should be saved to
	std::string bpath = outPath;

	// If there is not a trailing \, add one
	if(*(bpath.end() - 1) != '\\')
		bpath.append("\\");

	// Save the pk2 name as part of the path
	bpath.append(mPk2Name);
	bpath.append("\\");

	// Create the directories in the path to the file
	SHCreateDirectoryEx(NULL, bpath.c_str(), NULL);

	// Prestore
	size1 = set.size();

	// Loop through all the set and write out the files
	for(size_t x = 0; x < size1; ++x)
	{
		// Path we can build upon
		std::string path = bpath;

		// Null set are skipped
		if(strlen(set[x]->name) == 0)
			continue;

		// Skip special directories
		if(strcmp(set[x]->name, ".") == 0)
			continue;
		if(strcmp(set[x]->name, "..") == 0)
			continue;

		// Append the path to the new file
		path.append(set[x]->path);

		// If there is not a trailing \, add one
		if(*(path.end() - 1) != '\\')
			path.append("\\");

		// Clear out the buffer
		memset(buffer, 0, maxAlloc);

		// Create the directories in the path to the file
		SHCreateDirectoryEx(NULL, path.c_str(), NULL);

		// Append the final name
		path.append(set[x]->name);

		// If it's a directory, create it
		if(set[x]->type == 1)
		{
			// Create the directories in the path to the file
			SHCreateDirectoryEx(NULL, path.c_str(), NULL);
		}
		else
		{
			// Move the file to read from the start of the file
			SetFilePointer(hFile, set[x]->position, 0, FILE_BEGIN);

			// Read in the file
			if(ReadFile(hFile, buffer, set[x]->size, &dwRead, NULL) == FALSE)
			{
				// Log the error
				errorFunction("cPk2Reader::Extract", ErrorCodeToString(GetLastError()).c_str());

				// Skip
				continue;
			}

			// Error checking
			if(set[x]->size != dwRead)
			{
				// Build the error
				std::stringstream ss;
				ss << "Error: set[x]->size != dwRead" << "\nFile: " << set[x]->name << "\n";

				// Log the error
				errorFunction("cPk2Reader::Extract", ss.str().c_str());

				// Skip
				continue;
			}

			// Create the file
			HANDLE hNewFile = CreateFile(path.c_str(), GENERIC_WRITE, 0, NULL, CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL, NULL); 
			if(hNewFile == INVALID_HANDLE_VALUE)
			{
				// Build the error
				std::stringstream ss;
				ss << "Error: " << ErrorCodeToString(GetLastError()) << "\nFile: " << path << "\n";

				// Log the error
				errorFunction("cPk2Reader::Extract", ss.str().c_str());

				// Skip
				continue;
			}

			// Write the data
			if(WriteFile(hNewFile, buffer, set[x]->size, &dwRead, NULL) == FALSE)
			{
				// Log the error
				errorFunction("cPk2Reader::Extract", ErrorCodeToString(GetLastError()).c_str());

				// Skip
				continue;
			}

			if(dwRead != set[x]->size)
			{
				// Log the error
				errorFunction("cPk2Reader::Extract", "Write File Error: dwRead != set[x]->size");

				// Skip
				continue;
			}

			// Save the real file time from the PK2
			SetFileTime(hNewFile, &set[x]->createTime, NULL, &set[x]->modifyTime);

			// Done with the new file handle
			CloseHandle(hNewFile);
		}
	}

	// Free memory
	delete [] buffer;

	// Done with the file
	CloseHandle(hFile);

	// Success
	return true;
}

// Creates a file for the header information for all pk2 entries
bool cPk2Reader::GenerateListing(const char * outPath)
{
	// Save where the file should be saved to
	std::string bpath = outPath;

	// If there is not a trailing \, add one
	if(*(bpath.end() - 1) != '\\')
		bpath.append("\\");

	// Save the pk2 name as part of the path
	bpath.append(mPk2Name);
	bpath.append(".txt");

	// Try to open the out file
	FILE * outFile = fopen(bpath.c_str(), "w");

	// if the file could not be opened, just return
	if(outFile == NULL)
	{
		// Log the error
		errorFunction("cPk2Reader::GenerateListing", "Could not open the specified output file.");

		// Failure
		return false;
	}

	// Prestore the size
	size_t size1 = entries.size();

	// Loop through all of the entries
	for(size_t x = 0; x < size1; ++x)
	{
		// Only process 
		if(strlen(entries[x]->name))
		{
			// Output tabs to represent path depth
			for(int y = 0; y < entries[x]->level; ++y)
				fprintf(outFile, "\t");

			// If it's a directory prefix it with a [D]
			if(entries[x]->type == 1)
				fprintf(outFile, "[D]");

			// Output the path and name
			fprintf(outFile, "%s%s", entries[x]->path, entries[x]->name);

			// If it's a file, show the size
			if(entries[x]->type == 2)
				fprintf(outFile, " (%i bytes)", entries[x]->size);

			// If it's a file, show the size
			if(entries[x]->type == 2)
				fprintf(outFile, " [%X]", entries[x]->position);

			// Next line
			fprintf(outFile, "\n");
		}
	}

	// Done with the file
	fclose(outFile);

	// Success
	return true;
}

// Set the error logging function
void cPk2Reader::SetOnErrorFunction(OnErrorFunction func)
{
	// Store the error function
	errorFunction = func;

	// If the user does not want one, then set the default handler
	if(errorFunction == NULL)
	{
		errorFunction = DefaultOnError_Reader;
	}
}
