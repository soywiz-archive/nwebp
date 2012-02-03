using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

#if false
namespace NWebp.Internal
{
	/*
	// Retrieve basic header information: width, height.
	// This function will also validate the header and return 0 in
	// case of formatting error.
	// Pointers *width/*height can be passed null if deemed irrelevant.
	WEBP_EXTERN(int) WebPGetInfo(byte* data, uint data_size, int* width, int* height);

	// Decodes WEBP images pointed to by *data and returns RGB samples, along
	// with the dimensions in *width and *height.
	// The returned pointer should be deleted calling free().
	// Returns null in case of error.
	WEBP_EXTERN(byte*) WebPDecodeRGB(byte* data, uint data_size, int* width, int* height);

	// Same as WebPDecodeRGB, but returning RGBA data.
	WEBP_EXTERN(byte*) WebPDecodeRGBA(byte* data, uint data_size, int* width, int* height);

	// Same as WebPDecodeRGBA, but returning ARGB data.
	WEBP_EXTERN(byte*) WebPDecodeARGB(byte* data, uint data_size, int* width, int* height);

	// This variant decode to BGR instead of RGB.
	WEBP_EXTERN(byte*) WebPDecodeBGR(byte* data, uint data_size, int* width, int* height);
	// This variant decodes to BGRA instead of RGBA.
	WEBP_EXTERN(byte*) WebPDecodeBGRA(byte* data, uint data_size, int* width, int* height);

	// Decode WEBP images stored in *data in Y'UV format(*). The pointer returned is
	// the Y samples buffer. Upon return, *u and *v will point to the U and V
	// chroma data. These U and V buffers need NOT be free()'d, unlike the returned
	// Y luma one. The dimension of the U and V planes are both (*width + 1) / 2
	// and (*height + 1)/ 2.
	// Upon return, the Y buffer has a stride returned as '*stride', while U and V
	// have a common stride returned as '*uv_stride'.
	// Return null in case of error.
	// (*) Also named Y'CbCr. See: http://en.wikipedia.org/wiki/YCbCr
	WEBP_EXTERN(byte*) WebPDecodeYUV(byte* data, uint data_size, int* width, int* height, byte** u, byte** v, int* stride, int* uv_stride);

	// These five functions are variants of the above ones, that decode the image
	// directly into a pre-allocated buffer 'output_buffer'. The maximum storage
	// available in this buffer is indicated by 'output_buffer_size'. If this
	// storage is not sufficient (or an error occurred), null is returned.
	// Otherwise, output_buffer is returned, for convenience.
	// The parameter 'output_stride' specifies the distance (in bytes)
	// between scanlines. Hence, output_buffer_size is expected to be at least
	// output_stride x picture-height.
	WEBP_EXTERN(byte*) WebPDecodeRGBInto(byte* data, uint data_size, byte* output_buffer, int output_buffer_size, int output_stride);
	WEBP_EXTERN(byte*) WebPDecodeRGBAInto(byte* data, uint data_size, byte* output_buffer, int output_buffer_size, int output_stride);
	WEBP_EXTERN(byte*) WebPDecodeARGBInto(byte* data, uint data_size, byte* output_buffer, int output_buffer_size, int output_stride);
	// BGR variants
	WEBP_EXTERN(byte*) WebPDecodeBGRInto(byte* data, uint data_size, byte* output_buffer, int output_buffer_size, int output_stride);
	WEBP_EXTERN(byte*) WebPDecodeBGRAInto(byte* data, uint data_size, byte* output_buffer, int output_buffer_size, int output_stride);

	// WebPDecodeYUVInto() is a variant of WebPDecodeYUV() that operates directly
	// into pre-allocated luma/chroma plane buffers. This function requires the
	// strides to be passed: one for the luma plane and one for each of the
	// chroma ones. The size of each plane buffer is passed as 'luma_size',
	// 'u_size' and 'v_size' respectively.
	// Pointer to the luma plane ('*luma') is returned or null if an error occurred
	// during decoding (or because some buffers were found to be too small).
	WEBP_EXTERN(byte*) WebPDecodeYUVInto(byte* data, uint data_size, byte* luma, int luma_size, int luma_stride, byte* u, int u_size, int u_stride, byte* v, int v_size, int v_stride);
	*/


