// Pk2Editor.h : main header file for the PROJECT_NAME application
//

#pragma once

#ifndef __AFXWIN_H__
	#error include 'stdafx.h' before including this file for PCH
#endif

#include "resource.h"		// main symbols


// CPk2EditorApp:
// See Pk2Editor.cpp for the implementation of this class
//

class CPk2EditorApp : public CWinApp
{
public:
	CPk2EditorApp();

// Overrides
	public:
	virtual BOOL InitInstance();

// Implementation

	DECLARE_MESSAGE_MAP()
};

extern CPk2EditorApp theApp;