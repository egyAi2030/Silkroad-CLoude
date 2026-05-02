// Pk2EditorDlg.cpp : implementation file
//

#include "stdafx.h"
#include "Pk2Editor.h"
#include "Pk2EditorDlg.h"
#include ".\pk2editordlg.h"

#include "../common/pk2Reader.h"
#include "../common/pk2Writer.h"

#ifdef _DEBUG
#define new DEBUG_NEW
#endif

// Log a status message to an edit control
void LogStatusMessage(const char * text);

// Pk2 objects
cPk2Writer writer;
cPk2Reader reader;

// Main dialog hwnd
HWND gDialogHwnd = 0;
HWND gDialogLog = 0;

// CPk2EditorDlg dialog

CPk2EditorDlg::CPk2EditorDlg(CWnd* pParent /*=NULL*/)
	: CDialog(CPk2EditorDlg::IDD, pParent)
{
	m_hIcon = AfxGetApp()->LoadIcon(IDI_ICON1);
}

void CPk2EditorDlg::DoDataExchange(CDataExchange* pDX)
{
	CDialog::DoDataExchange(pDX);
	DDX_Control(pDX, IDC_EDIT2, mAutoSingleFileName);
	DDX_Control(pDX, IDC_EDIT3, mAutoMultiFileName);
	DDX_Control(pDX, IDC_EDIT4, mManSingleFileName);
	DDX_Control(pDX, IDC_EDIT5, mEntryPath);
	DDX_Control(pDX, IDC_EDIT6, mEntryName);
	DDX_Control(pDX, IDC_BUTTON2, mImport1);
	DDX_Control(pDX, IDC_BUTTON4, mImport2);
	DDX_Control(pDX, IDC_BUTTON6, mImport3);
	DDX_Control(pDX, IDC_BUTTON1, mSel1);
	DDX_Control(pDX, IDC_BUTTON3, mSel2);
	DDX_Control(pDX, IDC_BUTTON5, mSel3);
}

BEGIN_MESSAGE_MAP(CPk2EditorDlg, CDialog)
	ON_WM_PAINT()
	ON_WM_QUERYDRAGICON()
	//}}AFX_MSG_MAP
	ON_WM_CLOSE()
	ON_COMMAND(ID_FILE_EXIT, OnExitFunc)
	ON_COMMAND(ID_FILE_OPEN32771, OnOpenFunc)
	ON_COMMAND(ID_FILE_CLOSE32772, OnCloseFunc)
	ON_BN_CLICKED(IDC_BUTTON1, OnBnClickedButton1)
	ON_BN_CLICKED(IDC_BUTTON3, OnBnClickedButton3)
	ON_BN_CLICKED(IDC_BUTTON5, OnBnClickedButton5)
	ON_BN_CLICKED(IDC_BUTTON2, OnBnClickedButton2)
	ON_BN_CLICKED(IDC_BUTTON4, OnBnClickedButton4)
	ON_BN_CLICKED(IDC_BUTTON6, OnBnClickedButton6)
END_MESSAGE_MAP()

void OnError(const char * function, const char * error)
{
	char buffer[32768] = {0};
	_snprintf(buffer, 32767, "%s: %s", function, error);
	LogStatusMessage(buffer);
}

// CPk2EditorDlg message handlers

BOOL CPk2EditorDlg::OnInitDialog()
{
	CDialog::OnInitDialog();

	// Set the icon for this dialog.  The framework does this automatically
	//  when the application's main window is not a dialog
	SetIcon(m_hIcon, TRUE);			// Set big icon
	SetIcon(m_hIcon, FALSE);		// Set small icon

	// TODO: Add extra initialization here
	
	// Register the error functions
	reader.SetOnErrorFunction(OnError);
	writer.SetOnErrorFunction(OnError);

	// Initialize the objects
	reader.Initialize();
	writer.Initialize("GFXFileManager.dll");

	// Store the dialogs hwnd
	gDialogHwnd = GetSafeHwnd();
	gDialogLog = ::GetDlgItem(gDialogHwnd, IDC_EDIT1);


	// Log some status messages
	LogStatusMessage("Welcome to the PK2 Editor!");
	LogStatusMessage("Made by: Drew Benton");
	LogStatusMessage("http://0x33.org");
	LogStatusMessage("");
	LogStatusMessage("");

	return TRUE;  // return TRUE  unless you set the focus to a control
}