	// Output colorspaces and buffer

	/// <summary>
	/// Colorspaces
	/// </summary>
	public enum WEBP_CSP_MODE
	{
		MODE_RGB = 0, MODE_RGBA = 1,
		MODE_BGR = 2, MODE_BGRA = 3,
		MODE_ARGB = 4, MODE_RGBA_4444 = 5,
		MODE_RGB_565 = 6,
		// YUV modes must come after RGB ones.
		MODE_YUV = 7, MODE_YUVA = 8,  // yuv 4:2:0
		MODE_LAST = 9
	}

	/// <summary>
	/// Generic structure for describing the sample buffer.
	/// </summary>
	unsafe partial class WebPRGBABuffer
	{
		/// <summary>
		/// view as RGBA
		/// pointer to RGBA samples
		/// </summary>
		public byte* rgba;  

		/// <summary>
		/// stride in bytes from one scanline to the next.
		/// </summary>
		public int stride;      
 
		/// <summary>
		/// total size of the *rgba buffer.
		/// </summary>
		public int size;        
	}

	unsafe partial class WebPYUVABuffer
	{
		/// <summary>
		/// view as YUVA
		/// pointer to luma, chroma U/V, alpha samples
		/// </summary>
		public byte* y, u, v, a;

		/// <summary>
		/// luma stride
		/// </summary>
		public int y_stride;              

		/// <summary>
		/// chroma strides
		/// </summary>
		public int u_stride, v_stride;    

		/// <summary>
		/// alpha stride
		/// </summary>
		public int a_stride;              

		/// <summary>
		/// luma plane size
		/// </summary>
		public int y_size;                

		/// <summary>
		/// chroma planes size
		/// </summary>
		public int u_size, v_size;        

		/// <summary>
		/// alpha-plane size
		/// </summary>
		public int a_size;                
	}

	/// <summary>
	/// Output buffer
	/// </summary>
	unsafe public partial class WebPDecBuffer
	{
		/// <summary>
		/// Colorspace.
		/// </summary>
		WEBP_CSP_MODE colorspace; 

		/// <summary>
		/// Dimensions.
		/// </summary>
		int width, height;        
		
		/// <summary>
		/// If true, 'internal_memory' pointer is not used.
		/// </summary>
		bool is_external_memory;
		//union
		//{
		WebPRGBABuffer RGBA;
		WebPYUVABuffer YUVA;
		//}
		//u;                       // Nameless union of buffer parameters.
		
		/// <summary>
		/// Internally allocated memory (only when
		/// is_external_memory is false). Should not be used
		/// externally, but accessed via the buffer union.
		/// </summary>
		byte* private_memory;

		/// <summary>
		/// Initialize the structure as empty. Must be called before any other use.
		/// Returns false in case of version mismatch
		/// </summary>
		/// <returns></returns>
		int WebPInitDecBuffer() {
			return this.WebPInitDecBufferInternal(WEBP_DECODER_ABI_VERSION);
		}

	}

	/*
	// Internal, version-checked, entry point
	WEBP_EXTERN(int) WebPInitDecBufferInternal(WebPDecBuffer* const, int);
	*/

	/*
	// Free any memory associated with the buffer. Must always be called last.
	// Note: doesn't free the 'buffer' structure itself.
	WEBP_EXTERN(void) WebPFreeDecBuffer(WebPDecBuffer* buffer);
	*/

