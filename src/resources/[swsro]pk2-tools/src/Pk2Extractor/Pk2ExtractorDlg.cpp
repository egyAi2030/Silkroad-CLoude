// Pk2ExtractorDlg.cpp : implementation file
#include "stdafx.h"
#include "Pk2Extractor.h"
#include "Pk2ExtractorDlg.h"
#include ".\pk2extractordlg.h"

#include "../common/pk2Reader.h"

#include "mmsystem.h"
#pragma comment(lib, "winmm.lib")

#ifdef _DEBUG
	#define new DEBUG_NEW
#endif

#include <stack>

//-------------------------------------------------------------------------------------------------

// Log a status message to an edit control
void LogStatusMessage(const char * text);

// Main dialog hwnd
HWND gDialogHwnd = 0;
HWND gDialogLog = 0;
HWND gDialogExtract = 0;
HWND gDialogExtractAll = 0;
HWND gDialogSearch = 0;

// PK2 reading object
cPk2Reader reader;

// Current directory
char currentDir[MAX_PATH + 1] = {0};

bool searchMode = false;

// Search results
std::vector<tPK2Entry *> resultSet;

//-------------------------------------------------------------------------------------------------

void OnError(const char * function, const char * error)
{
	char buffer[32768] = {0};
	_snprintf(buffer, 32767, "%s: %s", function, error);
	LogStatusMessage(buffer);
}

//-------------------------------------------------------------------------------------------------

// CPk2ExtractorDlg dialog
CPk2ExtractorDlg::CPk2ExtractorDlg(CWnd* pParent /*=NULL*/)
	: CDialog(CPk2ExtractorDlg::IDD, pParent)
	, mFileTypeRadio(0)
	, mFolderTypeRadio(0)
	, mAnyStr(0)
	, mTitleStr(0)
	, mPathStr(0)
{
	m_hIcon = AfxGetApp()->LoadIcon(IDI_ICON1);
}

void CPk2ExtractorDlg::DoDataExchange(CDataExchange* pDX)
{
	CDialog::DoDataExchange(pDX);
	DDX_Control(pDX, IDC_TREE1, mTreeView);
	DDX_Control(pDX, IDC_EDIT1, mEditName);
	DDX_Control(pDX, IDC_EDIT2, mEditPath);
	DDX_Control(pDX, IDC_EDIT3, mEditSize);
	DDX_Control(pDX, IDC_BUTTON3, mSearchBtn);
	DDX_Control(pDX, IDC_LIST1, mListBox);
	DDX_Control(pDX, IDC_EDIT6, mMinSizeEdit);
	DDX_Control(pDX, IDC_EDIT7, mMaxSizeEdit);
	DDX_Control(pDX, IDC_EDIT5, mSearchStr);
	DDX_Control(pDX, IDC_BUTTON4, mStrNewSearch);
	DDX_Control(pDX, IDC_BUTTON6, mSizeNewSearch);
	DDX_Control(pDX, IDC_BUTTON8, mTypeNewSearch);
	DDX_Control(pDX, IDC_BUTTON5, mStrFilter);
	DDX_Control(pDX, IDC_BUTTON7, mSizeFilter);
	DDX_Control(pDX, IDC_BUTTON9, mTypeFilter);
	DDX_Control(pDX, IDC_STATIC_1, mStatic1);
	DDX_Control(pDX, IDC_STATIC_2, mStatic2);
	DDX_Control(pDX, IDC_STATIC_3, mStatic3);
	DDX_Control(pDX, IDC_STATIC_4, mStatic4);
	DDX_Control(pDX, IDC_STATIC_5, mStatic5);
	DDX_Control(pDX, IDC_RADIO6, mRadio1);
	DDX_Control(pDX, IDC_RADIO5, mRadio2);
	DDX_Control(pDX, IDC_RADIO4, mRadio3);
	DDX_Control(pDX, IDC_RADIO9, mRadio4);
	DDX_Control(pDX, IDC_RADIO8, mRadio5);
}


