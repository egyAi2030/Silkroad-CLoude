#pragma once

#include "IFileManager.h"



class CPFileManager : public IFileManager {
private:

	char root_dir[260]; //0x0004 
	char current_dir[260]; //0x0108 guessed ???
	__int32 bIsOpen; //0x020C 
	char pad_0x0210[0xC]; //0x0210
	HMODULE mainModuleHandle; //0x021C Get by function_8 
	
	char pad_0x0220[0x1C]; //0x0220
	unsigned char bSomething; //0x023C bool
	
	char pad_0x023D[0x2B]; //0x023D
	
	error_handler_t error_handler; //0x0268 set by fun_A4 
	HWND hwnd; //0x026C 
	
	char pad_0x0270[0x1D0]; //0x0270


	int bListMoreFiles;


public:
	virtual int Mode(void);
	virtual int ConfigSet(int, int);
	virtual int ConfigGet(int, int);

	virtual int CreateContainer(const char *filename,  const char *password);
	virtual int OpenContainer(const char *filename, const char* password, int mode);
	virtual int CloseContainer(void);
	virtual int IsOpen(void);

	virtual int CloseAllFiles(void);
	virtual HMODULE MainModuleHandle(void);
	virtual int Function_9(int);


	virtual int Open(CJArchiveFm *fm, const char *filename, int access, int unknown);
	virtual int Open(const char *filename, int access, int unknown);

	virtual int Function_12(void);
	virtual int Function_13(void);
	virtual int Create(CJArchiveFm * fm, const char * filename, int unknown); //
	virtual int Create(const char* filename, int unknown); //

	virtual int Delete(const char *filename);
	virtual int Close(int index);

	virtual int Read(int hFile, char* lpBuffer, int nNumberOfBytesToRead, unsigned long *lpNumberOfBytesRead);
	virtual int Write(int hFile, const char* lpBuffer, int nNumberOfBytesToWrite, unsigned long *lpNumberOfBytesWritten);


	virtual char* CmdLinePath(void);
	virtual char* CmdLineExe(void); 


	virtual __int64* GetDirectoryPosition(__int64* pPosition) override;
	virtual bool SetDirectoryPosition(__int64 position) override;

	
	virtual int DirectoryCreate(const char* name);
	virtual int DirectoryRemove(const char* name);

	virtual bool ResetDirectory(void);
	virtual bool ChangeDirectory(const char* dirname);
	virtual int GetDirectoryName(size_t buffersize, char* Dst);

	virtual int SetVirtualPath(const char *path);
	virtual int GetVirtualPath(char* dest);
	
	// Listing files 
	virtual search_handle_t* FindFirstFile(search_handle_t* pSearchHandle, const char* pSearchPattern, search_result_t* pSearchResult);
	virtual bool FindNextFile(search_handle_t* pSearchHandle, search_result_t* pSearchResult);
	virtual bool FindClose(search_handle_t* pSearchHandle);

	// File information
	virtual int FileNameFromHandle(int hFile, char* dst, size_t count); //GetFileName
	virtual int GetFileSize(int hFile, LPDWORD lpFileSizeHigh); //

	virtual BOOL GetFileTime(int hFile, LPFILETIME lpCreationTime, LPFILETIME lpLastWriteTime); //
	virtual BOOL SetFileTime(int hFile, LPFILETIME lpCreationTime, LPFILETIME lpLastWriteTime); //

	virtual int Seek(int hFile, LONG lDistanceToMove, DWORD dwMoveMethod);


	// error control
	virtual HWND GetHwnd(void);
	virtual void SetHwnd(HWND);
	virtual void RegisterErrorHandler(error_handler_t callback);


	// Importing
	virtual int ImportDirectory(const char *srcdir, const char *dstdir, const char *directory_name, bool create_target_dir) ;
	virtual int ImportFile(const char *srcdir, const char *dstdir, const char *filename, bool create_target_dir);
	virtual int ExportDirectory(const char *srcdir, const char *dstdir, const char *directory_name, bool create_target_dir);
	virtual int ExportFile(const char *srcdir, const char *dstdir, const char *filename, bool create_target_dir); // create_target_dir is unused




	virtual int FileExists(char* name, int flags);


	virtual int UpdateCurrentDirectory(void);
	virtual int Function_50(int);

	virtual int Lock(int); //
	virtual int Unlock(); //

	virtual ~CPFileManager() { };

	CPFileManager() {
		this->bIsOpen = 0;
		this->bListMoreFiles = 1;
	}
};
