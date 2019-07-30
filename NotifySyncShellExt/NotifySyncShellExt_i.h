

/* this ALWAYS GENERATED file contains the definitions for the interfaces */


 /* File created by MIDL compiler version 8.01.0622 */
/* at Tue Jan 19 07:14:07 2038
 */
/* Compiler settings for NotifySyncShellExt.idl:
    Oicf, W1, Zp8, env=Win32 (32b run), target_arch=X86 8.01.0622 
    protocol : dce , ms_ext, c_ext, robust
    error checks: allocation ref bounds_check enum stub_data 
    VC __declspec() decoration level: 
         __declspec(uuid()), __declspec(selectany), __declspec(novtable)
         DECLSPEC_UUID(), MIDL_INTERFACE()
*/
/* @@MIDL_FILE_HEADING(  ) */



/* verify that the <rpcndr.h> version is high enough to compile this file*/
#ifndef __REQUIRED_RPCNDR_H_VERSION__
#define __REQUIRED_RPCNDR_H_VERSION__ 500
#endif

#include "rpc.h"
#include "rpcndr.h"

#ifndef __RPCNDR_H_VERSION__
#error this stub requires an updated version of <rpcndr.h>
#endif /* __RPCNDR_H_VERSION__ */


#ifndef __NotifySyncShellExt_i_h__
#define __NotifySyncShellExt_i_h__

#if defined(_MSC_VER) && (_MSC_VER >= 1020)
#pragma once
#endif

/* Forward Declarations */ 

#ifndef __CNotifySyncShellExt_FWD_DEFINED__
#define __CNotifySyncShellExt_FWD_DEFINED__

#ifdef __cplusplus
typedef class CNotifySyncShellExt CNotifySyncShellExt;
#else
typedef struct CNotifySyncShellExt CNotifySyncShellExt;
#endif /* __cplusplus */

#endif 	/* __CNotifySyncShellExt_FWD_DEFINED__ */


/* header files for imported files */
#include "oaidl.h"
#include "ocidl.h"
#include "shobjidl.h"

#ifdef __cplusplus
extern "C"{
#endif 



#ifndef __NotifySyncShellExtLib_LIBRARY_DEFINED__
#define __NotifySyncShellExtLib_LIBRARY_DEFINED__

/* library NotifySyncShellExtLib */
/* [version][uuid] */ 


EXTERN_C const IID LIBID_NotifySyncShellExtLib;

EXTERN_C const CLSID CLSID_CNotifySyncShellExt;

#ifdef __cplusplus

class DECLSPEC_UUID("4430d43b-3d4f-4c6e-b24c-6ad3293dc86b")
CNotifySyncShellExt;
#endif
#endif /* __NotifySyncShellExtLib_LIBRARY_DEFINED__ */

/* Additional Prototypes for ALL interfaces */

/* end of Additional Prototypes */

#ifdef __cplusplus
}
#endif

#endif


