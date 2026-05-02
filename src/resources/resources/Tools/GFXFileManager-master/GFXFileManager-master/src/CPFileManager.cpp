#include "CPFileManager.h"
#include <iostream>
#include <string>

#include "debug.h"

int CPFileManager::Mode() {
	debug(DEBUG_OTHER, "WFM::get_mode() = 1\n");

	return 1;
}

int CPFileManager::ConfigSet(int a, int b) {

	if (a == 2) {
		bSomething = (b == 0) ? 0 : 1;
	}

	debug(DEBUG_UNKNOWN, "WFM::ConfigSet(%d , %d) = 0\n", a, b);

	return 0;
}

int CPFileManager::ConfigGet(int a, int b) {
	
	debug(DEBUG_UNKNOWN, "WFM::ConfigGet(%d , %d) = 0\n", a, b);

	_ERROR_MSGBOX();

	return 0;
}

int CPFileManager::CreateContainer(const char *filename, const char *password) {
	
	debug(DEBUG_CONTAINER, "WFM::CreateContainer(\"%s\", \"%s\") = 0\n", filename, password);

	return 0;
}

int CPFileManager::OpenContainer(const char *filename, const char* password, int mode) {

	debug(DEBUG_CONTAINER, "WFM::OpenContainer(\"%s\", \"%s\", 0x%08x) = 1\n", filename, password, mode);
	//MessageBox(0, "Open called!", "", MB_OK);
	
	char buffer[512] = {0};
	strcpy_s(buffer, sizeof(buffer), filename);

	char *pchr = strrchr(buffer, '.');

	*pchr = 0;

	// Set the root directory
	strcpy_s(this->root_dir, sizeof(this->root_dir), buffer);
	this->ResetDirectory();

	this->bIsOpen = 1;

	return 1;
}

int CPFileManager::CloseContainer() {

	this->bIsOpen = 0;

	debug(DEBUG_CONTAINER, "WFM::CloseContainer() = 1\n");

	return 1;
}

int CPFileManager::IsOpen() {
	debug(DEBUG_CONTAINER, "WFM::IsOpen() = %d\n", this->bIsOpen);

	return bIsOpen;
}

int CPFileManager::CloseAllFiles() {
	// This parses something like a list ...

	debug(DEBUG_UNKNOWN, "WFM::CloseAllFiles() = 1\n");

	_ERROR_MSGBOX();

	return 1;
}

HMODULE CPFileManager::MainModuleHandle(void) {

	debug(DEBUG_OTHER, "WFM::MainModuleHandle() = %08x\n", this->mainModuleHandle);

	return this->mainModuleHandle;
}

int CPFileManager::Function_9(int a) {

	debug(DEBUG_UNKNOWN, "WFM::Function_9(%d) = -1\n", a);


	return -1;
}

// This is a shortcut for open ...
int CPFileManager::Open(CJArchiveFm *fm, const char *filename, int access, int unknown) {

  debug(DEBUG_FILE, "WFM::Open2(0x%08x, \"%s\", 0x%08x, 0x%08x) = 0\n", fm, filename, access, unknown);

  fm->field_15 = 1;
  fm->pFileManager = this;


  int handle = this->Open(filename, access, unknown);

  fm->hFile = handle;

  // Magic flag stuff that is hopefully not used -.-
  fm->is_write_mode = (access >> 30) & 1;

  if (fm->is_write_mode) {
	fm->pCurrent = fm->buffer;
  } else {
	fm->pCurrent = fm->pEnd;
  }

  if (fm->hFile == -1) {
	return 0;
  }

  return 1;
}

int CPFileManager::Open(const char *filename, int access, int unknown) {
	int dwShareMode = 0;
	int dwCreationDistribution = 0;

	if (access == 0x40000000) 
	{
		dwCreationDistribution = CREATE_ALWAYS; // 0x2
	} 
	else
	{
		dwCreationDistribution = OPEN_EXISTING; // 0x3

		if (access == 0x80000000) 
		{
			dwShareMode = FILE_SHARE_READ;
		}
	}

	// Bugfix #1
	if (filename[0] == '\\') {
		filename++;
	}

	SetCurrentDirectory(this->current_dir);

	HANDLE hFile = CreateFile(filename, access, dwShareMode, 0, dwCreationDistribution, FILE_ATTRIBUTE_ARCHIVE, 0);
	
	debug(DEBUG_FILE, "WFM::Open(\"%s\", 0x%x, 0x%x) = 0x%x\n", filename, access, unknown, (int)hFile);

	// Example filtering
	if (strstr(filename, "bsr")) {
		debug(DEBUG_FILE_GEN, "Opening File in %s\n", filename);
	}

	if (hFile == INVALID_HANDLE_VALUE) {
		debug(DEBUG_FILE_NOTFOUND, "Error opening file: %s%s\n", this->current_dir, filename);
	}
	
	return (int)hFile;
}

