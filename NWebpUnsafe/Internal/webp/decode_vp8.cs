﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

#if false
namespace NWebp.Internal
{
	//------------------------------------------------------------------------------
	// Lower-level API
	//
	// These functions provide fine-grained control of the decoding process.
	// The call flow should resemble:
	//
	//   VP8Io io;
	//   VP8InitIo(&io);
	//   io.data = data;
	//   io.data_size = size;
	//   /* customize io's functions (setup()/put()/teardown()) if needed. */
	//
	//   VP8Decoder* dec = VP8New();
	//   bool ok = VP8Decode(dec);
	//   if (!ok) printf("Error: %s\n", VP8StatusMessage(dec));
	//   VP8Delete(dec);
	//   return ok;

	// Input / Output
	/*
	typedef struct VP8Io VP8Io;
	*/
	/*
	typedef int (*VP8IoPutHook)(VP8Io* io);
	typedef int (*VP8IoSetupHook)(VP8Io* io);
	typedef void (*VP8IoTeardownHook)(VP8Io* io);
	*/
	delegate int VP8IoPutHook(ref VP8Io io);
	delegate int VP8IoSetupHook(ref VP8Io io);
	delegate void VP8IoTeardownHook(ref VP8Io io);

	partial class VP8Io
	{
		// set by VP8GetHeaders()
		int width, height;         // picture dimensions, in pixels (invariable).
									// These are the original, uncropped dimensions.
									// The actual area passed to put() is stored
									// in mb_w / mb_h fields.

		// set before calling put()
		int mb_y;                  // position of the current rows (in pixels)
		int mb_w;                  // number of columns in the sample
		int mb_h;                  // number of rows in the sample
		byte* y, u, v;  // rows to copy (in yuv420 format)
		int y_stride;              // row stride for luma
		int uv_stride;             // row stride for chroma

		void* opaque;              // user data

		// called when fresh samples are available. Currently, samples are in
		// YUV420 format, and can be up to width x 24 in size (depending on the
		// in-loop filtering level, e.g.). Should return false in case of error
		// or abort request. The actual size of the area to update is mb_w x mb_h
		// in size, taking cropping into account.
		VP8IoPutHook put;

		// called just before starting to decode the blocks.
		// Must return false in case of setup error, true otherwise. If false is
		// returned, teardown() will NOT be called. But if the setup succeeded
		// and true is returned, then teardown() will always be called afterward.
		VP8IoSetupHook setup;

		// Called just after block decoding is finished (or when an error occurred
		// during put()). Is NOT called if setup() failed.
		VP8IoTeardownHook teardown;

		// this is a recommendation for the user-side yuv.rgb converter. This flag
		// is set when calling setup() hook and can be overwritten by it. It then
		// can be taken into consideration during the put() method.
		int fancy_upsampling;

		// Input buffer.
		uint data_size;
		byte* data;

		// If true, in-loop filtering will not be performed even if present in the
		// bitstream. Switching off filtering may speed up decoding at the expense
		// of more visible blocking. Note that output will also be non-compliant
		// with the VP8 specifications.
		int bypass_filtering;

		// Cropping parameters.
		int use_cropping;
		int crop_left, crop_right, crop_top, crop_bottom;

		// Scaling parameters.
		int use_scaling;
		int scaled_width, scaled_height;

		// pointer to the alpha data (if present) corresponding to the rows
		byte* a;
	};

	/*
	// Internal, version-checked, entry point
	WEBP_EXTERN(int) VP8InitIoInternal(VP8Io* const, int);

	// Set the custom IO function pointers and user-data. The setter for IO hooks
	// should be called before initiating incremental decoding. Returns true if
	// WebPIDecoder object is successfully modified, false otherwise.
	WEBP_EXTERN(int) WebPISetIOHooks(WebPIDecoder* idec, VP8IoPutHook put, VP8IoSetupHook setup, VP8IoTeardownHook teardown, void* user_data);

	// Main decoding object. This is an opaque structure.
	typedef struct VP8Decoder VP8Decoder;

	// Create a new decoder object.
	WEBP_EXTERN(VP8Decoder*) VP8New();
	*/

	unsafe public partial class VP8Io
	{
		// Must be called to make sure 'io' is initialized properly.
		// Returns false in case of version mismatch. Upon such failure, no other
		// decoding function should be called (VP8Decode, VP8GetHeaders, ...)
		int VP8InitIo() {
			return this.VP8InitIoInternal(WEBP_DECODER_ABI_VERSION);
		}
	}
	/*
	// Start decoding a new picture. Returns true if ok.
	WEBP_EXTERN(int) VP8GetHeaders(VP8Decoder* dec, VP8Io* io);

	// Decode a picture. Will call VP8GetHeaders() if it wasn't done already.
	// Returns false in case of error.
	WEBP_EXTERN(int) VP8Decode(VP8Decoder* dec, VP8Io* io);

	// Return current status of the decoder:
	WEBP_EXTERN(VP8StatusCode) VP8Status(VP8Decoder* dec);

	// return readable string corresponding to the last status.
	WEBP_EXTERN(char*) VP8StatusMessage(VP8Decoder* dec);

	// Resets the decoder in its initial state, reclaiming memory.
	// Not a mandatory call between calls to VP8Decode().
	WEBP_EXTERN(void) VP8Clear(VP8Decoder* dec);

	// Destroy the decoder object.
	WEBP_EXTERN(void) VP8Delete(VP8Decoder* dec);
	*/

	//------------------------------------------------------------------------------
}
#endif