// If you add a minimize button to your dialog, you will need the code below
//  to draw the icon.  For MFC applications using the document/view model,
//  this is automatically done for you by the framework.

void CPk2EditorDlg::OnPaint() 
{
	if (IsIconic())
	{
		CPaintDC dc(this); // device context for painting

		SendMessage(WM_ICONERASEBKGND, reinterpret_cast<WPARAM>(dc.GetSafeHdc()), 0);

		// Center icon in client rectangle
		int cxIcon = GetSystemMetrics(SM_CXICON);
		int cyIcon = GetSystemMetrics(SM_CYICON);
		CRect rect;
		GetClientRect(&rect);
		int x = (rect.Width() - cxIcon + 1) / 2;
		int y = (rect.Height() - cyIcon + 1) / 2;

		// Draw the icon
		dc.DrawIcon(x, y, m_hIcon);
	}
	else
	{
		CDialog::OnPaint();
	}
}

// The system calls this function to obtain the cursor to display while the user drags
//  the minimized window.
HCURSOR CPk2EditorDlg::OnQueryDragIcon()
{
	return static_cast<HCURSOR>(m_hIcon);
}

void CPk2EditorDlg::OnOK()
{
}

void CPk2EditorDlg::OnCancel()
{
}

void CPk2EditorDlg::OnClose()
{
	OnExitFunc();
}

void CPk2EditorDlg::OnExitFunc()
{
	if(MessageBox("Do you really wish to exit?", "Exit confirmation", MB_YESNO | MB_ICONQUESTION) == IDYES)
	{
		if(reader.IsLoaded())
			reader.Close();
		if(writer.IsLoaded())
			writer.Close();
		reader.Deinitialize();
		writer.Deinitialize();
		CDialog::OnOK();
	}
}

void CPk2EditorDlg::OnOpenFunc()
{
	bool result = false;

	// Buffers for the file chooser 
	char setDialogTitle[MAX_PATH + 1] = {0};
	char setDefFileName[MAX_PATH + 1] = {0};
	char setFilter[MAX_PATH + 1] = {0};
	char filename[MAX_PATH + 1] = {0};
	char setDirectory[MAX_PATH + 1] = {0};

	// File chooser struct
	OPENFILENAME fn = {0};

	_snprintf(setDialogTitle, MAX_PATH, "Please select a PK2 file..."); 
	_snprintf(setDefFileName, MAX_PATH, "*.pk2");
	_snprintf(setFilter, MAX_PATH, "PK2 Files\0*.pk2\0\0");

	// Build the file chooser dialog
	fn.lStructSize = sizeof(OPENFILENAME);					// Have to set this for the struct
	fn.Flags = OFN_FILEMUSTEXIST | OFN_PATHMUSTEXIST;		// File must exist
	fn.lpstrInitialDir = setDirectory;						// Default directory
	fn.lpstrTitle = setDialogTitle;							// Title we wish to display
	fn.lpstrFile = setDefFileName;							// Tell the chooser to use our buffer
	fn.nMaxFile = MAX_PATH;									// Max size of buffer
	fn.lpstrFileTitle = filename;							// Tell the chooser to use our buffer
	fn.nMaxFileTitle = MAX_PATH;							// Max size of buffer
	fn.lpstrFilter = setFilter;								// File filter

	// Show the dialog
	if(GetOpenFileName(&fn))
	{
		// Open the pk2 reader first
		bool result = reader.Open(fn.lpstrFile);
		if(result == false)
			return;

		// [UPDATE]

		char access[7] = {0};
		access[0] = 0x32;
		access[1] = 0x30;
		access[2] = 0x30;
		access[3] = 0x39;
		access[4] = 0xC4;
		access[5] = 0xEA;

		// Now the writer
		result = writer.Open(fn.lpstrFile, access);
		if(result == false)
		{
			reader.Close();
			return;
		}

		// Status
		LogStatusMessage("The PK2 has been loaded successfully.");

		mImport1.EnableWindow(TRUE);
		mImport2.EnableWindow(TRUE);
		mImport3.EnableWindow(TRUE);
		mSel1.EnableWindow(TRUE);
		mSel2.EnableWindow(TRUE);
		mSel3.EnableWindow(TRUE);
		mAutoSingleFileName.EnableWindow(TRUE);
		mAutoMultiFileName.EnableWindow(TRUE);
		mManSingleFileName.EnableWindow(TRUE);
		mEntryName.EnableWindow(TRUE);
		mEntryPath.EnableWindow(TRUE);
		mAutoSingleFileName.SetWindowText("");
		mAutoMultiFileName.SetWindowText("");
		mManSingleFileName.SetWindowText("");
		mEntryName.SetWindowText("");
		mEntryPath.SetWindowText("");

		// Change the title
		char buf[256] = {0};
		_snprintf(buf, 255, "PK2 Editor - %s.pk2", reader.GetPK2Name().c_str());
		this->SetWindowText(buf);
	}
}

