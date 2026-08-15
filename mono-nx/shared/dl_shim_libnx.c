#include "dl_shim_base.h"
#include <switch.h>

u32 extensionPadStateSize() 
{
    return sizeof(PadState);
}

u32 extensionHidTouchScreenStateSize() 
{
    return sizeof(HidTouchScreenState);
}

void *getsym_Libnx(const char *name)
{
    SYM_RESOLVE_EXISTING(padConfigureInput);
    SYM_RESOLVE_EXISTING(padUpdate);
    SYM_RESOLVE_EXISTING(padInitializeWithMask);
    SYM_RESOLVE_EXISTING(extensionPadStateSize);
    
    SYM_RESOLVE_EXISTING(appletMainLoop);
    SYM_RESOLVE_EXISTING(appletGetOperationMode);

    SYM_RESOLVE_EXISTING(hidInitializeTouchScreen);
    SYM_RESOLVE_EXISTING(hidGetTouchScreenStates);
    SYM_RESOLVE_EXISTING(extensionHidTouchScreenStateSize);

    // Used by opentk
    SYM_RESOLVE_EXISTING(nwindowGetDefault);
    return NULL;
}