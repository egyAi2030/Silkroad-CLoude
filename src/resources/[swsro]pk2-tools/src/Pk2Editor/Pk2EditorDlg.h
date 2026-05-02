// Pk2EditorDlg.h : header file
//

#pragma once
#include "afxwin.h"


// CPk2EditorDlg dialog
class CPk2EditorDlg : public CDialog
{
// Construction
public:
	CPk2EditorDlg(CWnd* pParent = NULL);	// standard constructor

// Dialog Data
	enum { IDD = IDD_PK2EDITOR_DIALOG };

	protected:
	virtual void DoDataExchange(CDataExchange* pDX);	// DDX/DDV support

	void OnCancel();
	void OnOK();

	void OnExitFunc();
	void OnOpenFunc();
	void OnCloseFunc();

// Implementation
protected:
	HICON m_hIcon;

	// Generated message map functions
	virtual BOOL OnInitDialog();
	afx_msg void OnPaint();
	afx_msg HCURSOR OnQueryDragIcon();
	DECLARE_MESSAGE_MAP()
public:
	afx_msg void OnClose();
	CEdit mAutoSingleFileName;
	CEdit mAutoMultiFileName;
	CEdit mManSingleFileName;
	CEdit mEntryPath;
	CEdit mEntryName;
	afx_msg void OnBnClickedButton1();
	afx_msg void OnBnClickedButton3();
	afx_msg void OnBnClickedButton5();
	afx_msg void OnBnClickedButton2();
	afx_msg void OnBnClickedButton4();
	afx_msg void OnBnClickedButton6();
	afx_msg void OnBnClickedButton7();
	CButton mImport1;
	CButton mImport2;
	CButton mImport3;
	CButton mSel1;
	CButton mSel2;
	CButton mSel3;
};
