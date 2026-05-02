# GFXFileManager

You can use the IFileManager-interface to utilize existing GFXFileManager-libraries. You can build your own pk2-extractor/editor, even if the GFXFileManager.dll was modifed.

## Overview

The GFXFileManager.dll is responsible for accessing *.pk2-container files. It provides all required methods for reading, writing and creating files and also folders. It also provides search-access to the directory structure.

## Interface

The library two major classes derived from one interface `IFileManager`.

* CPFileManager for access to PK2-containers
* CWFileManager for access to the local hard disk

There are no derived local variables, only 55 methods.

### Accessing containers

I order to access containers, the interface provides 4 different methods.

```
// Create a new container
// Parameter:
// - filename: filename of the container
// - password: password for accessing the new container
virtual int CreateContainer(const char *filename,  const char *password) = 0;

// Open an existing container
// Parameter:
// - filename: filename of the container
// - password: password required for accessing the container
// - mode: unknown, maybe for read and write access
virtual int OpenContainer(const char *filename, const char* password, int mode) = 0;

// Close the current container
// No Parameter
virtual int CloseContainer(void) = 0; //


// Returns 1, if this instance has opened a container
virtual int IsOpen(void) = 0; //
```

Make sure to check for success using `IsOpen()`.

### Accessing Files

```
//
// Files
//

// Open a file inside the container using a path
// Parameter:
// - filename: filename, relative to current dir or absolute path inside archive
// - access: 0 for open-existing, 0x80000000 for open and share_read, 0x40000000 for create_always
// - unknown: not used for original CWFileManager
// Return:
// Handle of opened file (can be any number or pointer) or -1 if opening is was unsuccessful
virtual int Open(const char *filename, int access, int unknown) = 0; //


// Delete a file by name
// Parameter:
// - filename: name of file to delete
virtual int Delete(char *filename) = 0; //

// Close file by handle
// Parameter:
// hFile: Any handle or pointer identifiying this file
virtual int Close(int hFile) = 0; //

// Read a number of bytes from file
// Parameter:
// hFile: Any handle or pointer identifiying this file
// lpBuffer: pointer to reserved memory for read operation
// nNumberOfBytesToWrite: size of lpBuffer
// lpNumberOfBytesWritten: pointer to memory, will contain the number of bytes read from the file
virtual int Read(int hFile, char* lpBuffer, int nNumberOfBytesToWrite, unsigned long *lpNumberOfBytesWritten) = 0;

// Write a number of bytes to file
// Parameter:
// hFile: Any handle or pointer identifiying this file
// lpBuffer: pointer to reserved memory for read operation
// nNumberOfBytesToWrite: size of lpBuffer
// lpNumberOfBytesWritten: pointer to memory, will contain the number of bytes written to the file
virtual int Write(int hFile, char* lpBuffer, int nNumberOfBytesToWrite, unsigned long *lpNumberOfBytesWritten) = 0;
```

Working with files in the archive is similar to working with files using the plain `WinAPI`. It all comes down to these three steps:

1. Open the file and store the returned handle
2. Work with the file (read, write) using the handle
3. Close the file using the handle


For the original GFXFileManager, the returned handle is just a unique number.


Joymax decided they needed another open method. This method simply fills out the given structure `fm` and then calls `Open` in order to get the Handle.
```
// Open a file inside the container using the CJArchiveFm-class
// Parameter:
// - fm: A valid pointer to the CJArchiveFm-class
// - filename: filename, relative to current dir or absolute path inside archive
// - access: 0 for open-existing, 0x80000000 for open and share_read, 0x40000000 for create_always
// - unknown: not used for original CWFileManager
virtual int Open2(CJArchiveFm* fm, const char *filename, int access, int unknown) = 0;
```

There are more methods you can use to get file information and control read and write operations. These are not documented yet, but somewhat self-explaining:

```
//
// File Information
//

virtual int FileNameFromHandle(int hFile, char* dst, size_t count) = 0;
virtual int GetFileSize(int hFile, LPDWORD lpFileSizeHigh) = 0;
virtual BOOL GetFileTime(int hFile, LPFILETIME lpCreationTime, LPFILETIME lpLastWriteTime) = 0;
virtual BOOL SetFileTime(int hFile, LPFILETIME lpCreationTime, LPFILETIME lpLastWriteTime) = 0;
virtual int Seek(int hFile, LONG lDistanceToMove, DWORD dwMoveMethod) = 0; //
```

### Directories and virtual filesystem

The whole container is a big virtual filesystem. I am not sure how it works entirely. TODO!



