void CPk2EditorDlg::OnCloseFunc()
{
	// Close the access objects if they were opened
	if(reader.IsLoaded())
	{
		reader.Close();
	}
	if(writer.IsLoaded())
	{
		writer.Close();
		LogStatusMessage("The PK2 has been closed successfully.");
	}

	mImport1.EnableWindow(FALSE);
	mImport2.EnableWindow(FALSE);
	mImport3.EnableWindow(FALSE);
	mSel1.EnableWindow(FALSE);
	mSel2.EnableWindow(FALSE);
	mSel3.EnableWindow(FALSE);
	mAutoSingleFileName.EnableWindow(FALSE);
	mAutoMultiFileName.EnableWindow(FALSE);
	mManSingleFileName.EnableWindow(FALSE);
	mEntryName.EnableWindow(FALSE);
	mEntryPath.EnableWindow(FALSE);
	mAutoSingleFileName.SetWindowText("");
	mAutoMultiFileName.SetWindowText("");
	mManSingleFileName.SetWindowText("");
	mEntryName.SetWindowText("");
	mEntryPath.SetWindowText("");

	// Restore the title
	this->SetWindowText("PK2 Editor");
}

//-----------------------------------------------------------------------------

// Log a status message to an edit control
void LogStatusMessage(const char * text)
{
	// Stores the buffer to append the message into
	static char buffer[32768] = {0};

	// Store the length of the window text
	int len = GetWindowTextLength(gDialogLog);

	// Auto clear out large buffers
	if(len > 30000 || len + strlen(text) + 3 > 32767)
	{
		SetWindowText(gDialogLog, "");
	}

	// Clear out the buffer
	memset(buffer, 0, 32768);

	//Get the text
	GetWindowText(gDialogLog, buffer, 32767);

	// Append the data
	strcat(buffer, "\r\n");
	strcat(buffer, text);

	// Set the new window text
	SetWindowText(gDialogLog, buffer);

	// Scroll to the bottom of the window
	SendMessage(gDialogLog, WM_VSCROLL, SB_BOTTOM, 0);
}

// Auto single file
void CPk2EditorDlg::OnBnClickedButton1()
{
	// Buffers for the file chooser 
	char setDialogTitle[MAX_PATH + 1] = {0};
	char setDefFileName[MAX_PATH + 1] = {0};
	char setFilter[MAX_PATH + 1] = {0};
	char filename[MAX_PATH + 1] = {0};
	char setDirectory[MAX_PATH + 1] = {0};

	// File chooser struct
	OPENFILENAME fn = {0};

	_snprintf(setDialogTitle, MAX_PATH, "Please select a file..."); 
	_snprintf(setDefFileName, MAX_PATH, "*.*");
	_snprintf(setFilter, MAX_PATH, "All Files\0*.*\0\0");

	// Build the file chooser dialog
	fn.lStructSize = sizeof(OPENFILENAME);					// Have to set this for the struct
	fn.Flags = OFN_FILEMUSTEXIST | OFN_PATHMUSTEXIST;		// File must exist
	fn.lpstrInitialDir = setDirectory;						// Default directory
	fn.lpstrTitle = setDialogTitle;							// Title we wish to display
	fn.lpstrFile = setDefFileName;							// Tell the chooser to use our buffer
	fn.nMaxFile = MAX_PATH;									// Max size of buffer
	fn.lpstrFileTitle = filename;							// Tell the chooser to use our buffer
	fn.nMaxFileTitle = MAX_PATH;							// Max size of buffer
	fn.lpstrFilter = setFilter;								// File filter

	// Show the dialog
	if(GetOpenFileName(&fn))
	{
		mAutoSingleFileName.SetWindowText(fn.lpstrFile);
	}
	else
	{
		mAutoSingleFileName.SetWindowText("");
	}
}

