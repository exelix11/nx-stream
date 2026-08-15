
#include <stdio.h>
#include <string.h>
#include <stdlib.h>
#define FONTSTASH_IMPLEMENTATION

//#define FONS_USE_FREETYPE

#include "fontstash.h"

#include <glad/glad.h> 

// Osu stream and glfontstash use the fixed pipeline but the dkp-provided glad header only includes core profile (despite the fact that mesa does support the compatibility profile)
// Regenerating the headers compiles but won't link because the following symbols are not even exposed in the object files
// So we have this ugly hack here where I manually defined the functions that are missing and resolve them with eglGetProcAddress

#define GL_CLIENT_PIXEL_STORE_BIT 0x00000001

#define make_gl_wrapper(name, ...) \
    typedef void (APIENTRYP PFN##name##PROC)(__VA_ARGS__); \
    void name (__VA_ARGS__) { \
        static PFN##name##PROC pfn = NULL; \
        if (!pfn) { \
            pfn = (PFN##name##PROC)eglGetProcAddress(#name); \
        } \
        if (!pfn) {  \
            fprintf(stderr, "Failed to load OpenGL function: %s\n", #name); \
            exit(1); \
        } \
        
make_gl_wrapper(glPushClientAttrib, GLbitfield mask) pfn(mask); }
make_gl_wrapper(glPopClientAttrib, void) pfn(); }
make_gl_wrapper(glTexCoordPointer, GLint size, GLenum type, GLsizei stride, const void *pointer) pfn(size, type, stride, pointer); }
make_gl_wrapper(glVertexPointer, GLint size, GLenum type, GLsizei stride, const void *pointer) pfn(size, type, stride, pointer); }
make_gl_wrapper(glColorPointer, GLint size, GLenum type, GLsizei stride, const void *pointer) pfn(size, type, stride, pointer); }

#define GLFONTSTASH_IMPLEMENTATION
#include "glfontstash.h"