BEGIN_MESSAGE_MAP(CPk2ExtractorDlg, CDialog)
	ON_WM_SYSCOMMAND()
	ON_WM_PAINT()
	ON_WM_QUERYDRAGICON()
	//}}AFX_MSG_MAP
	ON_WM_CLOSE()
	ON_COMMAND(ID_FILE_EXIT, OnExitFunc)
	ON_COMMAND(ID_FILE_OPEN32771, OnOpenFunc)
	ON_COMMAND(ID_FILE_CLOSE32772, OnCloseFunc)
	ON_BN_CLICKED(IDC_BUTTON2, OnBnClickedButton2)
	ON_BN_CLICKED(IDC_BUTTON1, OnBnClickedButton1)
	ON_NOTIFY(TVN_SELCHANGED, IDC_TREE1, OnTvnSelchangedTree1)
	ON_BN_CLICKED(IDC_BUTTON3, OnBnClickedButton3)
	ON_BN_CLICKED(IDC_BUTTON4, OnBnClickedButton4)
	ON_BN_CLICKED(IDC_BUTTON5, OnBnClickedButton5)
	ON_BN_CLICKED(IDC_BUTTON6, OnBnClickedButton6)
	ON_BN_CLICKED(IDC_BUTTON7, OnBnClickedButton7)
	ON_BN_CLICKED(IDC_BUTTON8, OnBnClickedButton8)
	ON_BN_CLICKED(IDC_BUTTON9, OnBnClickedButton9)
	ON_BN_CLICKED(IDC_RADIO6, OnBnClickedRadio6)
	ON_BN_CLICKED(IDC_RADIO5, OnBnClickedRadio5)
	ON_BN_CLICKED(IDC_RADIO4, OnBnClickedRadio4)
	ON_LBN_SELCHANGE(IDC_LIST1, OnLbnSelchangeList1)
	ON_BN_CLICKED(IDC_RADIO9, OnBnClickedRadio9)
	ON_BN_CLICKED(IDC_RADIO8, OnBnClickedRadio8)
END_MESSAGE_MAP()


// CPk2ExtractorDlg message handlers
BOOL CPk2ExtractorDlg::OnInitDialog()
{
	CDialog::OnInitDialog();

	// Set the icon for this dialog.  The framework does this automatically
	//  when the application's main window is not a dialog
	SetIcon(m_hIcon, TRUE);			// Set big icon
	SetIcon(m_hIcon, FALSE);		// Set small icon

	// Store the dialogs hwnd
	gDialogHwnd = GetSafeHwnd();
	gDialogLog = ::GetDlgItem(gDialogHwnd, IDC_EDIT4);
	gDialogExtract = ::GetDlgItem(gDialogHwnd, IDC_BUTTON2);
	gDialogExtractAll = ::GetDlgItem(gDialogHwnd, IDC_BUTTON1);
	gDialogSearch = ::GetDlgItem(gDialogHwnd, IDC_BUTTON3);

	// Reserve space
	resultSet.reserve(60000);

	// Set window title
	this->SetWindowText("PK2 Extractor");

	// Register the error function
	reader.SetOnErrorFunction(OnError);

	// Log some status messages
	LogStatusMessage("Welcome to the PK2 Extractor!");
	LogStatusMessage("Made by: Drew Benton");
	LogStatusMessage("http://0x33.org");
	LogStatusMessage("");
	LogStatusMessage("");

	// Store the current directory
	GetCurrentDirectory(MAX_PATH, currentDir);

	// Initialize the reader object
	reader.Initialize();

	// Default checks
	mRadio1.SetCheck(BST_CHECKED);
	mRadio4.SetCheck(BST_CHECKED);

	// return TRUE  unless you set the focus to a control
	return TRUE;
}

void CPk2ExtractorDlg::OnSysCommand(UINT nID, LPARAM lParam)
{
	CDialog::OnSysCommand(nID, lParam);
}

// If you add a minimize button to your dialog, you will need the code below
//  to draw the icon.  For MFC applications using the document/view model,
//  this is automatically done for you by the framework.
void CPk2ExtractorDlg::OnPaint() 
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
HCURSOR CPk2ExtractorDlg::OnQueryDragIcon()
{
	return static_cast<HCURSOR>(m_hIcon);
}

//-------------------------------------------------------------------------------------------------

void CPk2ExtractorDlg::OnOK()
{
}

void CPk2ExtractorDlg::OnCancel()
{
}

void CPk2ExtractorDlg::OnClose()
{
	OnExitFunc();
}