// Auto folder
void CPk2EditorDlg::OnBnClickedButton3()
{
	BROWSEINFO info = {0};
	LPITEMIDLIST lpList = NULL;

	// Prepare the browse for folder dialog
	info.lpszTitle = "Please select a folder...";
	info.ulFlags = BIF_RETURNONLYFSDIRS | BIF_VALIDATE;

	// Let the user select the folder
	lpList = SHBrowseForFolder(&info);

	// If there was not a result returned
	if(lpList == NULL)
	{
		mAutoMultiFileName.SetWindowText("");
		return;
	}

	// Buffer to hold the path
	CHAR szSelected[MAX_PATH + 1] = {0};

	// Temp int variable
	INT iResult = 0;

	// Get the selected path
	iResult = SHGetPathFromIDList(lpList, szSelected);
	if(iResult == 0)
	{
		// Free the block of memory
		CoTaskMemFree(lpList);
		mAutoMultiFileName.SetWindowText("");
		return;
	}

	// Free the block of memory
	CoTaskMemFree(lpList);

	// Return the folder selected
	mAutoMultiFileName.SetWindowText(szSelected);
}

// Manual single file
void CPk2EditorDlg::OnBnClickedButton5()
{
	// Buffers for the file chooser 
	char setDialogTitle[MAX_PATH + 1] = {0};
	char setDefFileName[MAX_PATH + 1] = {0};
	char setFilter[MAX_PATH + 1] = {0};
	char filename[MAX_PATH + 1] = {0};
	char setDirectory[MAX_PATH + 1] = {0};

	// File chooser struct
	OPENFILENAME fn = {0};

	_snprintf(setDialogTitle, MAX_PATH, "Please select a file..."); 
	_snprintf(setDefFileName, MAX_PATH, "*.*");
	_snprintf(setFilter, MAX_PATH, "All Files\0*.*\0\0");

	// Build the file chooser dialog
	fn.lStructSize = sizeof(OPENFILENAME);					// Have to set this for the struct
	fn.Flags = OFN_FILEMUSTEXIST | OFN_PATHMUSTEXIST;		// File must exist
	fn.lpstrInitialDir = setDirectory;						// Default directory
	fn.lpstrTitle = setDialogTitle;							// Title we wish to display
	fn.lpstrFile = setDefFileName;							// Tell the chooser to use our buffer
	fn.nMaxFile = MAX_PATH;									// Max size of buffer
	fn.lpstrFileTitle = filename;							// Tell the chooser to use our buffer
	fn.nMaxFileTitle = MAX_PATH;							// Max size of buffer
	fn.lpstrFilter = setFilter;								// File filter

	// Show the dialog
	if(GetOpenFileName(&fn))
	{
		mManSingleFileName.SetWindowText(fn.lpstrFile);
	}
	else
	{
		mManSingleFileName.SetWindowText("");
	}
}

// Import single auto
void CPk2EditorDlg::OnBnClickedButton2()
{
	// First make sure the pk2 access objects are ready for use
	if(reader.IsLoaded() == false || writer.IsLoaded() == false)
	{
		LogStatusMessage("There is no PK2 loaded.");
		return;
	}

	// Get the name of the file to update
	CString str;
	mAutoSingleFileName.GetWindowText(str);

	// Clear it out
	mAutoSingleFileName.SetWindowText("");

	// Convert any /'s to \'s
	std::string s = str;
	std::string title;
	size_t sze = s.size();
	for(size_t x = 0; x < sze; ++x)
	{
		if(s[x] == '/')
			s[x] = '\\';
	}

	// Find the last \ character
	size_t end = s.find_last_of('\\');
	if(end != -1)
	{
		// Save only the title portion
		title = s.substr(end + 1, s.size());
	}
	else
	{
		title = s;
	}

	// Look for the file entry
	std::vector<tPK2Entry * > set = reader.Search(title.c_str(), cPk2Reader::MODE_TITLE_ONLY);
	std::vector<tPK2Entry * > fin;

	// Make sure we get files that match exactly
	sze = set.size();
	for(size_t x = 0; x < sze; ++x)
	{
		if(strcmp(set[x]->name, title.c_str()) == 0)
		{
			fin.push_back(set[x]);
		}
	}

	// One result means we can do auto updating
	if(fin.size() == 1)
	{
		// Build the path the pk2 requires
		std::string path;
		if(strlen(fin[0]->path))
		{
			path = "\\";
			path.append(fin[0]->path);
		}
		path.append(title);

		// Try to import
		bool result = writer.ImportFile(path.c_str(), str);
		if(result)
		{
			char buf[256] = {0};
			_snprintf(buf, 255, "The file \"%s\" was successfully imported into the PK2 under the entry \"%s\".", str, path.c_str());
			LogStatusMessage(buf);
		}
	}
	// Other wise, multiple files were detected
	else if(fin.size())
	{
		char buf[256] = {0};
		size_t sze = fin.size();
		// Build the error message
		_snprintf(buf, 255, "%i files were detected for \"%s\". Only manual importing is possible. Conflicting files are:", sze, str);
		LogStatusMessage(buf);

		// List all conflicting files
		for(size_t x = 0; x < sze; ++x)
		{
			_snprintf(buf, 255, "%i. %s%s", x + 1, fin[x]->path, title);
			LogStatusMessage(buf);
		}
	}
	// Lastly, no files were detected
	else
	{
		// Log the error message
		char buf[256] = {0};
		_snprintf(buf, 255, "No files were detected for \"%s\". Only manual importing is possible.", str);
		LogStatusMessage(buf);
	}
}

