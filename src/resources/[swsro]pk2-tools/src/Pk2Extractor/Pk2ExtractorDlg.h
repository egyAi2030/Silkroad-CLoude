// Pk2ExtractorDlg.h : header file
//

#pragma once
#include "afxcmn.h"
#include "afxwin.h"


// CPk2ExtractorDlg dialog
class CPk2ExtractorDlg : public CDialog
{
// Construction
public:
	CPk2ExtractorDlg(CWnd* pParent = NULL);	// standard constructor

// Dialog Data
	enum { IDD = IDD_PK2EXTRACTOR_DIALOG };

	protected:
	virtual void DoDataExchange(CDataExchange* pDX);	// DDX/DDV support

	void OnOK();
	void OnCancel();

	void OnExitFunc();
	void OnOpenFunc();
	void OnCloseFunc();

	void UpdateSearchResults();

// Implementation
protected:
	HICON m_hIcon;

	// Generated message map functions
	virtual BOOL OnInitDialog();
	afx_msg void OnSysCommand(UINT nID, LPARAM lParam);
	afx_msg void OnPaint();
	afx_msg HCURSOR OnQueryDragIcon();
	DECLARE_MESSAGE_MAP()
public:
	afx_msg void OnClose();
	CTreeCtrl mTreeView;
	afx_msg void OnBnClickedButton2();
	afx_msg void OnBnClickedButton1();
	afx_msg void OnTvnSelchangedTree1(NMHDR *pNMHDR, LRESULT *pResult);
	CEdit mEditName;
	CEdit mEditPath;
	CEdit mEditSize;
	afx_msg void OnBnClickedButton3();
	CButton mSearchBtn;
	CListBox mListBox;
	int mFileTypeRadio;
	int mFolderTypeRadio;
	CEdit mMinSizeEdit;
	CEdit mMaxSizeEdit;
	int mAnyStr;
	int mTitleStr;
	int mPathStr;
	CEdit mSearchStr;
	CStatic mGS1;
	CButton mStrNewSearch;
	CButton mSizeNewSearch;
	CButton mTypeNewSearch;
	CButton mStrFilter;
	CButton mSizeFilter;
	CButton mTypeFilter;
	CStatic mStatic1;
	CStatic mStatic2;
	CStatic mStatic3;
	CStatic mStatic4;
	CStatic mStatic5;
	afx_msg void OnBnClickedButton4();
	afx_msg void OnBnClickedButton5();
	afx_msg void OnBnClickedButton6();
	afx_msg void OnBnClickedButton7();
	afx_msg void OnBnClickedButton8();
	afx_msg void OnBnClickedButton9();
	CButton mRadio1;
	CButton mRadio2;
	CButton mRadio3;
	afx_msg void OnBnClickedRadio6();
	afx_msg void OnBnClickedRadio5();
	afx_msg void OnBnClickedRadio4();
	afx_msg void OnLbnSelchangeList1();
	CButton mRadio4;
	CButton mRadio5;
	afx_msg void OnBnClickedRadio9();
	afx_msg void OnBnClickedRadio8();
};