void CPk2ExtractorDlg::OnExitFunc()
{
	if(MessageBox("Do you really wish to exit?", "Exit confirmation", MB_YESNO | MB_ICONQUESTION) == IDYES)
	{
		reader.Deinitialize();
		CDialog::OnOK();
	}
}

void CPk2ExtractorDlg::OnOpenFunc()
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
		result = reader.Open(fn.lpstrFile);
		if(result)
		{
			// Return all of the entries in the pk2 file
			std::vector<tPK2Entry *> entries = reader.GetAllEntries();

			// Prestore
			size_t size1 = entries.size();

			// Remove old items
			mTreeView.DeleteAllItems();

			HTREEITEM root = mTreeView.InsertItem(reader.GetPK2Name().c_str(), TVI_ROOT);
			HTREEITEM current = root;
			HTREEITEM child = root;

			std::stack<HTREEITEM> roots;
			roots.push(root);

			int currentLevel = 0;
			bool skip = false;

			// Loop through all of the entries
			for(size_t x = 0; x < size1; ++x)
			{
				std::string s = entries[x]->name;
				if(entries[x]->level == currentLevel)
				{
					if(s.size() && x && strlen(entries[x - 1]->name) == 0)
					{
						skip = true;
					}
				}
				else if(entries[x]->level > currentLevel)
				{
					skip = false;
					roots.push(current);
					currentLevel++;
				}
				else if(entries[x]->level < currentLevel)
				{
					skip = false;
					roots.pop();
					current = roots.top();
					currentLevel--;
				}
				if(s.size())
				{
					if(!skip && s != "." && s != "..")
					{
						current = mTreeView.InsertItem(entries[x]->name, roots.top(), current);
						mTreeView.SetItemData(current, (DWORD_PTR)entries[x]);
					}
				}
			}

			// Expand the root
			mTreeView.Expand(mTreeView.GetRootItem(), TVE_EXPAND);

			// Enable the buttons
			::EnableWindow(gDialogExtractAll, TRUE);
			::EnableWindow(gDialogSearch, TRUE);

			// Status
			LogStatusMessage("The PK2 has been loaded successfully.");

			// Set window title
			this->SetWindowText("PK2 Extractor - Explore Mode");
		}
	}
}

//-------------------------------------------------------------------------------------------------

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

void CPk2ExtractorDlg::OnBnClickedButton2()
{
	// Make sure a file is loaded first
	if(reader.IsLoaded() == false)
	{
		LogStatusMessage("Please load a PK2 file first.");
		return;
	}

	if(searchMode == true)
	{
		int sel = mListBox.GetCurSel();
		if(sel >= 0 && sel < (int)resultSet.size())
		{
			tPK2Entry * entry = resultSet[sel];
			if(entry)
			{
				if(entry->type == 1)
				{
					int start = 0;
					int level = -1;

					// Return all of the entries in the pk2 file
					std::vector<tPK2Entry *> entries = reader.GetAllEntries();
					std::vector<tPK2Entry *> set;
					for(size_t x = 0; x < entries.size(); ++x)
					{
						if(entries[x] == entry)
						{
							start = (int)x;
							level = entries[x]->level;
							set.push_back(entries[x]);
						}
						else if(start)
						{
							if(entries[x]->level <= level)
							{
								break;
							}
							set.push_back(entries[x]);
						}
					}
					LogStatusMessage("Beginning to extract files...Please Wait");
					bool result = reader.Extract(currentDir, set);
					if(result == true)
					{
						char msg[256] = {0};
						_snprintf(msg, 255, "The folder %s and all of its contents were extracted successfully.", entry->name);
						LogStatusMessage(msg);
						PlaySound((LPCTSTR)SND_ALIAS_SYSTEMASTERISK, NULL, SND_ALIAS_ID | SND_ASYNC);
					}
				}
				else if(entry->type == 2)
				{
					bool result = reader.Extract(currentDir, entry);
					if(result  == true)
					{
						char msg[256] = {0};
						_snprintf(msg, 255, "%s was extracted successfully.", entry->name);
						LogStatusMessage(msg);
					}
				}
			}
		}
	}
	else if(searchMode == false)
	{
		HTREEITEM hItem = mTreeView.GetSelectedItem();
		if(hItem == NULL)
		{
			LogStatusMessage("Please select an item to extract.");
			return;
		}

		tPK2Entry * entry = (tPK2Entry *)mTreeView.GetItemData(hItem);
		if(entry == NULL)
		{
			LogStatusMessage("This item cannot be extracted");
			return;
		}

		if(entry->type == 1)
		{
			int start = 0;
			int level = -1;

			// Return all of the entries in the pk2 file
			std::vector<tPK2Entry *> entries = reader.GetAllEntries();
			std::vector<tPK2Entry *> set;
			for(size_t x = 0; x < entries.size(); ++x)
			{
				if(entries[x] == entry)
				{
					start = (int)x;
					level = entries[x]->level;
					set.push_back(entries[x]);
				}
				else if(start)
				{
					if(entries[x]->level <= level)
					{
						break;
					}
					set.push_back(entries[x]);
				}
			}
			LogStatusMessage("Beginning to extract files...Please Wait");
			bool result = reader.Extract(currentDir, set);
			if(result == true)
			{
				char msg[256] = {0};
				_snprintf(msg, 255, "The folder %s and all of its contents were extracted successfully.", entry->name);
				LogStatusMessage(msg);
				PlaySound((LPCTSTR)SND_ALIAS_SYSTEMASTERISK, NULL, SND_ALIAS_ID | SND_ASYNC);
			}
		}
		else if(entry->type == 2)
		{
			bool result = reader.Extract(currentDir, entry);
			if(result == true)
			{
				char msg[256] = {0};
				_snprintf(msg, 255, "%s was extracted successfully.", entry->name);
				LogStatusMessage(msg);
			}
		}
	}
}

