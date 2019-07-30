// dllmain.h : Declaration of module class.

class CNotifySyncShellExtModule : public ATL::CAtlDllModuleT< CNotifySyncShellExtModule >
{
public :
	DECLARE_LIBID(LIBID_NotifySyncShellExtLib)
	DECLARE_REGISTRY_APPID_RESOURCEID(IDR_NOTIFYSYNCSHELLEXT, "{7fb6a0b9-8da1-4a0b-9b8e-f3309eb7e62c}")
};

extern class CNotifySyncShellExtModule _AtlModule;
