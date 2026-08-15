#include <stdlib.h>
#include <string.h>

// Mp3 support
#include "../third_party/minimp3_ex.h"

const char *decode_audio_mp3(const char *inbuf, int32_t inbufLen,
                             char **out_samples, int32_t *out_num_frames,
                             int *sample_rate, int *bits_per_sample, int *channels)
{
    if (out_samples)    *out_samples    = NULL;
    if (out_num_frames) *out_num_frames = 0;
 
    if (!inbuf || !out_samples || !out_num_frames)
        return "decode_audio_mp3: null required argument";
    if (inbufLen == 0)
        return "decode_audio_mp3: empty input buffer";
 
    mp3dec_t mp3d;
    mp3dec_file_info_t info;
    memset(&info, 0, sizeof(info));
 
    int rc = mp3dec_load_buf(&mp3d, (const uint8_t *)inbuf, inbufLen, &info, NULL, NULL);

    if (rc != 0) {
        if (info.buffer) free(info.buffer); 
        if (rc == MP3D_E_MEMORY) return "decode_audio_mp3: out of memory";
        
		return "decode_audio_mp3: failed to decode MP3 stream";
    }
    if (info.buffer == NULL || info.samples == 0 || info.channels <= 0) {
        free(info.buffer);
        return "decode_audio_mp3: nothing decoded";
    }
 
    /* info.samples counts *interleaved* samples (channels folded in). */
    *out_samples    = (char *)info.buffer;               /* caller frees */
    *out_num_frames = info.samples / (size_t)info.channels;
 
    if (sample_rate) *sample_rate = info.hz;
    if (channels) *channels    = info.channels;
    if (bits_per_sample) *bits_per_sample = (int)(sizeof(mp3d_sample_t) * 8);
    return NULL;
}

// M4A + AAC support
#include "../third_party/minimp4.h"
#include <fdk-aac/aacdecoder_lib.h>

typedef struct {
    const uint8_t *buffer;
    int64_t        size;
} input_buffer_t;
 
static int mem_read_callback(int64_t offset, void *dst, size_t size, void *token)
{
    input_buffer_t *buf = (input_buffer_t *)token;
    if (offset < 0 || offset > buf->size)
        return 1;

    int64_t avail = buf->size - offset;
    size_t  n = (size <= (size_t)avail) ? size : (size_t)avail;
    
	memcpy(dst, buf->buffer + offset, n);
    return n != size; /* nonzero => couldn't satisfy the full request */
}
 
// Growing list to accumulate decoded samples.
typedef struct {
    INT_PCM *data;
    size_t   count;
    size_t   cap;  
    int      oom;
} pcm_accum_t;
 
static int pcm_reserve(pcm_accum_t *a, size_t extra)
{
    if (a->oom) return 0;
    if (a->count + extra <= a->cap) return 1;
    size_t ncap = a->cap ? a->cap : 64 * 1024;
    while (ncap < a->count + extra) {
        size_t doubled = ncap * 2;
        if (doubled < ncap) { a->oom = 1; return 0; } /* overflow */
        ncap = doubled;
    }
    INT_PCM *nd = (INT_PCM *)realloc(a->data, ncap * sizeof(INT_PCM));
    if (!nd) { a->oom = 1; return 0; }
    a->data = nd;
    a->cap  = ncap;
    return 1;
}
 
static void pcm_append(pcm_accum_t *a, const INT_PCM *src, size_t n)
{
    if (!pcm_reserve(a, n)) return;
    memcpy(a->data + a->count, src, n * sizeof(INT_PCM));
    a->count += n;
}
 