void CPk2ExtractorDlg::OnBnClickedButton1()
{
	// Make sure a file is loaded first
	if(reader.IsLoaded() == false)
	{
		LogStatusMessage("Please load a PK2 file first.");
		return;
	}

	if(searchMode == true)
	{
		LogStatusMessage("Beginning to extract files...Please Wait");
		for(size_t x = 0; x < resultSet.size(); ++x)
		{
			tPK2Entry * entry = resultSet[x];
			if(entry)
			{
				bool result = reader.Extract(currentDir, entry);
				if(result  == true)
				{
					char msg[256] = {0};
					_snprintf(msg, 255, "%s was extracted successfully.", entry->name);
					LogStatusMessage(msg);
				}
			}
		}
		LogStatusMessage("All of the PK2 entries were extracted.");
		PlaySound((LPCTSTR)SND_ALIAS_SYSTEMASTERISK, NULL, SND_ALIAS_ID | SND_ASYNC);
	}
	else if(searchMode == false)
	{
		// Make sure they want to extract all the files
		if(MessageBox("Are you sure you wish to extract all of the PK2 entries?", "Confirmation", MB_ICONQUESTION | MB_YESNO) == IDYES)
		{
			LogStatusMessage("Beginning to extract files...Please Wait");
			bool result = reader.Extract(currentDir);
			if(result == true)
			{
				LogStatusMessage("All of the PK2 entries were extracted.");
				PlaySound((LPCTSTR)SND_ALIAS_SYSTEMASTERISK, NULL, SND_ALIAS_ID | SND_ASYNC);
			}
		}
	}
}

void CPk2ExtractorDlg::OnTvnSelchangedTree1(NMHDR *pNMHDR, LRESULT *pResult)
{
	// Get the select item
	HTREEITEM hItem = mTreeView.GetSelectedItem();
	if(hItem)
	{
		tPK2Entry * entry = (tPK2Entry *)mTreeView.GetItemData(hItem);
		if(entry)
		{
			::EnableWindow(gDialogExtract, TRUE);

			// Name of the entry
			this->mEditName.SetWindowText(entry->name);

			// Path to the entry
			this->mEditPath.SetWindowText(entry->path);

			// Entry size
			char buffer[256] = {0};
			_snprintf(buffer, 255, "%i bytes", entry->size);
			if(entry->size)
				this->mEditSize.SetWindowText(buffer);
			else
				this->mEditSize.SetWindowText("");
		}
		else
		{
			::EnableWindow(gDialogExtract, FALSE);
			this->mEditName.SetWindowText("");
			this->mEditPath.SetWindowText("");
			this->mEditSize.SetWindowText("");
		}
	}
}