int CPFileManager::Function_12(void)  {

	debug(DEBUG_UNKNOWN, "WFM::Function_12() = -1\n");

	return -1;
}


int CPFileManager::Function_13(void)  {

	debug(DEBUG_UNKNOWN, "WFM::Function_13() = 0\n");

	return 0;
}

int CPFileManager::Create(CJArchiveFm * fm, const char * filename, int unknown)  {

	debug(DEBUG_UNKNOWN, "WFM::Create(%d, %d, %d) = 0\n", fm, filename, unknown);

	_ERROR_MSGBOX();

	return 0;
}

int CPFileManager::Create(const char* filename, int unknown) {

	debug(DEBUG_UNKNOWN, "WFM::Create(\"%s\", %08x) = 0\n", filename, unknown);

	_ERROR_MSGBOX();

	return -1;
}

int CPFileManager::Delete(const char *filename) {

	debug(DEBUG_FILE, "WFM::Delete(\"%s\") = 0\n", filename);


	return 0;
}

int CPFileManager::Close(int hFile) {

	debug(DEBUG_FILE, "WFM::Close(%08x) = 0\n", hFile);

	return CloseHandle((HANDLE)hFile);
}


int CPFileManager::Read(int hFile, char* lpBuffer, int nNumberOfBytesToRead, unsigned long* lpNumberOfBytesRead) {

	int ret = ReadFile((HANDLE)hFile, lpBuffer, nNumberOfBytesToRead, lpNumberOfBytesRead, 0);

	debug(DEBUG_IO, "WFM::Read(%08x, %08x, %d, %d) = %d\n", hFile, lpBuffer, nNumberOfBytesToRead, lpNumberOfBytesRead, ret);

	return ret;
}

int CPFileManager::Write(int hFile, const char* lpBuffer, int nNumberOfBytesToWrite, unsigned long *lpNumberOfBytesWritten) {

	int ret = WriteFile((HANDLE)hFile, lpBuffer, nNumberOfBytesToWrite, lpNumberOfBytesWritten, 0);

	debug(DEBUG_IO, "WFM::Write(%08x, %08x, %d, %d) = %d\n", hFile, lpBuffer, nNumberOfBytesToWrite, lpNumberOfBytesWritten, ret);

	return ret;
}

char* CPFileManager::CmdLinePath(void){
	debug(DEBUG_OTHER, "WFM::CmdLinePath() = \"Silkroad.exe\"\n");
	return "Silkroad.exe";
}

char* CPFileManager::CmdLineExe(void) {
	debug(DEBUG_OTHER, "WFM::CmdLineExe() = \"/22 0 0\"\n");
	return "/22 0 0";
}

__int64 * CPFileManager::GetDirectoryPosition(__int64 * pPosition)
{
	debug(DEBUG_OTHER, "WFM::GetDirectoryPosition(0x%x)\n", pPosition);
	*pPosition = 0;
	return pPosition;
}

bool CPFileManager::SetDirectoryPosition(__int64 position)
{
	debug(DEBUG_OTHER, "WFM::SetDirectoryPosition(0x%08x) = 0\n", position);
	return false;
}

int CPFileManager::DirectoryCreate(const char* name) {

	SetCurrentDirectory(this->current_dir);
	int ret = CreateDirectory(name, 0);

	debug(DEBUG_DIRECTORY, "WFM::DirectoryCreate(\"%s\") = 0x%x\n", name, ret);

	return ret;
}

int CPFileManager::DirectoryRemove(const char* name) {

	SetCurrentDirectory(this->current_dir);
	int ret = RemoveDirectory(name);

	debug(DEBUG_DIRECTORY, "WFM::DirectoryRemove(\"%s\") = 0x%08x\n", name, ret);

	return ret;
}

bool CPFileManager::ResetDirectory(void) {
	
	int ret = SetCurrentDirectory(this->root_dir);
	GetCurrentDirectory(sizeof(this->current_dir), this->current_dir);

	int len = strlen(this->current_dir);

	if (this->current_dir[len] != '\\') {
		strcat_s(this->current_dir, sizeof(this->current_dir), "\\");
	}

	debug(DEBUG_DIRECTORY, "WFM::ResetDirectory() = %d\n" , ret);

	debug(DEBUG_DIRECTORY_GEN, "Reseting to: %s\n", this->root_dir);
	debug(DEBUG_DIRECTORY_GEN, "Current Directory is now: %s\n", this->current_dir);

	return ret;
}

