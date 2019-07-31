#pragma once

#include <shlobj.h>
#include <comdef.h>

#include "resource.h"
#include "NotifySyncShellExt_i.h"

#if defined(_WIN32_WCE) && !defined(_CE_DCOM) && !defined(_CE_ALLOW_SINGLE_THREADED_OBJECTS_IN_MTA)
#error "Single-threaded COM objects are not properly supported on Windows CE platform, such as the Windows Mobile platforms that do not include full DCOM support. Define _CE_ALLOW_SINGLE_THREADED_OBJECTS_IN_MTA to force ATL to support creating single-thread COM object's and allow use of it's single-threaded COM object implementations. The threading model in your rgs file was set to 'Free' as that is the only threading model supported in non DCOM Windows CE platforms."
#endif

using namespace ATL;

class ATL_NO_VTABLE CNotifySyncShellExt : public CComObjectRootEx<CComSingleThreadModel>,
	public CComCoClass<CNotifySyncShellExt, & CLSID_CNotifySyncShellExt>,
	public IShellExtInit, public IContextMenu {
public:
	CNotifySyncShellExt();

	DECLARE_REGISTRY_RESOURCEID(106)
	DECLARE_NOT_AGGREGATABLE(CNotifySyncShellExt)
	BEGIN_COM_MAP(CNotifySyncShellExt)
		COM_INTERFACE_ENTRY(IShellExtInit)
		COM_INTERFACE_ENTRY(IContextMenu)
	END_COM_MAP()

	DECLARE_PROTECT_FINAL_CONSTRUCT()

	HRESULT FinalConstruct();
	void FinalRelease();

public:
	// IShellExtInit
	STDMETHODIMP Initialize(LPCITEMIDLIST, LPDATAOBJECT, HKEY);
	// IContextMenu
	STDMETHODIMP GetCommandString(UINT_PTR, UINT, UINT*, LPSTR, UINT);
	STDMETHODIMP InvokeCommand(LPCMINVOKECOMMANDINFO);
	STDMETHODIMP QueryContextMenu(HMENU, UINT, UINT, UINT, UINT);

private:
	UINT m_fileNameCount;
	TCHAR** m_fileNames;
	UINT m_firstCmd;
	UINT m_deviceCount;
	TCHAR** m_deviceIds;

	void ReadBytes(HANDLE hFile, void *buffer, size_t count);
	UINT ReadUInt(HANDLE hFile);
	TCHAR* ReadString(HANDLE hFile);
	void WriteBytes(HANDLE hFile, const void *buffer, size_t count);
	void WriteUInt(HANDLE hFile, UINT value);
	void WriteString(HANDLE hFile, const TCHAR *value);

};

OBJECT_ENTRY_AUTO(__uuidof(CNotifySyncShellExt), CNotifySyncShellExt)