void CPk2ExtractorDlg::OnCloseFunc()
{
	// Set window title
	this->SetWindowText("PK2 Extractor");
	::EnableWindow(gDialogExtract, FALSE);
	::EnableWindow(gDialogExtractAll, FALSE);
	::EnableWindow(gDialogSearch, FALSE);
	this->mEditName.SetWindowText("");
	this->mEditPath.SetWindowText("");
	this->mEditSize.SetWindowText("");
	mTreeView.DeleteAllItems();
	if(reader.IsLoaded())
	{
		reader.Close();
		// Status
		LogStatusMessage("The PK2 has been closed successfully.");
	}

	// Switch modes
	resultSet.clear();
	mTreeView.ShowWindow(SW_SHOW);
	mSearchBtn.SetWindowText("Search Mode");
	mListBox.ShowWindow(SW_HIDE);
	searchMode = false;
	//
	mSearchStr.ShowWindow(SW_HIDE);
	mMinSizeEdit.ShowWindow(SW_HIDE);
	mMaxSizeEdit.ShowWindow(SW_HIDE);
	mStrNewSearch.ShowWindow(SW_HIDE);
	mSizeNewSearch.ShowWindow(SW_HIDE);
	mTypeNewSearch.ShowWindow(SW_HIDE);
	mStrFilter.ShowWindow(SW_HIDE);
	mSizeFilter.ShowWindow(SW_HIDE);
	mTypeFilter.ShowWindow(SW_HIDE);
	mStatic1.ShowWindow(SW_HIDE);
	mStatic2.ShowWindow(SW_HIDE);
	mStatic3.ShowWindow(SW_HIDE);
	mStatic4.ShowWindow(SW_HIDE);
	mStatic5.ShowWindow(SW_HIDE);
	GetDlgItem(IDC_RADIO6)->ShowWindow(FALSE);
	GetDlgItem(IDC_RADIO5)->ShowWindow(FALSE);
	GetDlgItem(IDC_RADIO4)->ShowWindow(FALSE);
	GetDlgItem(IDC_RADIO9)->ShowWindow(FALSE);
	GetDlgItem(IDC_RADIO8)->ShowWindow(FALSE);
}

