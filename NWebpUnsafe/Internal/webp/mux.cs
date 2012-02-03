using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NWebp.Internal
{
	/// <summary>
	/// Error codes
	/// </summary>
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

	/// <summary>
	/// Flag values for different features used in VP8X chunk.
	/// </summary>
	enum FeatureFlags
	{
		TILE_FLAG       = 0x00000001,
		ANIMATION_FLAG  = 0x00000002,
		ICCP_FLAG       = 0x00000004,
		META_FLAG       = 0x00000008,
		ALPHA_FLAG      = 0x00000010
	}

	/// <summary>
	/// Data type used to describe 'raw' data, e.g., chunk data
	/// (ICC profile, metadata) and WebP compressed image data.
	/// </summary>
	unsafe struct WebPData
	{
		byte* bytes_;
		uint size_;
	}

	/*

	//------------------------------------------------------------------------------
	// Animation.


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
	*/
}
