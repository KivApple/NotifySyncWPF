#include "pch.h"
#include "CNotifySyncShellExt.h"

CNotifySyncShellExt::CNotifySyncShellExt() {
	m_fileNameCount = 0;
	m_fileNames = NULL;
}

HRESULT CNotifySyncShellExt::FinalConstruct() {
	return S_OK;
}

void CNotifySyncShellExt::FinalRelease() {
	for (UINT i = 0; i < m_fileNameCount; i++) {
		delete[] m_fileNames[i];
	}
	delete[] m_fileNames;
	m_fileNames = NULL;
	m_fileNameCount = 0;
}

STDMETHODIMP CNotifySyncShellExt::Initialize(LPCITEMIDLIST pidlFolder, LPDATAOBJECT pDataObj, HKEY hProgID) {
	FORMATETC fmt = { CF_HDROP, NULL, DVASPECT_CONTENT, -1, TYMED_HGLOBAL };
	STGMEDIUM stg = { TYMED_HGLOBAL };
	if (FAILED(pDataObj->GetData(&fmt, &stg))) {
		return E_INVALIDARG;
	}
	HDROP hDrop = (HDROP) GlobalLock(stg.hGlobal);
	if (hDrop == NULL) {
		return E_INVALIDARG;
	}
	HRESULT result = S_OK;
	m_fileNameCount = DragQueryFile(hDrop, 0xFFFFFFFF, NULL, 0);
	if (m_fileNameCount > 0) {
		m_fileNames = new TCHAR*[m_fileNameCount];
		for (UINT i = 0; i < m_fileNameCount; i++) {
			m_fileNames[i] = new TCHAR[MAX_PATH];
			if (DragQueryFile(hDrop, i, m_fileNames[i], MAX_PATH) == 0) {
				result = E_INVALIDARG;
				break;
			}
		}
	} else {
		result = E_INVALIDARG;
	}
	GlobalUnlock(stg.hGlobal);
	ReleaseStgMedium(&stg);
	return result;
}

STDMETHODIMP CNotifySyncShellExt::QueryContextMenu(HMENU hMenu, UINT uMenuIndex, UINT uidFirstCmd, UINT uidLastCmd, UINT uFlags) {
	if (uFlags & CMF_DEFAULTONLY) {
		return MAKE_HRESULT(SEVERITY_SUCCESS, FACILITY_NULL, 0);
	}
	HMENU hSubMenu = CreateMenu();
	AppendMenu(hSubMenu, MF_GRAYED, 0, _T("Not connected to any device"));
	// TODO
	InsertMenu(hMenu, uMenuIndex, MF_BYPOSITION | MF_POPUP, (UINT_PTR) hSubMenu, _T("NotifySync"));
	return MAKE_HRESULT(SEVERITY_SUCCESS, FACILITY_NULL, 1);
}

STDMETHODIMP CNotifySyncShellExt::GetCommandString(UINT_PTR idCmd, UINT uFlags, UINT* pwReserved, LPSTR pszName, UINT cchMax) {
	return S_OK;
}

STDMETHODIMP CNotifySyncShellExt::InvokeCommand(LPCMINVOKECOMMANDINFO pCmdInfo) {
	if (m_fileNameCount == 0) {
		return E_INVALIDARG;
	}
	MessageBox(pCmdInfo->hwnd, m_fileNames[0], _T("NotifySync"), MB_ICONINFORMATION);
	return S_OK;
}