void CPk2ExtractorDlg::OnBnClickedButton3()
{
	char name[256] = {0};
	mSearchBtn.GetWindowText(name, 255);
	::EnableWindow(gDialogExtract, FALSE);
	if(strcmp(name, "Search Mode") == 0)
	{
		// Reset the search form
		resultSet.clear();
		// Remove all current items
		int count = mListBox.GetCount();
		for(int x = 0; x < count; ++x)
		{
			mListBox.DeleteString(0);
		}
		mSearchStr.SetWindowText("");
		mMinSizeEdit.SetWindowText("");
		mMaxSizeEdit.SetWindowText("");
		//
		mRadio1.SetCheck(BST_CHECKED);
		mRadio2.SetCheck(BST_UNCHECKED);
		mRadio3.SetCheck(BST_UNCHECKED);
		//
		mRadio4.SetCheck(BST_CHECKED);
		mRadio5.SetCheck(BST_UNCHECKED);
		//
		mTreeView.ShowWindow(SW_HIDE);
		mSearchBtn.SetWindowText("Explore Mode");
		this->SetWindowText("PK2 Extractor - Search Mode");
		mListBox.ShowWindow(SW_SHOW);
		searchMode = true;
		//
		mSearchStr.ShowWindow(SW_SHOW);
		mMinSizeEdit.ShowWindow(SW_SHOW);
		mMaxSizeEdit.ShowWindow(SW_SHOW);
		mStrNewSearch.ShowWindow(SW_SHOW);
		mSizeNewSearch.ShowWindow(SW_SHOW);
		mTypeNewSearch.ShowWindow(SW_SHOW);
		mStrFilter.ShowWindow(SW_SHOW);
		mSizeFilter.ShowWindow(SW_SHOW);
		mTypeFilter.ShowWindow(SW_SHOW);
		mStatic1.ShowWindow(SW_SHOW);
		mStatic2.ShowWindow(SW_SHOW);
		mStatic3.ShowWindow(SW_SHOW);
		mStatic4.ShowWindow(SW_SHOW);
		mStatic5.ShowWindow(SW_SHOW);
		GetDlgItem(IDC_RADIO6)->ShowWindow(TRUE);
		GetDlgItem(IDC_RADIO5)->ShowWindow(TRUE);
		GetDlgItem(IDC_RADIO4)->ShowWindow(TRUE);
		GetDlgItem(IDC_RADIO9)->ShowWindow(TRUE);
		GetDlgItem(IDC_RADIO8)->ShowWindow(TRUE);
	}
	else if(strcmp(name, "Explore Mode") == 0)
	{
		resultSet.clear();
		mTreeView.ShowWindow(SW_SHOW);
		mSearchBtn.SetWindowText("Search Mode");
		this->SetWindowText("PK2 Extractor - Explore Mode");
		mListBox.ShowWindow(SW_HIDE);
		searchMode = false;
		//
		mSearchStr.ShowWindow(SW_HIDE);
		mMinSizeEdit.ShowWindow(SW_HIDE);
		mMaxSizeEdit.ShowWindow(SW_HIDE);
		mStrNewSearch.ShowWindow(SW_HIDE);
		mSizeNewSearch.ShowWindow(SW_HIDE);
		mTypeNewSearch.ShowWindow(SW_HIDE);
		mStrFilter.ShowWindow(SW_HIDE);
		mSizeFilter.ShowWindow(SW_HIDE);
		mTypeFilter.ShowWindow(SW_HIDE);
		mStatic1.ShowWindow(SW_HIDE);
		mStatic2.ShowWindow(SW_HIDE);
		mStatic3.ShowWindow(SW_HIDE);
		mStatic4.ShowWindow(SW_HIDE);
		mStatic5.ShowWindow(SW_HIDE);
		GetDlgItem(IDC_RADIO6)->ShowWindow(FALSE);
		GetDlgItem(IDC_RADIO5)->ShowWindow(FALSE);
		GetDlgItem(IDC_RADIO4)->ShowWindow(FALSE);
		GetDlgItem(IDC_RADIO9)->ShowWindow(FALSE);
		GetDlgItem(IDC_RADIO8)->ShowWindow(FALSE);
	}
}

void CPk2ExtractorDlg::UpdateSearchResults()
{
	// Remove all current items
	int count = mListBox.GetCount();
	for(int x = 0; x < count; ++x)
	{
		mListBox.DeleteString(0);
	}

	// Add new items
	for(size_t x = 0; x < resultSet.size(); ++x)
	{
		mListBox.AddString(resultSet[x]->name);
	}

	if(resultSet.size())
	{
		mListBox.SetCurSel(0);
		OnLbnSelchangeList1();
	}
	else
	{
		::EnableWindow(gDialogExtract, FALSE);
		this->mEditName.SetWindowText("");
		this->mEditPath.SetWindowText("");
		this->mEditSize.SetWindowText("");
	}
}

// String new search
void CPk2ExtractorDlg::OnBnClickedButton4()
{
	cPk2Reader::SearchMode_String mode;
	CString str;

	// Store the search string
	mSearchStr.GetWindowText(str);

	if(mRadio3.GetCheck() == BST_CHECKED)
		mode = cPk2Reader::MODE_PATH_ONLY;
	else if(mRadio2.GetCheck() == BST_CHECKED)
		mode = cPk2Reader::MODE_TITLE_ONLY;
	else
		mode = cPk2Reader::MODE_ANY;

	// Perform the search
	resultSet = reader.Search(str, mode);
	
	// Update list view
	UpdateSearchResults();
}

// String filter
void CPk2ExtractorDlg::OnBnClickedButton5()
{
	cPk2Reader::SearchMode_String mode;
	CString str;

	// Store the search string
	mSearchStr.GetWindowText(str);

	if(mRadio3.GetCheck() == BST_CHECKED)
		mode = cPk2Reader::MODE_PATH_ONLY;
	else if(mRadio2.GetCheck() == BST_CHECKED)
		mode = cPk2Reader::MODE_TITLE_ONLY;
	else
		mode = cPk2Reader::MODE_ANY;

	// Perform the filter search
	std::vector<tPK2Entry *> tmp = reader.Search(resultSet, str, mode);
	resultSet = tmp;

	// Update list view
	UpdateSearchResults();
}

