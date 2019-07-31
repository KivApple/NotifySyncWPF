#include "pch.h"
#include "CNotifySyncShellExt.h"

#define NAMED_PIPE_NAME "\\\\.\\pipe\\NotifySync"

CNotifySyncShellExt::CNotifySyncShellExt() {
	m_fileNameCount = 0;
	m_fileNames = NULL;
	m_deviceCount = 0;
	m_deviceIds = NULL;
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
	for (UINT i = 0; i < m_deviceCount; i++) {
		delete[] m_deviceIds[i];
	}
	delete[] m_deviceIds;
	m_deviceIds = NULL;
	m_deviceCount = 0;
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
	for (UINT i = 0; i < m_fileNameCount; i++) {
		DWORD attrs = GetFileAttributes(m_fileNames[i]);
		if (attrs & FILE_ATTRIBUTE_DIRECTORY) {
			return MAKE_HRESULT(SEVERITY_SUCCESS, FACILITY_NULL, 0);
		}
	}
	HMENU hSubMenu = CreateMenu();
	HANDLE hPipe = CreateFile(TEXT(NAMED_PIPE_NAME), GENERIC_READ | GENERIC_WRITE, 0, NULL, OPEN_EXISTING, 0, NULL);
	if (hPipe != INVALID_HANDLE_VALUE) {
		WriteString(hPipe, TEXT("device-list"));
		m_deviceCount = ReadUInt(hPipe);
		m_deviceIds = new TCHAR*[m_deviceCount];
		for (UINT i = 0; i < m_deviceCount; i++) {
			m_deviceIds[i] = ReadString(hPipe);
			TCHAR* deviceName = ReadString(hPipe);
			TCHAR buffer[512];
			wnsprintf(buffer, sizeof(buffer) / sizeof(buffer[0]), _T("Send to %s"), deviceName);
			AppendMenu(hSubMenu, 0, uidFirstCmd + i, buffer);
			delete[] deviceName;
		}
		CloseHandle(hPipe);
	}
	if (m_deviceCount == 0) {
		AppendMenu(hSubMenu, MF_GRAYED, 0, _T("Not connected to any device"));
	}
	InsertMenu(hMenu, uMenuIndex, MF_BYPOSITION | MF_POPUP, (UINT_PTR) hSubMenu, _T("NotifySync"));
	return MAKE_HRESULT(SEVERITY_SUCCESS, FACILITY_NULL, 1);
}

STDMETHODIMP CNotifySyncShellExt::GetCommandString(UINT_PTR idCmd, UINT uFlags, UINT* pwReserved, LPSTR pszName, UINT cchMax) {
	return S_OK;
}

STDMETHODIMP CNotifySyncShellExt::InvokeCommand(LPCMINVOKECOMMANDINFO pCmdInfo) {
	if (HIWORD(pCmdInfo->lpVerb) != 0) {
		return E_INVALIDARG;
	}
	if (m_fileNameCount == 0) {
		return E_INVALIDARG;
	}
	UINT command = LOWORD(pCmdInfo->lpVerb);
	if (command >= m_deviceCount) {
		return E_INVALIDARG;
	}
	HANDLE hPipe = CreateFile(TEXT(NAMED_PIPE_NAME), GENERIC_READ | GENERIC_WRITE, 0, NULL, OPEN_EXISTING, 0, NULL);
	if (hPipe != INVALID_HANDLE_VALUE) {
		WriteString(hPipe, TEXT("send-files"));
		WriteString(hPipe, m_deviceIds[command]);
		WriteUInt(hPipe, m_fileNameCount);
		for (UINT i = 0; i < m_fileNameCount; i++) {
			WriteString(hPipe, m_fileNames[i]);
		}
		CloseHandle(hPipe);
	}
	return S_OK;
}

void CNotifySyncShellExt::ReadBytes(HANDLE hFile, void* buffer, size_t count) {
	DWORD realCount;
	ReadFile(hFile, buffer, (DWORD) count, &realCount, NULL);
}

UINT CNotifySyncShellExt::ReadUInt(HANDLE hFile) {
	UINT value;
	ReadBytes(hFile, &value, sizeof(value));
	return value;
}

TCHAR* CNotifySyncShellExt::ReadString(HANDLE hFile) {
	UINT count = ReadUInt(hFile);
	char* bytes = new char[count + 1];
	ReadBytes(hFile, bytes, count);
	bytes[count] = '\0';
	int byteCount = MultiByteToWideChar(CP_UTF8, 0, bytes, -1, NULL, 0);
	TCHAR* string = new TCHAR[byteCount];
	MultiByteToWideChar(CP_UTF8, 0, bytes, -1, string, byteCount);
	delete[] bytes;
	return string;
}

void CNotifySyncShellExt::WriteBytes(HANDLE hFile, const void* buffer, size_t count) {
	DWORD realCount;
	WriteFile(hFile, buffer, count, &realCount, NULL);
}

void CNotifySyncShellExt::WriteUInt(HANDLE hFile, UINT value) {
	WriteBytes(hFile, &value, sizeof(value));
}

void CNotifySyncShellExt::WriteString(HANDLE hFile, const TCHAR* value) {
	int count = WideCharToMultiByte(CP_UTF8, 0, value, -1, NULL, 0, NULL, NULL);
	char* bytes = new char[count + 1];
	WideCharToMultiByte(CP_UTF8, 0, value, -1, bytes, count + 1, NULL, NULL);
	UINT byteCount = strlen(bytes);
	WriteUInt(hFile, byteCount);
	WriteBytes(hFile, bytes, byteCount);
	delete[] bytes;
}
