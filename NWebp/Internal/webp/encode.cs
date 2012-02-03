using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NWebp.Internal
{
	partial class Global
	{
		public const int WEBP_ENCODER_ABI_VERSION = 0x0003;
	}

	/*

	//------------------------------------------------------------------------------
	// One-stop-shop call! No questions asked:

	// Returns the size of the compressed data (pointed to by *output), or 0 if
	// an error occurred. The compressed data must be released by the caller
	// using the call 'free(*output)'.
	WEBP_EXTERN(uint) WebPEncodeRGB(byte* rgb, int width, int height, int stride, float quality_factor, byte** output);
	WEBP_EXTERN(uint) WebPEncodeBGR(byte* bgr, int width, int height, int stride, float quality_factor, byte** output);
	WEBP_EXTERN(uint) WebPEncodeRGBA(byte* rgba, int width, int height, int stride, float quality_factor, byte** output);
	WEBP_EXTERN(uint) WebPEncodeBGRA(byte* bgra, int width, int height, int stride, float quality_factor, byte** output);
	*/

	//------------------------------------------------------------------------------
	// Coding parameters

	partial class WebPConfig
	{
		/// <summary>
		/// between 0 (smallest file) and 100 (biggest)
		/// </summary>
		float quality;

		/// <summary>
		/// if non-zero, set the desired target size in bytes.
		/// Takes precedence over the 'compression' parameter.
		/// </summary>
		int target_size;

		/// <summary>
		/// if non-zero, specifies the minimal distortion to
		/// try to achieve. Takes precedence over target_size.
		/// </summary>
		float target_PSNR;

		/// <summary>
		/// quality/speed trade-off (0=fast, 6=slower-better)
		/// </summary>
		int method;
		/// <summary>
		/// maximum number of segments to use, in [1..4]
		/// </summary>
		int segments;

		/// <summary>
		/// Spatial Noise Shaping. 0=off, 100=maximum.
		/// </summary>
		int sns_strength;

		/// <summary>
		/// range: [0 = off .. 100 = strongest]
		/// </summary>
		int filter_strength;

		/// <summary>
		/// range: [0 = off .. 7 = least sharp]
		/// </summary>
		int filter_sharpness;

		/// <summary>
		/// filtering type: 0 = simple, 1 = strong
		/// (only used if filter_strength > 0 or autofilter > 0)
		/// </summary>
		int filter_type;

		/// <summary>
		/// Auto adjust filter's strength [0 = off, 1 = on]
		/// </summary>
		int autofilter;

		/// <summary>
		/// number of entropy-analysis passes (in [1..10]).
		/// </summary>
		int pass;

		/// <summary>
		/// if true, export the compressed picture back.
		/// In-loop filtering is not applied.
		/// </summary>
		int show_compressed;

		/// <summary>
		/// preprocessing filter (0=none, 1=segment-smooth)
		/// </summary>
		int preprocessing;

		/// <summary>
		/// log2(number of token partitions) in [0..3]
		/// Default is set to 0 for easier progressive decoding.
		/// </summary>
		int partitions;

		/// <summary>
		/// quality degradation allowed to fit the 512k limit on
		/// prediction modes coding (0=no degradation, 100=full)
		/// </summary>
		int partition_limit;

		/// <summary>
		/// Algorithm for encoding the alpha plane (0 = none,
		/// 1 = backward reference counts encoded with
		/// arithmetic encoder). Default is 1.
		/// </summary>
		int alpha_compression;

		/// <summary>
		/// Predictive filtering method for alpha plane.
		/// 0: none, 1: fast, 2: best. Default if 1.
		/// </summary>
		int alpha_filtering;

		/// <summary>
		/// Between 0 (smallest size) and 100 (lossless).
		/// Default is 100.
		/// </summary>
		int alpha_quality;
	}

	// Enumerate some predefined settings for WebPConfig, depending on the type
	// of source picture. These presets are used when calling WebPConfigPreset().
	enum WebPPreset
	{
		WEBP_PRESET_DEFAULT = 0,  // default preset.
		WEBP_PRESET_PICTURE,      // digital picture, like portrait, inner shot
		WEBP_PRESET_PHOTO,        // outdoor photograph, with natural lighting
		WEBP_PRESET_DRAWING,      // hand or line drawing, with high-contrast details
		WEBP_PRESET_ICON,         // small-sized colorful images
		WEBP_PRESET_TEXT          // text-like
	}

	/*
	// Internal, version-checked, entry point
	WEBP_EXTERN(int) WebPConfigInitInternal(WebPConfig* const, WebPPreset, float, int);
	*/

	partial class WebPConfig
	{
		// Should always be called, to initialize a fresh WebPConfig structure before
		// modification. Returns 0 in case of version mismatch. WebPConfigInit() must
		// have succeeded before using the 'config' object.
		int WebPConfigInit()
		{
			return this.WebPConfigInitInternal(WEBP_PRESET_DEFAULT, 75.f, WEBP_ENCODER_ABI_VERSION);
		}

		// This function will initialize the configuration according to a predefined
		// set of parameters (referred to by 'preset') and a given quality factor.
		// This function can be called as a replacement to WebPConfigInit(). Will
		// return 0 in case of error.
		int WebPConfigPreset(WebPPreset preset, float quality)
		{
			return this.WebPConfigInitInternal(preset, quality, WEBP_ENCODER_ABI_VERSION);
		}
	}

	/*
	// Returns 1 if all parameters are in valid range and the configuration is OK.
	WEBP_EXTERN(int) WebPValidateConfig(WebPConfig* config);
	*/

	//------------------------------------------------------------------------------
	// Input / Output

	//typedef struct WebPPicture WebPPicture;   // main structure for I/O

	/// <summary>
	/// non-essential structure for storing auxiliary statistics
	/// </summary>
	struct WebPAuxStats
	{

		/// <summary>
		/// peak-signal-to-noise ratio for Y/U/V/All
		/// </summary>
		fixed float PSNR[4];

		/// <summary>
		/// final size
		/// </summary>
		int coded_size;

		/// <summary>
		/// number of intra4/intra16/skipped macroblocks
		/// </summary>
		fixed int block_count[3];

		/// <summary>
		/// approximate number of bytes spent for header
		/// and mode-partition #0
		/// </summary>
		fixed int header_bytes[2];

		/// <summary>
		/// approximate number of bytes spent for
		/// DC/AC/uv coefficients for each (0..3) segments.
		/// </summary>
		//fixed int residual_bytes[3][4]; 
		fixed int residual_bytes[3 * 4];

		/// <summary>
		/// number of macroblocks in each segments
		/// </summary>
		fixed int segment_size[4];

		/// <summary>
		/// quantizer values for each segments
		/// </summary>
		fixed int segment_quant[4];

		/// <summary>
		/// filtering strength for each segments [0..63]
		/// </summary>
		fixed int segment_level[4];

		/// <summary>
		/// size of the transparency data
		/// </summary>
		int alpha_data_size;

		/// <summary>
		/// size of the enhancement layer data
		/// </summary>
		int layer_data_size;

		/// <summary>
		/// this field is free to be set to any value and
		/// used during callbacks (like progress-report e.g.).
		/// </summary>
		void* user_data;
	}

	// Signature for output function. Should return 1 if writing was successful.
	// data/data_size is the segment of data to write, and 'picture' is for
	// reference (and so one can make use of picture.custom_ptr).
	delegate int WebPWriterFunction(byte* data, uint data_size, ref WebPPicture picture);

	// Progress hook, called from time to time to report progress. It can return 0
	// to request an abort of the encoding process, or 1 otherwise if all is OK.
	delegate int WebPProgressHook(int percent, ref WebPPicture picture);

	enum WebPEncCSP
	{
		// chroma sampling
		WEBP_YUV420 = 0,   // 4:2:0
		WEBP_YUV422 = 1,   // 4:2:2
		WEBP_YUV444 = 2,   // 4:4:4
		WEBP_YUV400 = 3,   // grayscale
		WEBP_CSP_UV_MASK = 3,   // bit-mask to get the UV sampling factors
		// alpha channel variants
		WEBP_YUV420A = 4,
		WEBP_YUV422A = 5,
		WEBP_YUV444A = 6,
		WEBP_YUV400A = 7,   // grayscale + alpha
		WEBP_CSP_ALPHA_BIT = 4   // bit that is set if alpha is present
	}

	/// <summary>
	/// Encoding error conditions.
	/// </summary>
	enum WebPEncodingError
	{
		/// <summary>
		/// ok
		/// </summary>
		VP8_ENC_OK = 0,

		/// <summary>
		/// memory error allocating objects
		/// </summary>
		VP8_ENC_ERROR_OUT_OF_MEMORY,

		/// <summary>
		/// memory error while flushing bits
		/// </summary>
		VP8_ENC_ERROR_BITSTREAM_OUT_OF_MEMORY,

		/// <summary>
		/// a pointer parameter is null
		/// </summary>
		VP8_ENC_ERROR_NULL_PARAMETER,

		/// <summary>
		/// configuration is invalid
		/// </summary>
		VP8_ENC_ERROR_INVALID_CONFIGURATION,

		/// <summary>
		/// picture has invalid width/height
		/// </summary>
		VP8_ENC_ERROR_BAD_DIMENSION,

		/// <summary>
		/// partition is bigger than 512k
		/// </summary>
		VP8_ENC_ERROR_PARTITION0_OVERFLOW,

		/// <summary>
		/// partition is bigger than 16M
		/// </summary>
		VP8_ENC_ERROR_PARTITION_OVERFLOW,

		/// <summary>
		/// error while flushing bytes
		/// </summary>
		VP8_ENC_ERROR_BAD_WRITE,
		/// <summary>
		/// file is bigger than 4G
		/// </summary>
		VP8_ENC_ERROR_FILE_TOO_BIG,

		/// <summary>
		/// abort request by user
		/// </summary>
		VP8_ENC_ERROR_USER_ABORT,

		/// <summary>
		/// list terminator. always last.
		/// </summary>
		VP8_ENC_ERROR_LAST
	}

	partial class Global
	{
		/// <summary>
		/// maximum width/height allowed (inclusive), in pixels
		/// </summary>
		int WEBP_MAX_DIMENSION = 16383;
	}

	struct WebPPicture
	{

		/// <summary>
		/// input
		/// colorspace: should be YUV420 for now (=Y'CbCr).
		/// </summary>
		WebPEncCSP colorspace;

		/// <summary>
		/// dimensions (less or equal to WEBP_MAX_DIMENSION)
		/// </summary>
		int width, height;

		/// <summary>
		/// pointers to luma/chroma planes.
		/// </summary>
		byte* y, u, v;

		/// <summary>
		/// luma/chroma strides.
		/// </summary>
		int y_stride, uv_stride;

		/// <summary>
		/// pointer to the alpha plane
		/// </summary>
		byte* a;

		/// <summary>
		/// stride of the alpha plane
		/// </summary>
		int a_stride;

		/// <summary>
		/// output
		/// can be null
		/// </summary>
		WebPWriterFunction writer;

		/// <summary>
		/// can be used by the writer.
		/// </summary>
		void* custom_ptr;

		/// <summary>
		/// map for extra information
		/// 1: intra type, 2: segment, 3: quant
		/// 4: intra-16 prediction mode,
		/// 5: chroma prediction mode,
		/// 6: bit cost, 7: distortion
		/// </summary>
		int extra_info_type;

		/// <summary>
		/// if not null, points to an array of size
		/// ((width + 15) / 16) * ((height + 15) / 16) that
		/// will be filled with a macroblock map, depending
		/// on extra_info_type.
		/// </summary>
		byte* extra_info;

		/// <summary>
		/// where to store statistics, if not null:
		/// </summary>
		WebPAuxStats* stats;

		/// <summary>
		/// original samples (for non-YUV420 modes)
		/// </summary>
		byte* u0, v0;
		int uv0_stride;

		/// <summary>
		/// error code in case of problem.
		/// </summary>
		WebPEncodingError error_code;

		/// <summary>
		/// if not null, called while encoding.
		/// </summary>
		WebPProgressHook progress_hook;
	};

	/*
	// Internal, version-checked, entry point
	WEBP_EXTERN(int) WebPPictureInitInternal(WebPPicture* const, int);
	*/

	// Should always be called, to initialize the structure. Returns 0 in case of
	// version mismatch. WebPPictureInit() must have succeeded before using the
	// 'picture' object.
	partial class WebPPicture
	{
		int WebPPictureInit()
		{
			return this.WebPPictureInitInternal(WEBP_ENCODER_ABI_VERSION);
		}
	}

	/*
	//------------------------------------------------------------------------------
	// WebPPicture utils

	// Convenience allocation / deallocation based on picture.width/height:
	// Allocate y/u/v buffers as per colorspace/width/height specification.
	// Note! This function will free the previous buffer if needed.
	// Returns 0 in case of memory error.
	WEBP_EXTERN(int) WebPPictureAlloc(WebPPicture* picture);

	// Release memory allocated by WebPPictureAlloc() or WebPPictureImport*()
	// Note that this function does _not_ free the memory pointed to by 'picture'.
	WEBP_EXTERN(void) WebPPictureFree(WebPPicture* picture);

	// Copy the pixels of *src into *dst, using WebPPictureAlloc.
	// Returns 0 in case of memory allocation error.
	WEBP_EXTERN(int) WebPPictureCopy(WebPPicture* src, WebPPicture* dst);

	// Compute PSNR or SSIM distortion between two pictures.
	// Result is in dB, stores in result[] in the Y/U/V/Alpha/All order.
	// Returns 0 in case of error (pic1 and pic2 don't have same dimension, ...)
	// Warning: this function is rather CPU-intensive.
	// int metric_type: 0 = PSNR, 1 = SSIM
	WEBP_EXTERN(int) WebPPictureDistortion(WebPPicture* pic1, WebPPicture* pic2, int metric_type, float result[5]);

	// self-crops a picture to the rectangle defined by top/left/width/height.
	// Returns 0 in case of memory allocation error, or if the rectangle is
	// outside of the source picture.
	WEBP_EXTERN(int) WebPPictureCrop(WebPPicture* picture, int left, int top, int width, int height);

	// Rescale a picture to new dimension width x height.
	// Now gamma correction is applied.
	// Returns false in case of error (invalid parameter or insufficient memory).
	WEBP_EXTERN(int) WebPPictureRescale(WebPPicture* pic, int width, int height);

	// Colorspace conversion function to import RGB samples.
	// Previous buffer will be free'd, if any.
	// *rgb buffer should have a size of at least height * rgb_stride.
	// Returns 0 in case of memory error.
	WEBP_EXTERN(int) WebPPictureImportRGB(WebPPicture* picture, byte* rgb, int rgb_stride);
	// Same, but for RGBA buffer
	WEBP_EXTERN(int) WebPPictureImportRGBA(WebPPicture* picture, byte* rgba, int rgba_stride);

	// Variant of the above, but taking BGR(A) input:
	WEBP_EXTERN(int) WebPPictureImportBGR(WebPPicture* picture, byte* bgr, int bgr_stride);
	WEBP_EXTERN(int) WebPPictureImportBGRA(WebPPicture* picture, byte* bgra, int bgra_stride);

	// Helper function: given a width x height plane of YUV(A) samples
	// (with stride 'stride'), clean-up the YUV samples under fully transparent
	// area, to help compressibility (no guarantee, though).
	WEBP_EXTERN(void) WebPCleanupTransparentArea(WebPPicture* picture);

	//------------------------------------------------------------------------------
	// Main call

	// Main encoding call, after config and picture have been initialized.
	// 'picture' must be less than 16384x16384 in dimension (cf WEBP_MAX_DIMENSION),
	// and the 'config' object must be a valid one.
	// Returns false in case of error, true otherwise.
	// In case of error, picture.error_code is updated accordingly.
	WEBP_EXTERN(int) WebPEncode(WebPConfig* config, WebPPicture* picture);

	//------------------------------------------------------------------------------
	*/
}
