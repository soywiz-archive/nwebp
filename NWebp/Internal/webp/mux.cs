using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NWebp.Internal
{
	// Error codes
	enum WebPMuxError
	{
		WEBP_MUX_OK                 =  1,
		WEBP_MUX_ERROR              =  0,
		WEBP_MUX_NOT_FOUND          = -1,
		WEBP_MUX_INVALID_ARGUMENT   = -2,
		WEBP_MUX_INVALID_PARAMETER  = -3,
		WEBP_MUX_BAD_DATA           = -4,
		WEBP_MUX_MEMORY_ERROR       = -5,
		WEBP_MUX_NOT_ENOUGH_DATA    = -6
	}

	enum WebPMuxState
	{
		WEBP_MUX_STATE_PARTIAL  =  0,
		WEBP_MUX_STATE_COMPLETE =  1,
		WEBP_MUX_STATE_ERROR    = -1
	}

	// Flag values for different features used in VP8X chunk.
	enum FeatureFlags
	{
		TILE_FLAG       = 0x00000001,
		ANIMATION_FLAG  = 0x00000002,
		ICCP_FLAG       = 0x00000004,
		META_FLAG       = 0x00000008,
		ALPHA_FLAG      = 0x00000010
	}

	//typedef struct WebPMux WebPMux;   // main opaque object.

	// Data type used to describe 'raw' data, e.g., chunk data
	// (ICC profile, metadata) and WebP compressed image data.
	unsafe struct WebPData
	{
		byte* bytes_;
		uint size_;
	}

	/*
	//------------------------------------------------------------------------------
	// Life of a Mux object
		
	// Creates an empty mux object.
	// Returns:
	//   A pointer to the newly created empty mux object.
	WEBP_EXTERN(WebPMux*) WebPMuxNew(void);

	// Deletes the mux object.
	// Parameters:
	//   mux - (in/out) object to be deleted
	WEBP_EXTERN(void) WebPMuxDelete(WebPMux* const mux);

	//------------------------------------------------------------------------------
	// Mux creation.

	// Creates a mux object from raw data given in WebP RIFF format.
	// Parameters:
	//   data - (in) the raw data in WebP RIFF format
	//   size - (in) size of raw data
	//   copy_data - (in) value 1 indicates given data WILL copied to the mux, and
	//               value 0 indicates data will NOT be copied.
	//   mux_state - (out) indicates the state of the mux returned. Can be passed
	//               null if not required.
	// Returns:
	//   A pointer to the mux object created from given data - on success.
	//   null - In case of invalid data or memory error.
	WEBP_EXTERN(WebPMux*) WebPMuxCreate(const byte* data, uint size, int copy_data, WebPMuxState* const mux_state);

	//------------------------------------------------------------------------------
	// Single Image.

	// Sets the image in the mux object. Any existing images (including frame/tile)
	// will be removed.
	// Parameters:
	//   mux - (in/out) object in which the image is to be set
	//   data - (in) the image data to be set. The data can be either a VP8
	//          bitstream or a single-image WebP file (non-animated & non-tiled)
	//   size - (in) size of the image data
	//   alpha_data - (in) the alpha data corresponding to the image (if present)
	//   alpha_size - (in) size of alpha chunk data
	//   copy_data - (in) value 1 indicates given data WILL copied to the mux, and
	//               value 0 indicates data will NOT be copied.
	// Returns:
	//   WEBP_MUX_INVALID_ARGUMENT - if mux is null or data is null.
	//   WEBP_MUX_MEMORY_ERROR - on memory allocation error.
	//   WEBP_MUX_OK - on success.
	WEBP_EXTERN(WebPMuxError) WebPMuxSetImage(WebPMux* const mux, const byte* data, uint size, const byte* alpha_data, uint alpha_size, int copy_data);

	// Gets a reference to the image in the mux object.
	// The caller should NOT free the returned data.
	// Parameters:
	//   mux - (in) object from which the image is to be fetched
	//   image - (out) the image data
	//   alpha - (out) the alpha data of the image (if present)
	// Returns:
	//   WEBP_MUX_INVALID_ARGUMENT - if either mux or image is null
	//                               OR if mux contains animation/tiling.
	//   WEBP_MUX_NOT_FOUND - if image is not present in mux object.
	//   WEBP_MUX_OK - on success.
	WEBP_EXTERN(WebPMuxError) WebPMuxGetImage(const WebPMux* const mux, WebPData* const image, WebPData* const alpha);

	// Deletes the image in the mux object.
	// Parameters:
	//   mux - (in/out) object from which the image is to be deleted
	// Returns:
	//   WEBP_MUX_INVALID_ARGUMENT - if mux is null
	//                               OR if mux contains animation/tiling.
	//   WEBP_MUX_NOT_FOUND - if image is not present in mux object.
	//   WEBP_MUX_OK - on success.
	WEBP_EXTERN(WebPMuxError) WebPMuxDeleteImage(WebPMux* const mux);

	//------------------------------------------------------------------------------
	// XMP Metadata.

	// Sets the XMP metadata in the mux object. Any existing metadata chunk(s) will
	// be removed.
	// Parameters:
	//   mux - (in/out) object to which the XMP metadata is to be added
	//   data - (in) the XMP metadata data to be added
	//   size - (in) size of the XMP metadata data
	//   copy_data - (in) value 1 indicates given data WILL copied to the mux, and
	//               value 0 indicates data will NOT be copied.
	// Returns:
	//   WEBP_MUX_INVALID_ARGUMENT - if mux is null or data is null.
	//   WEBP_MUX_MEMORY_ERROR - on memory allocation error.
	//   WEBP_MUX_OK - on success.
	WEBP_EXTERN(WebPMuxError) WebPMuxSetMetadata(WebPMux* const mux, const byte* data, uint size, int copy_data);

	// Gets a reference to the XMP metadata in the mux object.
	// The caller should NOT free the returned data.
	// Parameters:
	//   mux - (in) object from which the XMP metadata is to be fetched
	//   metadata - (out) XMP metadata
	// Returns:
	//   WEBP_MUX_INVALID_ARGUMENT - if either mux or metadata is null.
	//   WEBP_MUX_NOT_FOUND - if metadata is not present in mux object.
	//   WEBP_MUX_OK - on success.
	WEBP_EXTERN(WebPMuxError) WebPMuxGetMetadata(const WebPMux* const mux, WebPData* const metadata);

	// Deletes the XMP metadata in the mux object.
	// Parameters:
	//   mux - (in/out) object from which XMP metadata is to be deleted
	// Returns:
	//   WEBP_MUX_INVALID_ARGUMENT - if mux is null
	//   WEBP_MUX_NOT_FOUND - If mux does not contain metadata.
	//   WEBP_MUX_OK - on success.
	WEBP_EXTERN(WebPMuxError) WebPMuxDeleteMetadata(WebPMux* const mux);

	//------------------------------------------------------------------------------
	// ICC Color Profile.

	// Sets the color profile in the mux object. Any existing color profile chunk(s)
	// will be removed.
	// Parameters:
	//   mux - (in/out) object to which the color profile is to be added
	//   data - (in) the color profile data to be added
	//   size - (in) size of the color profile data
	//   copy_data - (in) value 1 indicates given data WILL copied to the mux, and
	//               value 0 indicates data will NOT be copied.
	// Returns:
	//   WEBP_MUX_INVALID_ARGUMENT - if mux is null or data is null
	//   WEBP_MUX_MEMORY_ERROR - on memory allocation error
	//   WEBP_MUX_OK - on success
	WEBP_EXTERN(WebPMuxError) WebPMuxSetColorProfile(WebPMux* const mux, const byte* data, uint size, int copy_data);

	// Gets a reference to the color profile in the mux object.
	// The caller should NOT free the returned data.
	// Parameters:
	//   mux - (in) object from which the color profile data is to be fetched
	//   color_profile - (out) color profile data
	// Returns:
	//   WEBP_MUX_INVALID_ARGUMENT - if either mux or color_profile is null.
	//   WEBP_MUX_NOT_FOUND - if color profile is not present in mux object.
	//   WEBP_MUX_OK - on success.
	WEBP_EXTERN(WebPMuxError) WebPMuxGetColorProfile(const WebPMux* const mux, WebPData* const color_profile);

	// Deletes the color profile in the mux object.
	// Parameters:
	//   mux - (in/out) object from which color profile is to be deleted
	// Returns:
	//   WEBP_MUX_INVALID_ARGUMENT - if mux is null
	//   WEBP_MUX_NOT_FOUND - If mux does not contain color profile.
	//   WEBP_MUX_OK - on success.
	WEBP_EXTERN(WebPMuxError) WebPMuxDeleteColorProfile(WebPMux* const mux);

	//------------------------------------------------------------------------------
	// Animation.

	// Adds an animation frame to the mux object.
	// nth=0 has a special meaning - last position.
	// Parameters:
	//   mux - (in/out) object to which an animation frame is to be added
	//   nth - (in) The position at which the frame is to be added.
	//   data - (in) the raw VP8 image data corresponding to frame image. The data
	//          can be either a VP8 bitstream or a single-image WebP file
	//          (non-animated & non-tiled)
	//   size - (in) size of frame chunk data
	//   alpha_data - (in) the alpha data corresponding to frame image (if present)
	//   alpha_size - (in) size of alpha chunk data
	//   x_offset - (in) x-offset of the frame to be added
	//   y_offset - (in) y-offset of the frame to be added
	//   duration - (in) duration of the frame to be added (in milliseconds)
	//   copy_data - (in) value 1 indicates given data WILL copied to the mux, and
	//               value 0 indicates data will NOT be copied.
	// Returns:
	//   WEBP_MUX_INVALID_ARGUMENT - if mux is null or data is null
	//   WEBP_MUX_NOT_FOUND - If we have less than (nth-1) frames before adding.
	//   WEBP_MUX_MEMORY_ERROR - on memory allocation error.
	//   WEBP_MUX_OK - on success.
	WEBP_EXTERN(WebPMuxError) WebPMuxAddFrame(WebPMux* const mux, uint nth, const byte* data, uint size, const byte* alpha_data, uint alpha_size, uint x_offset, uint y_offset, uint duration, int copy_data);

	// TODO(urvang): Create a struct as follows to reduce argument list size:
	// typedef struct {
	//  int nth;
	//  byte* data;
	//  uint data_size;
	//  byte* alpha;
	//  uint alpha_size;
	//  uint x_offset, y_offset;
	//  uint duration;
	// } FrameInfo;

	// Gets a reference to the nth animation frame from the mux object.
	// The caller should NOT free the returned data.
	// nth=0 has a special meaning - last position.
	// Parameters:
	//   mux - (in) object from which the info is to be fetched
	//   nth - (in) index of the frame in the mux object
	//   image - (out) the image data
	//   alpha - (out) the alpha data corresponding to frame image (if present)
	//   x_offset - (out) x-offset of the returned frame
	//   y_offset - (out) y-offset of the returned frame
	//   duration - (out) duration of the returned frame (in milliseconds)
	// Returns:
	//   WEBP_MUX_INVALID_ARGUMENT - if either mux, image, x_offset,
	//                               y_offset, or duration is null
	//   WEBP_MUX_NOT_FOUND - if there are less than nth frames in the mux object.
	//   WEBP_MUX_BAD_DATA - if nth frame chunk in mux is invalid.
	//   WEBP_MUX_OK - on success.
	WEBP_EXTERN(WebPMuxError) WebPMuxGetFrame(const WebPMux* const mux, uint nth, WebPData* const image, WebPData* const alpha, uint* x_offset, uint* y_offset, uint* duration);

	// Deletes an animation frame from the mux object.
	// nth=0 has a special meaning - last position.
	// Parameters:
	//   mux - (in/out) object from which a frame is to be deleted
	//   nth - (in) The position from which the frame is to be deleted
	// Returns:
	//   WEBP_MUX_INVALID_ARGUMENT - if mux is null
	//   WEBP_MUX_NOT_FOUND - If there are less than nth frames in the mux object
	//                        before deletion.
	//   WEBP_MUX_OK - on success.
	WEBP_EXTERN(WebPMuxError) WebPMuxDeleteFrame(WebPMux* const mux, uint nth);

	// Sets the animation loop count in the mux object. Any existing loop count
	// value(s) will be removed.
	// Parameters:
	//   mux - (in/out) object in which loop chunk is to be set/added
	//   loop_count - (in) animation loop count value.
	//                Note that loop_count of zero denotes infinite loop.
	// Returns:
	//   WEBP_MUX_INVALID_ARGUMENT - if mux is null
	//   WEBP_MUX_MEMORY_ERROR - on memory allocation error.
	//   WEBP_MUX_OK - on success.
	WEBP_EXTERN(WebPMuxError) WebPMuxSetLoopCount(WebPMux* const mux, uint loop_count);

	// Gets the animation loop count from the mux object.
	// Parameters:
	//   mux - (in) object from which the loop count is to be fetched
	//   loop_count - (out) the loop_count value present in the LOOP chunk
	// Returns:
	//   WEBP_MUX_INVALID_ARGUMENT - if either of mux or loop_count is null
	//   WEBP_MUX_NOT_FOUND - if loop chunk is not present in mux object.
	//   WEBP_MUX_OK - on success.
	WEBP_EXTERN(WebPMuxError) WebPMuxGetLoopCount(const WebPMux* const mux, uint* loop_count);

	//------------------------------------------------------------------------------
	// Tiling.

	// Adds a tile to the mux object.
	// nth=0 has a special meaning - last position.
	// Parameters:
	//   mux - (in/out) object to which a tile is to be added
	//   nth - (in) The position at which the tile is to be added.
	//   data - (in) the raw VP8 image data corresponding to tile image.  The data
	//          can be either a VP8 bitstream or a single-image WebP file
	//          (non-animated & non-tiled)
	//   size - (in) size of tile chunk data
	//   alpha_data - (in) the alpha data corresponding to tile image (if present)
	//   alpha_size - (in) size of alpha chunk data
	//   x_offset - (in) x-offset of the tile to be added
	//   y_offset - (in) y-offset of the tile to be added
	//   copy_data - (in) value 1 indicates given data WILL copied to the mux, and
	//               value 0 indicates data will NOT be copied.
	// Returns:
	//   WEBP_MUX_INVALID_ARGUMENT - if mux is null or data is null
	//   WEBP_MUX_NOT_FOUND - If we have less than (nth-1) tiles before adding.
	//   WEBP_MUX_MEMORY_ERROR - on memory allocation error.
	//   WEBP_MUX_OK - on success.
	WEBP_EXTERN(WebPMuxError) WebPMuxAddTile(WebPMux* const mux, uint nth, const byte* data, uint size, const byte* alpha_data, uint alpha_size, uint x_offset, uint y_offset, int copy_data);

	// Gets a reference to the nth tile from the mux object.
	// The caller should NOT free the returned data.
	// nth=0 has a special meaning - last position.
	// Parameters:
	//   mux - (in) object from which the info is to be fetched
	//   nth - (in) index of the tile in the mux object
	//   image - (out) the image data
	//   alpha - (out) the alpha data corresponding to tile image (if present)
	//   x_offset - (out) x-offset of the returned tile
	//   y_offset - (out) y-offset of the returned tile
	// Returns:
	//   WEBP_MUX_INVALID_ARGUMENT - if either mux, image, x_offset or
	//                               y_offset is null
	//   WEBP_MUX_NOT_FOUND - if there are less than nth tiles in the mux object.
	//   WEBP_MUX_BAD_DATA - if nth tile chunk in mux is invalid.
	//   WEBP_MUX_OK - on success.
	WEBP_EXTERN(WebPMuxError) WebPMuxGetTile(const WebPMux* const mux, uint nth, WebPData* const image, WebPData* const alpha, uint* x_offset, uint* y_offset);

	// Deletes a tile from the mux object.
	// nth=0 has a special meaning - last position
	// Parameters:
	//   mux - (in/out) object from which a tile is to be deleted
	//   nth - (in) The position from which the tile is to be deleted
	// Returns:
	//   WEBP_MUX_INVALID_ARGUMENT - if mux is null
	//   WEBP_MUX_NOT_FOUND - If there are less than nth tiles in the mux object
	//                        before deletion.
	//   WEBP_MUX_OK - on success.
	WEBP_EXTERN(WebPMuxError) WebPMuxDeleteTile(WebPMux* const mux, uint nth);

	//------------------------------------------------------------------------------
	// Misc Utilities.

	// Gets the feature flags from the mux object.
	// Parameters:
	//   mux - (in) object from which the features are to be fetched
	//   flags - (out) the flags specifying which features are present in the
	//           mux object. This will be an OR of various flag values.
	//           Enum 'FeatureFlags' can be used to test for individual flag values.
	// Returns:
	//   WEBP_MUX_INVALID_ARGUMENT - if mux is null or flags is null
	//   WEBP_MUX_NOT_FOUND - if VP8X chunk is not present in mux object.
	//   WEBP_MUX_BAD_DATA - if VP8X chunk in mux is invalid.
	//   WEBP_MUX_OK - on success.
	WEBP_EXTERN(WebPMuxError) WebPMuxGetFeatures(const WebPMux* const mux, uint* flags);

	// Gets number of chunks having tag value tag in the mux object.
	// Parameters:
	//   mux - (in) object from which the info is to be fetched
	//   tag - (in) tag name specifying the type of chunk
	//   num_elements - (out) number of chunks corresponding to the specified tag
	// Returns:
	//   WEBP_MUX_INVALID_ARGUMENT - if either mux, tag or num_elements is null
	//   WEBP_MUX_OK - on success.
	WEBP_EXTERN(WebPMuxError) WebPMuxNumNamedElements(const WebPMux* const mux, const char* tag, int* num_elements);

	// Assembles all chunks in WebP RIFF format and returns in output_data.
	// This function also validates the mux object.
	// The content of '*output_data' is allocated using malloc(), and NOT
	// owned by the 'mux' object.
	// It MUST be deallocated by the caller by calling free().
	// Parameters:
	//   mux - (in/out) object whose chunks are to be assembled
	//   output_data - (out) byte array where assembled WebP data is returned
	//   output_size - (out) size of returned data
	// Returns:
	//   WEBP_MUX_BAD_DATA - if mux object is invalid.
	//   WEBP_MUX_INVALID_ARGUMENT - if either mux, output_data or output_size is
	//                               null.
	//   WEBP_MUX_MEMORY_ERROR - on memory allocation error.
	//   WEBP_MUX_OK - on success
	WEBP_EXTERN(WebPMuxError) WebPMuxAssemble(WebPMux* const mux, byte** output_data, uint* output_size);

	//------------------------------------------------------------------------------
	*/
}
