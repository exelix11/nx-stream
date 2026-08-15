#include "../dl_shim_base.h"

#define STB_IMAGE_IMPLEMENTATION
#define STBI_NO_STDIO
#include "stb_image.h"

void libc_free(void *ptr)
{
	free(ptr);
}

void *getsym_OsuInternal(const char *name)
{
	SYM_RESOLVE(decode_audio_mp3);
	SYM_RESOLVE(decode_audio_mp4);
	SYM_RESOLVE_EXISTING(libc_free);
	
    SYM_RESOLVE_EXISTING(stbi_load_from_memory);
	SYM_RESOLVE_EXISTING(stbi_image_free);

	SYM_RESOLVE(glfonsCreate);
	SYM_RESOLVE(glfonsDelete);
	SYM_RESOLVE(fonsAddFont);
	SYM_RESOLVE(fonsClearState);
	SYM_RESOLVE(fonsSetFont);
	SYM_RESOLVE(fonsSetSize);
	SYM_RESOLVE(fonsSetColor);
	SYM_RESOLVE(fonsSetSpacing);
	SYM_RESOLVE(fonsSetAlign);
	SYM_RESOLVE(fonsVertMetrics);
	SYM_RESOLVE(fonsDrawText);
	SYM_RESOLVE(fonsTextBounds);
	return NULL;
}