// This function will find all *.ext files and call UserFunc with the file name.
void FindAllFiles(const char* searchThisDir, bool searchSubDirs, void (*UserFunc)(const char*))
{
	// What we need to search for files
	WIN32_FIND_DATA FindFileData = {0};
	HANDLE hFind = INVALID_HANDLE_VALUE;

	// Build the file search string
	char searchDir[2048] = {0};

	// If it already is a path that ends with \, only add the *
	if(searchThisDir[strlen(searchThisDir) - 1] == '\\')
	{
		_snprintf(searchDir, 2047, "%s*", searchThisDir);
	}
	// Otherwise, add the \* to the end of the path
	else
	{
		_snprintf(searchDir, 2047, "%s\\*", searchThisDir);
	}

	// Find the first file in the directory.
	hFind = FindFirstFile(searchDir, &FindFileData);

	// If there is no file, return
	if (hFind == INVALID_HANDLE_VALUE)
	{
		return;
	}

	// Find files!
	do
	{
		// If it's the "." directory, continue
		if(strcmp(FindFileData.cFileName, ".") == 0)
		{
			continue;
		}

		// If it's the ".." directory, continue
		if(strcmp(FindFileData.cFileName, "..") == 0)
		{
			continue;
		}		

		// If we find a directory
		if(FindFileData.dwFileAttributes == FILE_ATTRIBUTE_DIRECTORY)
		{
			// If we want to search subdirectories
			if(searchSubDirs)
			{
				// Holds the new directory to search
				char searchDir2[2048] = {0};

				// If it already is a path that ends with \, only add the *
				if(searchThisDir[strlen(searchThisDir) - 1] == '\\')
				{
					_snprintf(searchDir2, 2047, "%s%s", searchThisDir, FindFileData.cFileName);
				}
				// Otherwise, add the \* to the end of the path
				else
				{
					_snprintf(searchDir2, 2047, "%s\\%s", searchThisDir, FindFileData.cFileName);
				}
				FindAllFiles(searchDir2, true, UserFunc);
			}

			// Do not need to process anymore
			continue;
		}

		// Create an path to the file
		char filePath[2048] = {0};
		_snprintf(filePath, 2047, "%s\\%s", searchThisDir, FindFileData.cFileName);

		// If there is a file found, pass it to the user function
		if(UserFunc)
		{
			UserFunc(filePath);
		}
	}
	// Loop while we find more files
	while(FindNextFile(hFind, &FindFileData) != 0);

	// We are done with the finding
	FindClose(hFind);
}

// Vector of files to import
std::vector<std::string> results;

// Function to append the files found
void AppendFile(const char* file)
{
	results.push_back(file);
}