// Size new search
void CPk2ExtractorDlg::OnBnClickedButton6()
{
	// Temp string
	CString str;

	// Store minx
	mMinSizeEdit.GetWindowText(str);
	if(str=="")
		str = "-1";
	int min = atoi(str);

	// Store max
	mMaxSizeEdit.GetWindowText(str);
	if(str=="")
		str = "-1";
	int max = atoi(str);

	// Perform the search
	resultSet = reader.Search(min, max);

	// Update list view
	UpdateSearchResults();
}

// Size filter
void CPk2ExtractorDlg::OnBnClickedButton7()
{
	// Temp string
	CString str;

	// Store minx
	mMinSizeEdit.GetWindowText(str);
	if(str=="")
		str = "-1";
	int min = atoi(str);

	// Store max
	mMaxSizeEdit.GetWindowText(str);
	if(str=="")
		str = "-1";
	int max = atoi(str);

	// Perform the search
	std::vector<tPK2Entry *> tmp = reader.Search(resultSet, min, max);
	resultSet = tmp;

	// Update list view
	UpdateSearchResults();
}

// Type new search
void CPk2ExtractorDlg::OnBnClickedButton8()
{
	int type = 0;
	if(mRadio4.GetCheck() == BST_CHECKED)
		type = 2;
	else if(mRadio5.GetCheck() == BST_CHECKED)
		type = 1;

	// Perform the search
	resultSet = reader.Search(type);

	// Update list view
	UpdateSearchResults();
}

// Type filter
void CPk2ExtractorDlg::OnBnClickedButton9()
{
	int type = 0;
	if(mRadio4.GetCheck() == BST_CHECKED)
		type = 2;
	else if(mRadio5.GetCheck() == BST_CHECKED)
		type = 1;

	// Perform the search
	std::vector<tPK2Entry *> tmp = reader.Search(resultSet, type);
	resultSet = tmp;

	// Update list view
	UpdateSearchResults();
}

void CPk2ExtractorDlg::OnBnClickedRadio6()
{
	mRadio1.SetCheck(BST_CHECKED);
	mRadio2.SetCheck(BST_UNCHECKED);
	mRadio3.SetCheck(BST_UNCHECKED);
}

void CPk2ExtractorDlg::OnBnClickedRadio5()
{
	mRadio2.SetCheck(BST_CHECKED);
	mRadio1.SetCheck(BST_UNCHECKED);
	mRadio3.SetCheck(BST_UNCHECKED);
}

void CPk2ExtractorDlg::OnBnClickedRadio4()
{
	mRadio3.SetCheck(BST_CHECKED);
	mRadio1.SetCheck(BST_UNCHECKED);
	mRadio2.SetCheck(BST_UNCHECKED);
}


void CPk2ExtractorDlg::OnLbnSelchangeList1()
{
	int sel = mListBox.GetCurSel();
	if(sel >= 0 && sel < (int)resultSet.size())
	{
		tPK2Entry * entry = resultSet[sel];
		if(entry)
		{
			::EnableWindow(gDialogExtract, TRUE);

			// Name of the entry
			this->mEditName.SetWindowText(entry->name);

			// Path to the entry
			this->mEditPath.SetWindowText(entry->path);

			// Entry size
			char buffer[256] = {0};
			_snprintf(buffer, 255, "%i bytes", entry->size);
			if(entry->size)
				this->mEditSize.SetWindowText(buffer);
			else
				this->mEditSize.SetWindowText("");
		}
		else
		{
			::EnableWindow(gDialogExtract, FALSE);
			this->mEditName.SetWindowText("");
			this->mEditPath.SetWindowText("");
			this->mEditSize.SetWindowText("");
		}
	}
}

void CPk2ExtractorDlg::OnBnClickedRadio9()
{
	mRadio4.SetCheck(BST_CHECKED);
	mRadio5.SetCheck(BST_UNCHECKED);
}

void CPk2ExtractorDlg::OnBnClickedRadio8()
{
	mRadio5.SetCheck(BST_CHECKED);
	mRadio4.SetCheck(BST_UNCHECKED);
}