	/// <summary>
	/// Enumeration of the status codes
	/// </summary>
	public enum VP8StatusCode
	{
		VP8_STATUS_OK = 0,
		VP8_STATUS_OUT_OF_MEMORY,
		VP8_STATUS_INVALID_PARAM,
		VP8_STATUS_BITSTREAM_ERROR,
		VP8_STATUS_UNSUPPORTED_FEATURE,
		VP8_STATUS_SUSPENDED,
		VP8_STATUS_USER_ABORT,
		VP8_STATUS_NOT_ENOUGH_DATA
	}

	//------------------------------------------------------------------------------
	// Incremental decoding
	//
	// This API allows streamlined decoding of partial data.
	// Picture can be incrementally decoded as data become available thanks to the
	// WebPIDecoder object. This object can be left in a SUSPENDED state if the
	// picture is only partially decoded, pending additional input.
	// Code example:
	//
	//   WebPIDecoder* idec = WebPINew(mode);
	//   while (has_more_data) {
	//     // ... (get additional data)
	//     status = WebPIAppend(idec, new_data, new_data_size);
	//     if (status != VP8_STATUS_SUSPENDED ||
	//       break;
	//     }
	//
	//     // The above call decodes the current available buffer.
	//     // Part of the image can now be refreshed by calling to
	//     // WebPIDecGetRGB()/WebPIDecGetYUV() etc.
	//   }
	//   WebPIDelete(idec);

	/*
	typedef struct WebPIDecoder WebPIDecoder;

	// Creates a new incremental decoder with the supplied buffer parameter.
	// This output_buffer can be passed null, in which case a default output buffer
	// is used (with MODE_RGB). Otherwise, an internal reference to 'output_buffer'
	// is kept, which means that the lifespan of 'output_buffer' must be larger than
	// that of the returned WebPIDecoder object.
	// Returns null if the allocation failed.
	WEBP_EXTERN(WebPIDecoder*) WebPINewDecoder(WebPDecBuffer* output_buffer);

	// Creates a WebPIDecoder object. Returns null in case of failure.
	// TODO(skal): DEPRECATED. Prefer using WebPINewDecoder().
	WEBP_EXTERN(WebPIDecoder*) WebPINew(WEBP_CSP_MODE mode);

	// This function allocates and initializes an incremental-decoder object, which
	// will output the r/g/b(/a) samples specified by 'mode' into a preallocated
	// buffer 'output_buffer'. The size of this buffer is at least
	// 'output_buffer_size' and the stride (distance in bytes between two scanlines)
	// is specified by 'output_stride'. Returns null if the allocation failed.
	WEBP_EXTERN(WebPIDecoder*) WebPINewRGB(WEBP_CSP_MODE mode, byte* output_buffer, int output_buffer_size, int output_stride);

	// This function allocates and initializes an incremental-decoder object, which
	// will output the raw luma/chroma samples into a preallocated planes. The luma
	// plane is specified by its pointer 'luma', its size 'luma_size' and its stride
	// 'luma_stride'. Similarly, the chroma-u plane is specified by the 'u',
	// 'u_size' and 'u_stride' parameters, and the chroma-v plane by 'v', 'v_size'
	// and 'v_size'.
	// Returns null if the allocation failed.
	WEBP_EXTERN(WebPIDecoder*) WebPINewYUV(byte* luma, int luma_size, int luma_stride, byte* u, int u_size, int u_stride, byte* v, int v_size, int v_stride);

	// Deletes the WebPIDecoder object and associated memory. Must always be called
	// if WebPINew, WebPINewRGB or WebPINewYUV succeeded.
	WEBP_EXTERN(void) WebPIDelete(WebPIDecoder* idec);

	// Copies and decodes the next available data. Returns VP8_STATUS_OK when
	// the image is successfully decoded. Returns VP8_STATUS_SUSPENDED when more
	// data is expected. Returns error in other cases.
	WEBP_EXTERN(VP8StatusCode) WebPIAppend(WebPIDecoder* idec, byte* data, uint data_size);

	// A variant of the above function to be used when data buffer contains
	// partial data from the beginning. In this case data buffer is not copied
	// to the internal memory.
	// Note that the value of the 'data' pointer can change between calls to
	// WebPIUpdate, for instance when the data buffer is resized to fit larger data.
	WEBP_EXTERN(VP8StatusCode) WebPIUpdate(WebPIDecoder* idec, byte* data, uint data_size);

	// Returns the r/g/b/(a) image decoded so far. Returns null if output params
	// are not initialized yet. The r/g/b/(a) output type corresponds to the mode
	// specified in WebPINew()/WebPINewRGB(). *last_y is the index of last decoded
	// row in raster scan order. Some pointers (*last_y, *width etc.) can be null if
	// corresponding information is not needed.
	WEBP_EXTERN(byte*) WebPIDecGetRGB(WebPIDecoder* idec, int* last_y, int* width, int* height, int* stride);

	// Same as above function to get YUV image. Returns pointer to the luma plane
	// or null in case of error.
	WEBP_EXTERN(byte*) WebPIDecGetYUV(WebPIDecoder* idec, int* last_y, byte** u, byte** v, int* width, int* height, int* stride, int* uv_stride);

	// Generic call to retrieve information about the displayable area.
	// If non null, the left/right/width/height pointers are filled with the visible
	// rectangular area so far.
	// Returns null in case the incremental decoder object is in an invalid state.
	// Otherwise returns the pointer to the internal representation. This structure
	// is read-only, tied to WebPIDecoder's lifespan and should not be modified.
	WEBP_EXTERN(WebPDecBuffer*) WebPIDecodedArea(WebPIDecoder* idec, int* left, int* top, int* width, int* height);
	*/