bool CPFileManager::ChangeDirectory(const char* dirname) {

	if (dirname[0] == 0) {
		return 1;
	}

	SetCurrentDirectory(this->current_dir);

	bool ret = SetCurrentDirectory(dirname);

	GetCurrentDirectory(sizeof(this->current_dir), this->current_dir);

	int len = strlen(this->current_dir);

	if (this->current_dir[len] != '\\') {
		strcat_s(this->current_dir, sizeof(this->current_dir), "\\");
	}

	debug(DEBUG_DIRECTORY, "WFM::ChangeDirectory(\"%s\") = %d\n", dirname, ret);

	return ret;
}

int CPFileManager::GetDirectoryName(size_t buffersize, char* Dst) {

	strcpy_s(Dst, buffersize, this->current_dir);

	int len = strlen(Dst);

	debug(DEBUG_DIRECTORY, "WFM::GetDirectoryName(%d, %08x) = %d\n", buffersize, Dst, len);

	return len;
}

int CPFileManager::SetVirtualPath(const char *path) {
	strcpy_s(this->root_dir, sizeof(this->root_dir), path);
	strcpy_s(this->current_dir, sizeof(this->current_dir), path);

	debug(DEBUG_DIRECTORY, "WFM::SetVirtualPath(%08x) = 1\n", path);

	return 1;
}

int CPFileManager::GetVirtualPath(char* dest) {
	strcpy_s(dest, sizeof(this->current_dir), this->current_dir);

	debug(DEBUG_DIRECTORY, "WFM::GetVirtualPath(%08x) = 1\n", dest);

	return 1;
}

search_handle_t* CPFileManager::FindFirstFile(search_handle_t* pSearchHandle, const char* pSearchPattern, search_result_t* pSearchResult) {

	debug(DEBUG_SEARCH, "WFM::FindFirstFile(%08x, \"%s\", %08x) = 0\n", pSearchHandle, pSearchPattern, pSearchResult);

	pSearchHandle->Success = 0;

	if (this->bListMoreFiles >= 0) {
		
		pSearchHandle->Success = 1;

		pSearchResult->Type = ENTRY_TYPE_FOLDER;

		
		pSearchResult->CreationTime.dwLowDateTime = 0x80000;

		pSearchResult->Size = 1337;
		
		strcpy_s(pSearchResult->Name, sizeof(pSearchResult->Name), "TestingEntry");
		
		this->bListMoreFiles--;
	}


	return pSearchHandle;
}

bool CPFileManager::FindNextFile(search_handle_t* pSearchHandle, search_result_t* pSearchResult) {

	debug(DEBUG_SEARCH, "WFM::FindNextFile(%08x, %08x) = 0\n", pSearchHandle, pSearchResult);

	pSearchHandle->Success = 0;

	if (this->bListMoreFiles >= 0) {
		
		pSearchHandle->Success = 1;

		pSearchResult->Type = ENTRY_TYPE_FILE;

		strcpy_s(pSearchResult->Name, sizeof(pSearchResult->Name), "TestingFile.txt");
		
		this->bListMoreFiles--;
	}


	return 0;
}

bool CPFileManager::FindClose(search_handle_t* search) {
	debug(DEBUG_SEARCH, "WFM::FindClose(%08x) = 0\n", search);

	// Should always return 1 ...
	this->bListMoreFiles = 1;
	return true;
}

int CPFileManager::FileNameFromHandle(int hFile, char* dst, size_t count) {

	char buffer[512] = {0};

	DWORD ret = GetFinalPathNameByHandle((HANDLE)hFile, buffer, sizeof(buffer), 0);

	if (ret == ERROR_PATH_NOT_FOUND) {
		debug(DEBUG_FILE, "WFM::FileNameFromHandle(%08x, %08x, %08x) = 0\n", hFile, dst, count);
		return 0;
	}

	// \\?\D:\Chronyc ..
	strcpy_s(dst, count, &(buffer[4 + strlen(this->root_dir)]));

	for (int i = 0; dst[i] != 0; i++) {
		dst[i] = tolower(dst[i]);
	}

	debug(DEBUG_FILE, "WFM::FileNameFromHandle(%08x, %08x, %08x) = 1\n", hFile, dst, count);
	return 1;
}


int CPFileManager::GetFileSize(int hFile, LPDWORD lpFileSizeHigh) {
	int ret = ::GetFileSize((HANDLE)hFile, lpFileSizeHigh);
	
	debug(DEBUG_FILE, "WFM::GetFileSize(%d, %08x) = %d\n", hFile, lpFileSizeHigh, ret);

	return ret;
}

