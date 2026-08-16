#pragma once

#include <stdint.h>

// Disabling dlshim is possible only in AOT builds when direct-pinvoke and direct-icall are used, these options enable static linking of all extern functions removing the need for our fake dynamic loader. Disabling dlshim allows the linker to trim additional code reducing the final nro size. However, this is not possible in interpreter builds since the mono runtime needs to be able to load libraries and symbols at runtime.

#if !DLSHIM_DISABLE

void* dlshim_loadLibrary(const char *name, int flags, char **err, void *user_data);
void* dlshim_closeLibrary(void *handle, void *user_data);
void* dlshim_getSymbol(void *handle, const char *name, char **err, void *user_data);

#endif