	//------------------------------------------------------------------------------
	// Advanced decoding parametrization
	//
	//  Code sample for using the advanced decoding API
	/*
			// A) Init a configuration object
			WebPDecoderConfig config;
			CHECK(WebPInitDecoderConfig(&config));

			// B) optional: retrieve the bitstream's features.
			CHECK(WebPGetFeatures(data, data_size, &config.input) == VP8_STATUS_OK);

			// C) Adjust 'config', if needed
			config.no_fancy = 1;
			config.output.colorspace = MODE_BGRA;
			// etc.

			// Note that you can also make config.output point to an externally
			// supplied memory buffer, provided it's big enough to store the decoded
			// picture. Otherwise, config.output will just be used to allocate memory
			// and store the decoded picture.

			// D) Decode!
			CHECK(WebPDecode(data, data_size, &config) == VP8_STATUS_OK);

			// E) Decoded image is now in config.output (and config.output.u.RGBA)

			// F) Reclaim memory allocated in config's object. It's safe to call
			// this function even if the memory is external and wasn't allocated
			// by WebPDecode().
			WebPFreeDecBuffer(&config.output);
	*/

	// Features gathered from the bitstream
	public partial class WebPBitstreamFeatures
	{
		/// <summary>
		/// the original width, as read from the bitstream
		/// </summary>
		public int width;        

		/// <summary>
		/// the original height, as read from the bitstream
		/// </summary>
		public int height;      

		/// <summary>
		/// true if bitstream contains an alpha channel
		/// </summary>
		public int has_alpha;    

		/// <summary>
		/// if true, using incremental decoding is not recommended.
		/// </summary>
		public int no_incremental_decoding;  

		/// <summary>
		/// TODO(later)
		/// </summary>
		public int rotate;                  
 
		/// <summary>
		/// should be 0 for now. TODO(later)
		/// </summary>
		public int uv_sampling;             

		/// <summary>
		/// should be 0 for now. TODO(later)
		/// </summary>
		public int bitstream_version;        
	}

	/*
	// Internal, version-checked, entry point
	WEBP_EXTERN(VP8StatusCode) WebPGetFeaturesInternal( byte*, uint, WebPBitstreamFeatures* const, int);
	*/