BOOL CPFileManager::GetFileTime(int hFile, LPFILETIME lpCreationTime, LPFILETIME lpLastWriteTime) {
	int ret = ::GetFileTime((HANDLE)hFile, lpCreationTime, 0, lpLastWriteTime);

	debug(DEBUG_FILE, "WFM::GetFileTime(%08x, %08x, %08x) = %d\n", hFile, lpCreationTime, lpLastWriteTime, ret);

	return ret;
}

BOOL CPFileManager::SetFileTime(int hFile, LPFILETIME lpCreationTime, LPFILETIME lpLastWriteTime) {

	int ret = ::SetFileTime((HANDLE)hFile, lpCreationTime, 0, lpLastWriteTime);

	debug(DEBUG_FILE, "WFM::SetFileTime(%08x, %08x, %08x) = %d\n", hFile, lpCreationTime, lpLastWriteTime, ret);

	return ret;
}

int CPFileManager::Seek(int hFile, LONG lDistanceToMove, DWORD dwMoveMethod) {

	int ret = SetFilePointer((HANDLE)hFile, lDistanceToMove, 0, dwMoveMethod);
	
	debug(DEBUG_IO, "WFM::Seek(%08x, %d, %d) = %d\n", hFile, lDistanceToMove, dwMoveMethod);

	return ret;
}


HWND CPFileManager::GetHwnd(void) {
	debug(DEBUG_OTHER, "WFM::GetHwnd() = %08x\n", this->hwnd);

	return this->hwnd;
}

void CPFileManager::SetHwnd(HWND nhwnd) {
	debug(DEBUG_OTHER, "WFM::SetHwnd(%08x)\n", nhwnd);

	this->hwnd = nhwnd;
}

void CPFileManager::RegisterErrorHandler(error_handler_t callback) {
	debug(DEBUG_OTHER, "WFM::RegisterErrorHandler(0x%08x)\n", callback);

	this->error_handler = callback;
}

int CPFileManager::ImportDirectory(const char *srcdir, const char *dstdir, const char *directory_name, bool create_target_dir) {

	debug(DEBUG_UNKNOWN, "WFM::ImportDirectory\"%s\", \"%s\", \"%s\", %08x) = 0\n", srcdir, dstdir, directory_name, create_target_dir);


	return 0;
}

int CPFileManager::ImportFile(const char *srcdir, const char *dstdir, const char *filename, bool create_target_dir) {

	debug(DEBUG_FILE, "WFM::ImportFile(\"%s\", \"%s\", \"%s\", %08x) = 0\n", srcdir, dstdir, filename, create_target_dir);


	return 0;
}

int CPFileManager::ExportDirectory(const char *srcdir, const char *dstdir, const char *directory_name, bool create_target_dir) {

	debug(DEBUG_UNKNOWN, "WFM::ExportDirectory(\"%s\", \"%s\", \"%s\", %08x) = 0\n",srcdir, dstdir, directory_name, create_target_dir);


	return 0;
}

int CPFileManager::ExportFile(const char *srcdir, const char *dstdir, const char *filename, bool create_target_dir) {

	debug(DEBUG_UNKNOWN, "WFM::ExportFile(\"%s\", \"%s\", \"%s\", %08x) = 0\n", srcdir, dstdir, filename, create_target_dir);


	return 0;
}


int CPFileManager::FileExists(char* name, int flags) {

	char buffer[512];

	strcpy_s(buffer, sizeof(buffer), current_dir);
	strcat_s(buffer, sizeof(buffer), name);

	int attrib = GetFileAttributes(buffer);

	int ret = 0; // File found

	if (attrib == INVALID_FILE_ATTRIBUTES) {
		ret = -1; // File not found
	}

	debug(DEBUG_FILE, "WFM::FileExists(\"%s\", %08x) = %d\n", name, flags, ret);

	return ret;
}

int CPFileManager::UpdateCurrentDirectory(void) {

	debug(DEBUG_UNKNOWN, "WFM::UpdateCurrentDirectory() = 0\n");


	return 0;
}

int CPFileManager::Function_50(int a) {

	debug(DEBUG_UNKNOWN, "WFM::Function_50(0x%08x) = 0\n", a);


	return 0;
}

int CPFileManager::Lock(int a) {

	debug(DEBUG_UNKNOWN, "WFM::Lock(0x%08x) = 0\n", a);


	return 0;
}

int CPFileManager::Unlock() {

	debug(DEBUG_UNKNOWN, "WFM::Unlock() = 0\n");

	return 0;
}