// Import Multi auto
void CPk2EditorDlg::OnBnClickedButton4()
{
	// First make sure the pk2 access objects are ready for use
	if(reader.IsLoaded() == false || writer.IsLoaded() == false)
	{
		LogStatusMessage("There is no PK2 loaded.");
		return;
	}

	// Get the name of the file to update
	CString str1;
	mAutoMultiFileName.GetWindowText(str1);

	// Clear it out
	mAutoMultiFileName.SetWindowText("");

	// Get all the files in the dir
	FindAllFiles(str1, true, AppendFile);

	// Loop through all of the files
	size_t count = results.size();
	for(size_t mainCtr = 0; mainCtr < count; ++mainCtr)
	{
		// Convert any /'s to \'s
		std::string s = results[mainCtr];
		std::string title;
		size_t sze = s.size();
		for(size_t x = 0; x < sze; ++x)
		{
			if(s[x] == '/')
				s[x] = '\\';
		}

		// Find the last \ character
		size_t end = s.find_last_of('\\');
		if(end != -1)
		{
			// Save only the title portion
			title = s.substr(end + 1, s.size());
		}
		else
		{
			title = s;
		}

		// Look for the file entry
		std::vector<tPK2Entry * > set = reader.Search(title.c_str(), cPk2Reader::MODE_TITLE_ONLY);
		std::vector<tPK2Entry * > fin;

		// Make sure we get files that match exactly
		sze = set.size();
		for(size_t x = 0; x < sze; ++x)
		{
			if(strcmp(set[x]->name, title.c_str()) == 0)
			{
				fin.push_back(set[x]);
			}
		}

		// One result means we can do auto updating
		if(fin.size() == 1)
		{
			// Build the path the pk2 requires
			std::string path;
			if(strlen(fin[0]->path))
			{
				path = "\\";
				path.append(fin[0]->path);
			}
			path.append(title);

			// Try to import
			bool result = writer.ImportFile(path.c_str(), results[mainCtr].c_str());
			if(result)
			{
				char buf[256] = {0};
				_snprintf(buf, 255, "The file \"%s\" was successfully imported into the PK2 under the entry \"%s\".", results[mainCtr].c_str(), path.c_str());
				LogStatusMessage(buf);
			}
		}
		// Other wise, multiple files were detected
		else if(fin.size())
		{
			char buf[256] = {0};
			size_t sze = fin.size();
			// Build the error message
			_snprintf(buf, 255, "%i files were detected for \"%s\". Only manual importing is possible. Conflicting files are:", sze, results[mainCtr].c_str());
			LogStatusMessage(buf);

			// List all conflicting files
			for(size_t x = 0; x < sze; ++x)
			{
				_snprintf(buf, 255, "%i. %s%s", x + 1, fin[x]->path, title);
				LogStatusMessage(buf);
			}
		}
		// Lastly, no files were detected
		else
		{
			// Log the error message
			char buf[256] = {0};
			_snprintf(buf, 255, "No files were detected for \"%s\". Only manual importing is possible.", results[mainCtr].c_str());
			LogStatusMessage(buf);
		}
	}

	// Remove the file names
	results.clear();
}

// Import manual single
void CPk2EditorDlg::OnBnClickedButton6()
{
	// First make sure the pk2 access objects are ready for use
	if(reader.IsLoaded() == false || writer.IsLoaded() == false)
	{
		LogStatusMessage("There is no PK2 loaded.");
		return;
	}

	// Get the name of the fields
	CString strPath;
	CString strName;
	CString strFile;
	mEntryName.GetWindowText(strName);
	mEntryPath.GetWindowText(strPath);
	mManSingleFileName.GetWindowText(strFile);

	size_t sze = strlen(strName);
	for(size_t x = 0; x < sze; ++x)
	{
		char ch = strName.GetAt(x);
		if(ch == '\\' || ch == '/')
		{
			LogStatusMessage("Error: The Entry Name must not have any \\ or / characters.");
			return;
		}
	}

	// Clear out the fields
	mEntryName.SetWindowText("");
	mEntryPath.SetWindowText("");
	mManSingleFileName.SetWindowText("");
	
	// Build the final entry path
	std::string s = strPath;
	if(s.size())
	{
		char lastC = s[s.size() - 1];
		if(lastC != '\\' && lastC != '/')
			s.append("\\");
	}
	s.append(strName);

	// Convert any /'s to \'s
	sze = s.size();
	for(size_t x = 0; x < sze; ++x)
	{
		if(s[x] == '/')
			s[x] = '\\';
	}

	bool result = writer.ImportFile(s.c_str(), strFile);
	if(result)
	{
		char buf[256] = {0};
		_snprintf(buf, 255, "The file \"%s\" was successfully imported into the PK2 under the entry \"%s\".", strFile, s.c_str());
		LogStatusMessage(buf);
	}
}