	unsafe class External
	{
		int WEBP_DECODER_ABI_VERSION = 0x0002;

		// Retrieve features from the bitstream. The *features structure is filled
		// with information gathered from the bitstream.
		// Returns false in case of error or version mismatch.
		// In case of error, features.bitstream_status will reflect the error code.
		static VP8StatusCode WebPGetFeatures(byte* data, uint data_size, ref WebPBitstreamFeatures features)
		{
			return WebPGetFeaturesInternal(data, data_size, features, WEBP_DECODER_ABI_VERSION);
		}
	}

	/// <summary>
	/// Decoding options
	/// </summary>
	public partial class WebPDecoderOptions
	{
		/// <summary>
		/// if true, skip the in-loop filtering
		/// </summary>
		public int bypass_filtering;           
   
		/// <summary>
		/// if true, use faster pointwise upsampler
		/// </summary>
		public int no_fancy_upsampling;           

		/// <summary>
		/// if true, cropping is applied _first_
		/// </summary>
		public int use_cropping;                
  
		/// <summary>
		/// top-left position for cropping.
		/// Will be snapped to even values.
		/// </summary>
		public int crop_left, crop_top;         
											
		/// <summary>
		/// dimension of the cropping area
		/// </summary>
		public int crop_width, crop_height;        

		/// <summary>
		/// if true, scaling is applied _afterward_
		/// </summary>
		public int use_scaling;                    

		/// <summary>
		/// final resolution
		/// </summary>
		public int scaled_width, scaled_height;   
 
		/// <summary>
		/// forced rotation (to be applied _last_)
		/// </summary>
		public int force_rotation;                

		/// <summary>
		/// if true, discard enhancement layer
		/// </summary>
		public int no_enhancement;                
 
		/// <summary>
		/// if true, use multi-threaded decoding
		/// </summary>
		public int use_threads;                   
	}

	/// <summary>
	/// Main object storing the configuration for advanced decoding.
	/// </summary>
	public partial class WebPDecoderConfig
	{
		/// <summary>
		/// Immutable bitstream features (optional)
		/// </summary>
		public WebPBitstreamFeatures input;

		/// <summary>
		/// Output buffer (can point to external mem)
		/// </summary>
		public WebPDecBuffer output;         

		/// <summary>
		/// Decoding options
		/// </summary>
		public WebPDecoderOptions options;   
	}

	/*
	// Internal, version-checked, entry point
	WEBP_EXTERN(int) WebPInitDecoderConfigInternal(WebPDecoderConfig* const, int);

	// Initialize the configuration as empty. This function must always be
	// called first, unless WebPGetFeatures() is to be called.
	// Returns false in case of mismatched version.
	static int WebPInitDecoderConfig(WebPDecoderConfig* config) {
		return WebPInitDecoderConfigInternal(config, WEBP_DECODER_ABI_VERSION);
	}

	// Instantiate a new incremental decoder object with requested configuration.
	// The bitstream can be passed using *data and data_size parameter,
	// in which case the features will be parsed and stored into config.input.
	// Otherwise, 'data' can be null and now parsing will occur.
	// Note that 'config' can be null too, in which case a default configuration is
	// used.
	// The return WebPIDecoder object must always be deleted calling WebPIDelete().
	// Returns null in case of error (and config.status will then reflect
	// the error condition).
	WEBP_EXTERN(WebPIDecoder*) WebPIDecode(byte* data, uint data_size, WebPDecoderConfig* config);

	// Non-incremental version. This version decodes the full data at once, taking
	// 'config' into account. Return decoding status (VP8_STATUS_OK if decoding
	// was successful).
	WEBP_EXTERN(VP8StatusCode) WebPDecode(byte* data, uint data_size, WebPDecoderConfig* config);
	*/
}
#endif