const char *decode_audio_mp4(const char *inbuf, int32_t inbufLen,
                             char **out_samples, int32_t *out_num_frames,
                             int *sample_rate, int *bits_per_sample, int *channels)
{
    if (out_samples)    *out_samples    = NULL;
    if (out_num_frames) *out_num_frames = 0;
 
    if (!inbuf || !out_samples || !out_num_frames)
        return "decode_audio_mp4: null required argument";
    if (inbufLen == 0)
        return "decode_audio_mp4: empty input buffer";
 
    const char     *err_msg = NULL;
    input_buffer_t  ib      = { (const uint8_t *)inbuf, (int64_t)inbufLen };
    MP4D_demux_t    mp4;
    HANDLE_AACDECODER dec   = NULL;
    pcm_accum_t     acc     = {0};
    memset(&mp4, 0, sizeof(mp4));
 
    MP4D_open(&mp4, mem_read_callback, &ib, (int64_t)inbufLen);
    if (mp4.track_count == 0) {
        err_msg = "decode_audio_mp4: not a valid MP4/M4A container";
        goto done;
    }
 
    unsigned atrack = (unsigned)-1;
    for (unsigned t = 0; t < mp4.track_count; t++) {
        if (mp4.track[t].handler_type == MP4D_HANDLER_TYPE_SOUN) {
            atrack = t;
            break;
        }
    }
    if (atrack == (unsigned)-1) {
        err_msg = "decode_audio_mp4: no audio track found";
        goto done;
    }
 
    MP4D_track_t *tr = &mp4.track[atrack];
 
    switch (tr->object_type_indication) {
        case MP4_OBJECT_TYPE_AUDIO_ISO_IEC_14496_3:              /* MPEG-4 AAC   */
        case MP4_OBJECT_TYPE_AUDIO_ISO_IEC_13818_7_MAIN_PROFILE: /* MPEG-2 AAC   */
        case MP4_OBJECT_TYPE_AUDIO_ISO_IEC_13818_7_LC_PROFILE:
        case MP4_OBJECT_TYPE_AUDIO_ISO_IEC_13818_7_SSR_PROFILE:
            break;
        default:
            err_msg = "decode_audio_mp4: audio track is not AAC";
            goto done;
    }
    if (!tr->dsi || tr->dsi_bytes == 0) {
        err_msg = "decode_audio_mp4: missing AudioSpecificConfig (dsi)";
        goto done;
    }
 
    dec = aacDecoder_Open(TT_MP4_RAW, 1);
    if (dec)
    {
        UCHAR *dsi      = (UCHAR *)tr->dsi;
        UINT   dsi_size = (UINT)tr->dsi_bytes;
        if (aacDecoder_ConfigRaw(dec, &dsi, &dsi_size) != AAC_DEC_OK) {
            err_msg = "decode_audio_mp4: aacDecoder_ConfigRaw failed";
            goto done;
        }
    }
	else
	{
        err_msg = "decode_audio_mp4: aacDecoder_Open failed";
        goto done;
	}
 
    int      out_rate = 0, out_ch = 0;
    unsigned decoded_frames = 0;
    INT_PCM  pcm[8 * 2048]; /* max 8ch * 2048 samples/frame (HE-AAC SBR) */
 
    for (unsigned s = 0; s < tr->sample_count; s++) {
        unsigned frame_bytes = 0, timestamp = 0, duration = 0;
        MP4D_file_offset_t ofs =
            MP4D_frame_offset(&mp4, atrack, s, &frame_bytes, &timestamp, &duration);
 
        if (frame_bytes == 0)
            continue;
        if ((int64_t)ofs + (int64_t)frame_bytes > (int64_t)inbufLen) {
            err_msg = "decode_audio_mp4: sample offset out of bounds";
            goto done;
        }
 
        UCHAR *au        = (UCHAR *)(inbuf + ofs);
        UINT   au_size   = frame_bytes;
        UINT   bytes_valid = frame_bytes;
 
        if (aacDecoder_Fill(dec, &au, &au_size, &bytes_valid) != AAC_DEC_OK) {
            err_msg = "decode_audio_mp4: aacDecoder_Fill failed";
            goto done;
        }
 
        /* One access unit usually yields one frame, but drain fully. */
        for (;;) {
            if (aacDecoder_DecodeFrame(dec, pcm, (int)(sizeof(pcm) / sizeof(pcm[0])), 0) != AAC_DEC_OK)
                break; 
 
            CStreamInfo *si = aacDecoder_GetStreamInfo(dec);
            if (!si || si->frameSize <= 0 || si->numChannels <= 0)
                break;
 
            if (out_ch == 0) {           /* latch format from first good frame */
                out_ch   = si->numChannels;
                out_rate = si->sampleRate; /* reflects SBR-doubled rate if HE-AAC */
            }
 
            pcm_append(&acc, pcm, (size_t)si->frameSize * (size_t)si->numChannels);
            if (acc.oom) {
                err_msg = "decode_audio_mp4: out of memory";
                goto done;
            }

            decoded_frames++;
 
            if (bytes_valid == 0)
                break;
        }
    }
 
    if (decoded_frames == 0 || out_ch == 0 || acc.count == 0) {
        err_msg = "decode_audio_mp4: no audio samples decoded";
        goto done;
    }
 	
    *out_samples    = (char *)acc.data;
    *out_num_frames = acc.count / (size_t)out_ch;
    acc.data        = NULL; /* prevent cleanup from freeing it */
 
    if (sample_rate) *sample_rate = out_rate;
    if (channels) *channels    = out_ch;
    if (bits_per_sample) *bits_per_sample = (int)(sizeof(INT_PCM) * 8);
 
done:
    if (dec) aacDecoder_Close(dec);
    MP4D_close(&mp4);
    free(acc.data); /* NULL on success path */
    return err_msg;
}
